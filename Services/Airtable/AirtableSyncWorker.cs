using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ComparacaoPropostas.Data;

namespace ComparacaoPropostas.Services.Airtable;

/// <summary>
/// Processa a fila AirtableSyncPendentes em segundo plano: acorda logo que há uma gravação
/// (com uma pequena espera para juntar gravações seguidas) e, de qualquer forma, a cada
/// IntervaloSegundos. Erros ficam na linha da fila e são repetidos com espera crescente
/// (1, 2, 4, 8... minutos, até 1 hora).
///
/// Nota IIS/SmarterASP: se o site ficar parado, o IIS pode desligar a aplicação e o worker
/// pára com ela. Nada se perde — a fila está na base de dados e é retomada no próximo pedido.
/// </summary>
public class AirtableSyncWorker : BackgroundService
{
    private const int TamanhoLote = 200;

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<AirtableOptions> _options;
    private readonly AirtableSyncSinal _sinal;
    private readonly ILogger<AirtableSyncWorker> _logger;
    private DateTime _ultimaLimpeza = DateTime.MinValue;

    public AirtableSyncWorker(IServiceScopeFactory scopeFactory, IOptionsMonitor<AirtableOptions> options,
        AirtableSyncSinal sinal, ILogger<AirtableSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _sinal = sinal;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Não atrasar o arranque da aplicação.
        await Task.Yield();

        while (!stoppingToken.IsCancellationRequested)
        {
            var houveTrabalho = false;
            try
            {
                if (_options.CurrentValue.Ativo)
                    houveTrabalho = await ProcessarLoteAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Airtable: erro inesperado no worker de sincronização.");
            }

            if (houveTrabalho) continue; // pode haver mais na fila

            try
            {
                var intervalo = TimeSpan.FromSeconds(Math.Max(5, _options.CurrentValue.IntervaloSegundos));
                var acordado = await _sinal.EsperarAsync(intervalo, stoppingToken);
                if (acordado)
                    await Task.Delay(TimeSpan.FromSeconds(Math.Max(0, _options.CurrentValue.AtrasoAgruparSegundos)), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }

    /// <summary>Processa um lote da fila. Devolve true se havia pendentes.</summary>
    internal async Task<bool> ProcessarLoteAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var sync = scope.ServiceProvider.GetRequiredService<IAirtableSyncService>();

        var agora = DateTime.UtcNow;
        var pendentes = await db.AirtableSyncPendentes
            .Where(p => p.ProcessadoEm == null && p.ProximaTentativaEm <= agora)
            .OrderBy(p => p.Id)
            .Take(TamanhoLote)
            .ToListAsync(ct);

        if (pendentes.Count == 0)
        {
            await LimparAntigosAsync(db, ct);
            return false;
        }

        // Várias linhas (ex.: 40 itens da mesma proposta) resolvem para o mesmo processo:
        // agrupa por alvo para sincronizar cada processo uma só vez.
        var porAlvo = new Dictionary<(string Tipo, int Id), List<Models.Entities.AirtableSyncPendente>>();
        foreach (var p in pendentes)
        {
            (string Tipo, int Id)? alvo;
            try
            {
                alvo = await sync.ResolverAlvoAsync(p.Tipo, p.EntidadeId, ct);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                MarcarErro(p, ex);
                continue;
            }

            if (alvo == null)
            {
                p.ProcessadoEm = DateTime.UtcNow;
                p.Erro = null;
                continue;
            }

            if (!porAlvo.TryGetValue(alvo.Value, out var lista))
                porAlvo[alvo.Value] = lista = new();
            lista.Add(p);
        }
        await db.SaveChangesAsync(ct);

        foreach (var ((tipo, id), linhas) in porAlvo)
        {
            try
            {
                if (tipo == AirtableSyncService.TipoItemMaterial)
                    await sync.SincronizarItemMaterialAsync(id, ct);
                else
                    await sync.SincronizarProcessoAsync(id, ct);

                foreach (var p in linhas)
                {
                    p.ProcessadoEm = DateTime.UtcNow;
                    p.Erro = null;
                }
                _logger.LogInformation("Airtable: {Tipo} {Id} sincronizado.", tipo, id);
            }
            catch (Exception ex) when (!ct.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "Airtable: falha ao sincronizar {Tipo} {Id}.", tipo, id);
                foreach (var p in linhas) MarcarErro(p, ex);
            }

            await db.SaveChangesAsync(ct);
        }

        return true;
    }

    private static void MarcarErro(Models.Entities.AirtableSyncPendente p, Exception ex)
    {
        p.Tentativas++;
        var minutos = Math.Min(60, Math.Pow(2, Math.Min(p.Tentativas - 1, 6)));
        p.ProximaTentativaEm = DateTime.UtcNow.AddMinutes(minutos);
        var msg = ex.Message;
        p.Erro = msg.Length > 1000 ? msg[..1000] : msg;
    }

    /// <summary>Uma vez por dia apaga da fila as linhas já processadas há mais de 30 dias.</summary>
    private async Task LimparAntigosAsync(AppDbContext db, CancellationToken ct)
    {
        if (DateTime.UtcNow - _ultimaLimpeza < TimeSpan.FromDays(1)) return;
        _ultimaLimpeza = DateTime.UtcNow;

        var limite = DateTime.UtcNow.AddDays(-30);
        var apagados = await db.AirtableSyncPendentes
            .Where(p => p.ProcessadoEm != null && p.ProcessadoEm < limite)
            .ExecuteDeleteAsync(ct);
        if (apagados > 0) _logger.LogInformation("Airtable: {N} linha(s) antigas removidas da fila.", apagados);
    }
}
