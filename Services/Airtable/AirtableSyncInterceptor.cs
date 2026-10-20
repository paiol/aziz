using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.Options;
using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Services.Airtable;

/// <summary>
/// Acorda o worker de sincronização (sem esperar pelo intervalo) quando há alterações novas.
/// </summary>
public class AirtableSyncSinal
{
    private readonly SemaphoreSlim _sinal = new(0, 1);

    public void Sinalizar()
    {
        try
        {
            if (_sinal.CurrentCount == 0) _sinal.Release();
        }
        catch (SemaphoreFullException)
        {
            // Já estava sinalizado.
        }
    }

    public Task<bool> EsperarAsync(TimeSpan maximo, CancellationToken ct) => _sinal.WaitAsync(maximo, ct);
}

/// <summary>
/// Interceptor do EF Core: em cada SaveChanges da app deteta o que mudou nas entidades que vão
/// para o Airtable e acrescenta linhas à fila AirtableSyncPendentes. Assim não é preciso mexer em
/// nenhum controller — qualquer gravação (criar, editar, apagar, importar Excel, adjudicar...)
/// fica coberta. Uma falha aqui nunca impede a gravação do utilizador: só fica registada no log.
/// </summary>
public class AirtableSyncInterceptor : SaveChangesInterceptor
{
    private readonly IOptionsMonitor<AirtableOptions> _options;
    private readonly AirtableSyncSinal _sinal;
    private readonly ILogger<AirtableSyncInterceptor> _logger;

    // O SQL Server aceita no máximo 2100 parâmetros por comando (2 por linha + 1).
    private const int LoteInsert = 500;

    // Entidades alteradas, guardadas entre o "antes" e o "depois" do SaveChanges de cada DbContext.
    private readonly ConditionalWeakTable<DbContext, List<object>> _alteradas = new();

    public AirtableSyncInterceptor(IOptionsMonitor<AirtableOptions> options,
        AirtableSyncSinal sinal, ILogger<AirtableSyncInterceptor> logger)
    {
        _options = options;
        _sinal = sinal;
        _logger = logger;
    }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Capturar(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData,
        InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Capturar(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result)
    {
        var alvos = Recolher(eventData.Context);
        if (alvos.Count > 0)
        {
            try
            {
                foreach (var lote in alvos.Chunk(LoteInsert))
                {
                    var (sql, parametros) = InsertPendentes(lote);
                    eventData.Context!.Database.ExecuteSqlRaw(sql, parametros);
                }
                _sinal.Sinalizar();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Airtable: não foi possível registar {N} alteração(ões) na fila.", alvos.Count);
            }
        }
        return base.SavedChanges(eventData, result);
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result,
        CancellationToken cancellationToken = default)
    {
        var alvos = Recolher(eventData.Context);
        if (alvos.Count > 0)
        {
            try
            {
                foreach (var lote in alvos.Chunk(LoteInsert))
                {
                    var (sql, parametros) = InsertPendentes(lote);
                    // CancellationToken.None: a gravação do utilizador já foi feita, a fila também tem de ficar.
                    await eventData.Context!.Database.ExecuteSqlRawAsync(sql, parametros, CancellationToken.None);
                }
                _sinal.Sinalizar();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Airtable: não foi possível registar {N} alteração(ões) na fila.", alvos.Count);
            }
        }
        return await base.SavedChangesAsync(eventData, result, cancellationToken);
    }

    /// <summary>
    /// INSERT na fila feito pela MESMA ligação do DbContext que gravou: se o controller estiver
    /// dentro de uma transação (BeginTransaction), a linha da fila só fica visível quando a
    /// transação for confirmada — e desaparece se for revertida.
    /// </summary>
    internal static (string Sql, object[] Parametros) InsertPendentes(IReadOnlyList<(string Tipo, int Id)> alvos)
    {
        var agora = DateTime.UtcNow;
        var parametros = new List<object> { agora };
        var valores = new List<string>();
        foreach (var (tipo, id) in alvos)
        {
            parametros.Add(tipo);
            parametros.Add(id);
            valores.Add($"({{{parametros.Count - 2}}}, {{{parametros.Count - 1}}}, {{0}}, 0, {{0}})");
        }

        var sql = "INSERT INTO [AirtableSyncPendentes] ([Tipo], [EntidadeId], [CriadoEm], [Tentativas], [ProximaTentativaEm]) VALUES "
                  + string.Join(", ", valores);
        return (sql, parametros.ToArray());
    }

    public override void SaveChangesFailed(DbContextErrorEventData eventData)
    {
        if (eventData.Context != null) _alteradas.Remove(eventData.Context);
        base.SaveChangesFailed(eventData);
    }

    public override Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default)
    {
        if (eventData.Context != null) _alteradas.Remove(eventData.Context);
        return base.SaveChangesFailedAsync(eventData, cancellationToken);
    }

    private void Capturar(DbContext? context)
    {
        if (context == null || !_options.CurrentValue.Ativo) return;

        try
        {
            var alteradas = context.ChangeTracker.Entries()
                .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted)
                .Select(e => e.Entity)
                .Where(EhSincronizada)
                .ToList();

            if (alteradas.Count > 0) _alteradas.AddOrUpdate(context, alteradas);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Airtable: erro ao detetar alterações.");
        }
    }

    /// <summary>
    /// Depois de gravar (os IDs das entidades novas já existem), traduz as entidades alteradas
    /// em linhas da fila, sem repetidos.
    /// </summary>
    private List<(string Tipo, int Id)> Recolher(DbContext? context)
    {
        if (context == null || !_alteradas.TryGetValue(context, out var alteradas)) return new();
        _alteradas.Remove(context);

        return alteradas
            .Select(Alvo)
            .Where(a => a.Id > 0)
            .Distinct()
            .ToList();
    }

    private static bool EhSincronizada(object entidade) => entidade is Processo or PedidoProposta or Criterio
        or Proposta or ItemProposta or Avaliacao or MemoriaCalculoAvaliacao or ItemMaterial;

    private static (string Tipo, int Id) Alvo(object entidade) => entidade switch
    {
        Processo p => (AirtableSyncService.TipoProcesso, p.Id),
        PedidoProposta pp => (AirtableSyncService.TipoPedido, pp.Id),
        Criterio c => (AirtableSyncService.TipoProcesso, c.ProcessoId),
        Proposta pr => (AirtableSyncService.TipoProcesso, pr.ProcessoId),
        ItemProposta ip => (AirtableSyncService.TipoProposta, ip.PropostaId),
        Avaliacao a => (AirtableSyncService.TipoProposta, a.PropostaId),
        MemoriaCalculoAvaliacao m => (AirtableSyncService.TipoProposta, m.PropostaId),
        ItemMaterial im => (AirtableSyncService.TipoItemMaterial, im.Id),
        _ => ("", 0)
    };
}
