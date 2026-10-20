using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using S = ComparacaoPropostas.Services.Airtable.AirtableSchema;

namespace ComparacaoPropostas.Services.Airtable;

public interface IAirtableSyncService
{
    /// <summary>
    /// Converte uma linha da fila (Processo/Pedido/Proposta/ItemMaterial) no que é preciso
    /// sincronizar: ("Processo", id), ("ItemMaterial", id) ou null se não houver nada a fazer.
    /// </summary>
    Task<(string Tipo, int Id)?> ResolverAlvoAsync(string tipo, int entidadeId, CancellationToken ct);

    /// <summary>Envia para o Airtable o processo completo (critérios, propostas, itens, avaliações) e apaga lá o que já não existe na app.</summary>
    Task SincronizarProcessoAsync(int processoId, CancellationToken ct);

    Task SincronizarItemMaterialAsync(int itemMaterialId, CancellationToken ct);

    /// <summary>Põe na fila todos os processos e itens do catálogo (carga inicial / reparação).</summary>
    Task<int> EnfileirarTudoAsync(CancellationToken ct);
}

public class AirtableSyncService : IAirtableSyncService
{
    public const string TipoProcesso = "Processo";
    public const string TipoPedido = "Pedido";
    public const string TipoProposta = "Proposta";
    public const string TipoItemMaterial = "ItemMaterial";

    private readonly AppDbContext _db;
    private readonly IAirtableClient _airtable;
    private readonly IOptionsMonitor<AirtableOptions> _options;
    private readonly ILogger<AirtableSyncService> _logger;

    public AirtableSyncService(AppDbContext db, IAirtableClient airtable, IOptionsMonitor<AirtableOptions> options, ILogger<AirtableSyncService> logger)
    {
        _db = db;
        _airtable = airtable;
        _options = options;
        _logger = logger;
    }

    public async Task<(string Tipo, int Id)?> ResolverAlvoAsync(string tipo, int entidadeId, CancellationToken ct)
    {
        switch (tipo)
        {
            case TipoProcesso:
            case TipoItemMaterial:
                return (tipo, entidadeId);

            case TipoProposta:
                // Se a proposta foi apagada, a própria eliminação já pôs o processo na fila.
                var processoDaProposta = await _db.Propostas.AsNoTracking()
                    .Where(p => p.Id == entidadeId).Select(p => (int?)p.ProcessoId).FirstOrDefaultAsync(ct);
                return processoDaProposta is int pid ? (TipoProcesso, pid) : null;

            case TipoPedido:
                // Um pedido só aparece no Airtable através do processo criado a partir dele.
                var processoDoPedido = await _db.Processos.AsNoTracking()
                    .Where(p => p.PedidoPropostaId == entidadeId).Select(p => (int?)p.Id).FirstOrDefaultAsync(ct);
                return processoDoPedido is int prid ? (TipoProcesso, prid) : null;

            default:
                _logger.LogWarning("Airtable: tipo de pendente desconhecido '{Tipo}' ignorado.", tipo);
                return null;
        }
    }

    public async Task SincronizarProcessoAsync(int processoId, CancellationToken ct)
    {
        var appBaseUrl = _options.CurrentValue.AppBaseUrl;

        var processo = await _db.Processos.AsNoTracking().AsSplitQuery()
            .Include(p => p.PedidoProposta)
            .Include(p => p.PropostaVencedora)
            .Include(p => p.Criterios)
            .Include(p => p.Propostas).ThenInclude(pr => pr.ItensProposta).ThenInclude(ip => ip.ItemMaterial)
            .Include(p => p.Propostas).ThenInclude(pr => pr.Avaliacoes).ThenInclude(a => a.Avaliador)
            .Include(p => p.Propostas).ThenInclude(pr => pr.MemoriaCalculo)
            .FirstOrDefaultAsync(p => p.Id == processoId, ct);

        var linksDoProcesso = await _db.AirtableLinks
            .Where(l => l.ProcessoId == processoId)
            .ToListAsync(ct);

        // Tudo o que é enviado agora fica marcado aqui; o que sobrar em linksDoProcesso é apagado no fim.
        var enviados = new HashSet<(string Tabela, string Chave)>();

        if (processo != null)
        {
            // 1. Itens do catálogo usados nas propostas (são globais, não pertencem ao processo).
            var itensMaterial = processo.Propostas
                .SelectMany(pr => pr.ItensProposta)
                .Where(ip => ip.ItemMaterial != null)
                .Select(ip => ip.ItemMaterial!)
                .DistinctBy(im => im.Id)
                .ToList();
            var recItens = await EnviarAsync(S.ItensMaterial.Tabela,
                itensMaterial.Select(AirtableMapper.ItemMaterial).ToList(), processoId: null, enviados: null, ct);

            // 2. Processo
            var recProcessos = await EnviarAsync(S.Processos.Tabela,
                new List<RegistoAirtable> { AirtableMapper.Processo(processo, appBaseUrl) }, processoId, enviados, ct);
            var recProcesso = recProcessos.GetValueOrDefault(AirtableMapper.ChaveProcesso(processo.Id));

            // 3. Critérios (na app são por processo, com o seu próprio peso) + junção Critério do Processo
            var criterios = processo.Criterios.OrderBy(c => c.Id).ToList();
            var recCriterios = await EnviarAsync(S.Criterios.Tabela,
                criterios.Select(AirtableMapper.Criterio).ToList(), processoId, enviados, ct);
            var recCriteriosProcesso = await EnviarAsync(S.CriteriosProcesso.Tabela,
                criterios.Select(c => AirtableMapper.CriterioProcesso(c, processo.Nome, recProcesso,
                    recCriterios.GetValueOrDefault(AirtableMapper.ChaveCriterio(c.Id)))).ToList(),
                processoId, enviados, ct);

            // 4. Propostas
            var propostas = processo.Propostas.OrderBy(p => p.Id).ToList();
            var recPropostas = await EnviarAsync(S.Propostas.Tabela,
                propostas.Select(pr => AirtableMapper.Proposta(pr, recProcesso, appBaseUrl)).ToList(),
                processoId, enviados, ct);

            // 5. Itens das propostas
            var itensProposta = propostas
                .SelectMany(pr => pr.ItensProposta.OrderBy(ip => ip.Id).Select(ip => AirtableMapper.ItemProposta(ip, pr,
                    recPropostas.GetValueOrDefault(AirtableMapper.ChaveProposta(pr.Id)),
                    ip.ItemMaterialId is int imId ? recItens.GetValueOrDefault(AirtableMapper.ChaveItemMaterial(imId)) : null)))
                .ToList();
            await EnviarAsync(S.ItensProposta.Tabela, itensProposta, processoId, enviados, ct);

            // 6. Avaliações: uma por Proposta × Critério, com a nota final da app
            var avaliacoes = new List<RegistoAirtable>();
            foreach (var pr in propostas)
            {
                foreach (var c in criterios)
                {
                    var notaFinal = AirtableMapper.NotaFinal(pr, c);
                    if (notaFinal == null) continue;
                    var (nota, comentario) = notaFinal.Value;
                    var chaveC = AirtableMapper.ChaveCriterio(c.Id);
                    avaliacoes.Add(AirtableMapper.Avaliacao(pr, c, nota, comentario,
                        recPropostas.GetValueOrDefault(AirtableMapper.ChaveProposta(pr.Id)),
                        recCriterios.GetValueOrDefault(chaveC),
                        recCriteriosProcesso.GetValueOrDefault(chaveC)));
                }
            }
            await EnviarAsync(S.Avaliacoes.Tabela, avaliacoes, processoId, enviados, ct);
        }

        // 7. Apagar no Airtable o que deixou de existir na app (ou o processo inteiro, se foi apagado).
        var aApagar = linksDoProcesso.Where(l => !enviados.Contains((l.TabelaId, l.Chave))).ToList();
        foreach (var porTabela in aApagar.GroupBy(l => l.TabelaId))
        {
            await _airtable.ApagarAsync(porTabela.Key, porTabela.Select(l => l.RecordId).ToList(), ct);
        }
        if (aApagar.Count > 0)
        {
            _db.AirtableLinks.RemoveRange(aApagar);
            await _db.SaveChangesAsync(ct);
            _logger.LogInformation("Airtable: {N} registo(s) do processo {Id} apagados.", aApagar.Count, processoId);
        }
    }

    public async Task SincronizarItemMaterialAsync(int itemMaterialId, CancellationToken ct)
    {
        var item = await _db.ItensMaterial.AsNoTracking().FirstOrDefaultAsync(i => i.Id == itemMaterialId, ct);
        if (item != null)
        {
            await EnviarAsync(S.ItensMaterial.Tabela, new List<RegistoAirtable> { AirtableMapper.ItemMaterial(item) }, null, null, ct);
            return;
        }

        var chave = AirtableMapper.ChaveItemMaterial(itemMaterialId);
        var link = await _db.AirtableLinks.FirstOrDefaultAsync(l => l.TabelaId == S.ItensMaterial.Tabela && l.Chave == chave, ct);
        if (link == null) return;

        await _airtable.ApagarAsync(link.TabelaId, new[] { link.RecordId }, ct);
        _db.AirtableLinks.Remove(link);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> EnfileirarTudoAsync(CancellationToken ct)
    {
        var agora = DateTime.UtcNow;
        var processos = await _db.Processos.AsNoTracking().Select(p => p.Id).ToListAsync(ct);
        var itens = await _db.ItensMaterial.AsNoTracking().Select(i => i.Id).ToListAsync(ct);

        // Processos que já não existem mas ainda têm registos no Airtable (para os apagar lá).
        var orfaos = await _db.AirtableLinks.AsNoTracking()
            .Where(l => l.ProcessoId != null && !_db.Processos.Any(p => p.Id == l.ProcessoId))
            .Select(l => l.ProcessoId!.Value).Distinct().ToListAsync(ct);

        var pendentes = itens.Select(id => Pendente(TipoItemMaterial, id, agora))
            .Concat(processos.Concat(orfaos).Distinct().Select(id => Pendente(TipoProcesso, id, agora)))
            .ToList();

        _db.AirtableSyncPendentes.AddRange(pendentes);
        await _db.SaveChangesAsync(ct);
        return pendentes.Count;
    }

    private static AirtableSyncPendente Pendente(string tipo, int id, DateTime agora)
        => new() { Tipo = tipo, EntidadeId = id, CriadoEm = agora, ProximaTentativaEm = agora };

    /// <summary>
    /// Faz upsert dos registos e atualiza a tabela de ligações. Devolve chave → rec id.
    /// </summary>
    private async Task<IReadOnlyDictionary<string, string>> EnviarAsync(string tabelaId, List<RegistoAirtable> registos,
        int? processoId, HashSet<(string Tabela, string Chave)>? enviados, CancellationToken ct)
    {
        if (registos.Count == 0) return new Dictionary<string, string>();

        var recIds = await _airtable.UpsertAsync(tabelaId, registos, ct);

        var chaves = registos.Select(r => r.Chave).ToList();
        var existentes = await _db.AirtableLinks
            .Where(l => l.TabelaId == tabelaId && chaves.Contains(l.Chave))
            .ToDictionaryAsync(l => l.Chave, ct);

        var agora = DateTime.UtcNow;
        foreach (var r in registos)
        {
            enviados?.Add((tabelaId, r.Chave));
            if (!recIds.TryGetValue(r.Chave, out var recId)) continue;

            if (existentes.TryGetValue(r.Chave, out var link))
            {
                link.RecordId = recId;
                link.ProcessoId = processoId;
                link.AtualizadoEm = agora;
            }
            else
            {
                _db.AirtableLinks.Add(new AirtableLink
                {
                    TabelaId = tabelaId,
                    Chave = r.Chave,
                    RecordId = recId,
                    ProcessoId = processoId,
                    AtualizadoEm = agora
                });
            }
        }

        await _db.SaveChangesAsync(ct);
        return recIds;
    }
}
