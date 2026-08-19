using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.ViewModels.ComparacaoObra;

namespace ComparacaoPropostas.Services;

public class ScoringObraService : IScoringObraService
{
    private readonly AppDbContext _db;

    public ScoringObraService(AppDbContext db)
    {
        _db = db;
    }

    private decimal ObterNotaMediaCriterio(PropostaEmpreiteiro proposta, int criterioObraId)
    {
        var avaliacoes = proposta.Avaliacoes.Where(a => a.CriterioObraId == criterioObraId).ToList();
        return avaliacoes.Count == 0 ? 0m : (decimal)avaliacoes.Average(a => a.Nota);
    }

    public decimal CalcularPontuacaoPonderada(PropostaEmpreiteiro proposta, IEnumerable<CriterioObra> criterios)
    {
        decimal totalPonderado = 0m;
        foreach (var c in criterios)
        {
            totalPonderado += ObterNotaMediaCriterio(proposta, c.Id) * (c.Peso / 100m);
        }
        return Math.Round(totalPonderado, 2, MidpointRounding.AwayFromZero);
    }

    public void ClonarItensMQTParaProposta(PropostaEmpreiteiro proposta, IEnumerable<ItemMQT> itensMQT)
    {
        foreach (var item in itensMQT)
        {
            proposta.ItensProposta.Add(new ItemPropostaEmpreiteiro
            {
                ItemMQTId = item.Id,
                QuantidadeFornecida = item.Quantidade,
                PrecoUnitario = 0m,
                Incluido = true
            });
        }
    }

    public ComparacaoObraViewModel BuildComparacao(int projetoObraId)
    {
        var projeto = _db.ProjetosObra.FirstOrDefault(p => p.Id == projetoObraId)
            ?? throw new KeyNotFoundException($"Projeto de Obra {projetoObraId} não encontrado.");

        var criterios = _db.CriteriosObra.Where(c => c.ProjetoObraId == projetoObraId).ToList();
        var propostas = _db.PropostasEmpreiteiro
            .Where(p => p.ProjetoObraId == projetoObraId)
            .ToList();

        foreach (var p in propostas)
        {
            p.Avaliacoes = _db.AvaliacoesObra.Where(a => a.PropostaEmpreiteiroId == p.Id).ToList();
            p.ItensProposta = _db.ItensPropostaEmpreiteiro.Where(i => i.PropostaEmpreiteiroId == p.Id).ToList();
        }

        var itensMQT = _db.ItensMQT.Where(i => i.ProjetoObraId == projetoObraId).OrderBy(i => i.CodigoIndexacao).ThenBy(i => i.Descricao).ToList();

        var vm = new ComparacaoObraViewModel { ProjetoObra = projeto };

        var colunas = propostas
            .Select(p => new PropostaEmpreiteiroColuna
            {
                PropostaId = p.Id,
                Empreiteiro = p.Empreiteiro,
                ValorTotal = p.Subtotal,
                PrazoEntregaDias = p.PrazoEntregaDias,
                PontuacaoPonderada = CalcularPontuacaoPonderada(p, criterios),
                Status = p.Status
            })
            .OrderByDescending(p => p.PontuacaoPonderada)
            .ThenBy(p => p.ValorTotal)
            .ToList();

        for (var i = 0; i < colunas.Count; i++) colunas[i].PosicaoRanking = i + 1;

        if (colunas.Count > 0)
        {
            var menorValor = colunas.Min(p => p.ValorTotal);
            foreach (var col in colunas) col.ValorTotalMelhor = col.ValorTotal == menorValor;

            var comPrazo = colunas.Where(p => p.PrazoEntregaDias.HasValue).ToList();
            if (comPrazo.Count > 0)
            {
                var menorPrazo = comPrazo.Min(p => p.PrazoEntregaDias!.Value);
                foreach (var col in comPrazo) col.PrazoMelhor = col.PrazoEntregaDias == menorPrazo;
            }

            var maiorPontuacao = colunas.Max(p => p.PontuacaoPonderada);
            foreach (var col in colunas) col.PontuacaoMelhor = col.PontuacaoPonderada == maiorPontuacao;
        }

        vm.Propostas = colunas;

        vm.LinhasCriterios = criterios
            .OrderByDescending(c => c.Peso)
            .Select(c => new LinhaCriterioObra
            {
                CriterioObraId = c.Id,
                CriterioNome = c.Nome,
                Peso = c.Peso
            })
            .ToList();

        foreach (var linha in vm.LinhasCriterios)
        {
            foreach (var proposta in propostas)
            {
                var avaliacoes = proposta.Avaliacoes.Where(a => a.CriterioObraId == linha.CriterioObraId).ToList();
                linha.NotasPorProposta[proposta.Id] = avaliacoes.Count > 0 ? (decimal)avaliacoes.Average(a => a.Nota) : null;
            }

            var notasPresentes = linha.NotasPorProposta.Values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
            if (notasPresentes.Count > 0)
            {
                var maiorNota = notasPresentes.Max();
                foreach (var (propostaId, nota) in linha.NotasPorProposta)
                    linha.MelhorPorProposta[propostaId] = nota.HasValue && nota.Value == maiorNota;
            }
        }

        foreach (var item in itensMQT)
        {
            var linha = new LinhaItemMQT
            {
                ItemMQTId = item.Id,
                Descricao = item.Descricao,
                Unidade = item.Unidade,
                QuantidadeSolicitada = item.Quantidade,
                NaoPrevisto = item.NaoPrevisto
            };

            foreach (var proposta in propostas)
            {
                var itemProposta = proposta.ItensProposta.FirstOrDefault(i => i.ItemMQTId == item.Id);
                // Preço 0 significa "ainda não cotado" (valor por omissão ao clonar os itens do
                // MQT para a proposta), não uma oferta genuína a custo zero.
                var incluido = itemProposta != null && itemProposta.Incluido && itemProposta.PrecoUnitario > 0;

                linha.IncluidoPorProposta[proposta.Id] = incluido;
                linha.QuantidadePorProposta[proposta.Id] = incluido ? itemProposta!.QuantidadeFornecida : null;
                linha.PrecoPorProposta[proposta.Id] = incluido ? itemProposta!.PrecoUnitario : null;
                linha.SubtotalPorProposta[proposta.Id] = incluido ? itemProposta!.Subtotal : null;
            }

            var subtotaisPresentes = linha.SubtotalPorProposta.Values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
            if (subtotaisPresentes.Count > 0)
            {
                var menorSubtotal = subtotaisPresentes.Min();
                foreach (var (propostaId, subtotal) in linha.SubtotalPorProposta)
                    linha.MelhorPorProposta[propostaId] = subtotal.HasValue && subtotal.Value == menorSubtotal;
            }

            vm.LinhasItens.Add(linha);
        }

        return vm;
    }
}
