using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Services.Airtable.Scope;

/// <summary>
/// Envia para o Airtable (base SCOPE) os projetos marcados com ScopePendente:
/// aceites → cria/atualiza; já não aceites mas enviados antes → apaga no Airtable.
/// </summary>
public class ScopeSyncService
{
    private const int TamanhoLote = 50;

    private readonly AppDbContext _db;
    private readonly IScopeClient _client;
    private readonly ILogger<ScopeSyncService> _logger;

    public ScopeSyncService(AppDbContext db, IScopeClient client, ILogger<ScopeSyncService> logger)
    {
        _db = db;
        _client = client;
        _logger = logger;
    }

    /// <summary>Processa um lote. Devolve quantos projetos foram tratados (0 = nada a fazer).</summary>
    public async Task<int> ProcessarLoteAsync(CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var projetos = await _db.ProjetosPlano
            .Include(p => p.Plano)
            .Where(p => p.ScopePendente && (p.ScopeProximaTentativa == null || p.ScopeProximaTentativa <= agora))
            .OrderBy(p => p.Id)
            .Take(TamanhoLote)
            .ToListAsync(ct);
        if (projetos.Count == 0) return 0;

        // Plano reaberto entretanto: deixa de enviar (não apaga o que já foi enviado).
        foreach (var p in projetos.Where(p => p.Plano.Estado != EstadoPlano.Concluido)) p.ScopePendente = false;

        var concluidos = projetos.Where(p => p.Plano.Estado == EstadoPlano.Concluido).ToList();
        var enviar = concluidos.Where(ScopeMapper.DeveEstarNoScope).ToList();
        var apagar = concluidos.Where(p => !ScopeMapper.DeveEstarNoScope(p)).ToList();

        try
        {
            if (enviar.Count > 0)
            {
                var ids = await _client.UpsertAsync(enviar.Select(p => (ScopeMapper.Chave(p), ScopeMapper.Campos(p, p.Plano.Ano))).ToList(), ct);
                foreach (var p in enviar)
                {
                    if (ids.TryGetValue(ScopeMapper.Chave(p), out var rec)) p.ScopeRecordId = rec;
                    Sucesso(p);
                }
            }

            var comRegisto = apagar.Where(p => !string.IsNullOrEmpty(p.ScopeRecordId)).ToList();
            if (comRegisto.Count > 0) await _client.ApagarAsync(comRegisto.Select(p => p.ScopeRecordId!).ToList(), ct);
            foreach (var p in apagar)
            {
                p.ScopeRecordId = null;
                Sucesso(p);
            }
        }
        catch (Exception ex) when (!ct.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "Airtable SCOPE: falha ao enviar {N} projeto(s).", concluidos.Count);
            foreach (var p in concluidos) Erro(p, ex);
        }

        await _db.SaveChangesAsync(ct);
        return projetos.Count;
    }

    private static void Sucesso(ProjetoPlano p)
    {
        p.ScopePendente = false;
        p.ScopeEnviadoEm = DateTime.UtcNow;
        p.ScopeErro = null;
        p.ScopeTentativas = 0;
        p.ScopeProximaTentativa = null;
    }

    private static void Erro(ProjetoPlano p, Exception ex)
    {
        p.ScopeTentativas++;
        p.ScopeProximaTentativa = DateTime.UtcNow.AddMinutes(Math.Min(60, Math.Pow(2, Math.Min(p.ScopeTentativas - 1, 6))));
        var msg = ex.Message;
        p.ScopeErro = msg.Length > 1000 ? msg[..1000] : msg;
    }

    /// <summary>Marca para envio todos os projetos de um plano (ao concluir ou ao pedir "Reenviar").</summary>
    public static async Task<int> MarcarPlanoAsync(AppDbContext db, int planoId, CancellationToken ct = default)
    {
        var projetos = await db.ProjetosPlano.Where(p => p.PlanoId == planoId).ToListAsync(ct);
        var marcados = 0;
        foreach (var p in projetos.Where(p => ScopeMapper.DeveEstarNoScope(p) || !string.IsNullOrEmpty(p.ScopeRecordId)))
        {
            p.ScopePendente = true;
            p.ScopeProximaTentativa = null;
            marcados++;
        }
        return marcados;
    }
}

/// <summary>
/// Sempre que um projeto de um plano CONCLUÍDO é criado ou alterado (exceto nos próprios
/// campos de envio), fica marcado para ser reenviado ao Airtable.
/// </summary>
public class ScopePendenteInterceptor : SaveChangesInterceptor
{
    private static readonly HashSet<string> CamposEnvio = new()
    {
        nameof(ProjetoPlano.ScopePendente), nameof(ProjetoPlano.ScopeRecordId), nameof(ProjetoPlano.ScopeEnviadoEm),
        nameof(ProjetoPlano.ScopeErro), nameof(ProjetoPlano.ScopeTentativas), nameof(ProjetoPlano.ScopeProximaTentativa)
    };

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Marcar(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Marcar(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private static void Marcar(DbContext? context)
    {
        if (context is not AppDbContext db) return;

        var entradas = db.ChangeTracker.Entries<ProjetoPlano>()
            .Where(e => e.State == EntityState.Added
                        || (e.State == EntityState.Modified && e.Properties.Any(pr => pr.IsModified && !CamposEnvio.Contains(pr.Metadata.Name))))
            .ToList();
        if (entradas.Count == 0) return;

        var planoIds = entradas.Select(e => e.Entity.PlanoId).Distinct().ToList();
        var concluidos = db.Planos.AsNoTracking()
            .Where(p => planoIds.Contains(p.Id) && p.Estado == EstadoPlano.Concluido)
            .Select(p => p.Id)
            .ToHashSet();

        foreach (var e in entradas.Where(e => concluidos.Contains(e.Entity.PlanoId)))
        {
            e.Entity.ScopePendente = true;
            e.Entity.ScopeProximaTentativa = null;
        }
    }
}

/// <summary>Processa os envios para o SCOPE em segundo plano, a cada IntervaloSegundos.</summary>
public class ScopeSyncWorker : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<ScopeOptions> _options;
    private readonly IOptionsMonitor<AirtableOptions> _airtable;
    private readonly ILogger<ScopeSyncWorker> _logger;

    public ScopeSyncWorker(IServiceScopeFactory scopeFactory, IOptionsMonitor<ScopeOptions> options,
        IOptionsMonitor<AirtableOptions> airtable, ILogger<ScopeSyncWorker> logger)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _airtable = airtable;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Yield();
        while (!stoppingToken.IsCancellationRequested)
        {
            var tratados = 0;
            try
            {
                var o = _options.CurrentValue;
                if (o.Enabled && !string.IsNullOrWhiteSpace(o.TokenEfetivo(_airtable.CurrentValue.Token)))
                {
                    using var scope = _scopeFactory.CreateScope();
                    tratados = await scope.ServiceProvider.GetRequiredService<ScopeSyncService>().ProcessarLoteAsync(stoppingToken);
                    if (tratados > 0) _logger.LogInformation("Airtable SCOPE: {N} projeto(s) tratados.", tratados);
                }
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Airtable SCOPE: erro inesperado no worker.");
            }

            if (tratados > 0) continue;
            try
            {
                await Task.Delay(TimeSpan.FromSeconds(Math.Max(5, _options.CurrentValue.IntervaloSegundos)), stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
        }
    }
}
