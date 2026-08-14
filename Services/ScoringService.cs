using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Helper;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.ViewModels.Comparacao;

namespace ComparacaoPropostas.Services;

public class ScoringService : IScoringService
{
    private readonly AppDbContext _db;

    public ScoringService(AppDbContext db)
    {
        _db = db;
    }

    public decimal ObterNotaMediaCriterio(Proposta proposta, int criterioId)
    {
        var avaliacoesCriterio = proposta.Avaliacoes.Where(a => a.CriterioId == criterioId).ToList();
        if (avaliacoesCriterio.Count == 0) return 0m;
        return (decimal)avaliacoesCriterio.Average(a => a.Nota);
    }

    public decimal CalcularNotaMedia(Proposta proposta)
    {
        if (proposta.Avaliacoes.Count == 0) return 0m;
        return (decimal)proposta.Avaliacoes.Average(a => a.Nota);
    }

    public decimal CalcularPontuacaoPonderada(Proposta proposta)
    {
        var criterios = proposta.Processo?.Criterios ?? (ICollection<Criterio>)new List<Criterio>();
        if (criterios.Count == 0 && proposta.ProcessoId > 0)
        {
            criterios = _db.Criterios.Where(c => c.ProcessoId == proposta.ProcessoId).ToList();
        }
        return CalcularPontuacaoPonderada(proposta, criterios);
    }

    public decimal CalcularPontuacaoPonderada(Proposta proposta, IEnumerable<Criterio> criterios)
    {
        if (proposta.Avaliacoes.Count == 0) return 0m;

        decimal totalPonderado = 0m;
        foreach (var c in criterios)
        {
            var notaMedia = ObterNotaMediaCriterio(proposta, c.Id);
            totalPonderado += notaMedia * (c.Peso / 100m);
        }

        return Math.Round(totalPonderado, 2, MidpointRounding.AwayFromZero);
    }

    public decimal? MenorValorOfertado(Processo processo)
        => processo.Propostas.Count == 0 ? null : processo.Propostas.Min(p => p.ValorTotalCVE);

    public ComparacaoViewModel BuildComparacao(int processoId)
    {
        var processo = _db.Processos
            .Include(p => p.Criterios)
            .Include(p => p.Propostas).ThenInclude(pr => pr.Avaliacoes).ThenInclude(a => a.Avaliador)
            .FirstOrDefault(p => p.Id == processoId)
            ?? throw new KeyNotFoundException($"Processo {processoId} não encontrado.");

        var vm = new ComparacaoViewModel { Processo = processo };

        vm.Propostas = MontarPropostaColunas(processo.Propostas.ToList(), processo.Criterios.ToList());

        vm.LinhasCriterios = processo.Criterios
            .OrderByDescending(c => c.Peso)
            .Select(c => new LinhaCriterio
            {
                CriterioId = c.Id,
                CriterioNome = c.Nome,
                Peso = c.Peso
            })
            .ToList();

        foreach (var linha in vm.LinhasCriterios)
        {
            foreach (var proposta in processo.Propostas)
            {
                var avaliacoes = proposta.Avaliacoes.Where(a => a.CriterioId == linha.CriterioId).ToList();
                if (avaliacoes.Count > 0)
                {
                    linha.NotasPorProposta[proposta.Id] = (decimal)avaliacoes.Average(a => a.Nota);
                }
                else
                {
                    linha.NotasPorProposta[proposta.Id] = null;
                }
            }

            var notasPresentes = linha.NotasPorProposta.Values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
            if (notasPresentes.Count > 0)
            {
                var maiorNota = notasPresentes.Max();
                foreach (var (propostaId, nota) in linha.NotasPorProposta)
                    linha.MelhorPorProposta[propostaId] = nota.HasValue && nota.Value == maiorNota;
            }
        }

        return vm;
    }

    public ComparacaoItensViewModel BuildComparacaoItens(int processoId)
    {
        var processo = _db.Processos
            .Include(p => p.Criterios)
            .Include(p => p.Propostas).ThenInclude(pr => pr.ItensProposta).ThenInclude(ip => ip.ItemMaterial)
            .Include(p => p.Propostas).ThenInclude(pr => pr.Avaliacoes)
            .Include(p => p.PedidoProposta).ThenInclude(pp => pp.ItensPedido)
            .FirstOrDefault(p => p.Id == processoId)
            ?? throw new KeyNotFoundException($"Processo {processoId} não encontrado.");

        var vm = new ComparacaoItensViewModel
        {
            Processo = processo,
            Propostas = MontarPropostaColunas(processo.Propostas.ToList(), processo.Criterios.ToList())
        };

        var itensMateriais = processo.Propostas
            .SelectMany(p => p.ItensProposta)
            .Select(ip => ip.ItemMaterial)
            .DistinctBy(im => im.Id)
            .OrderBy(im => im.NomeItem)
            .ToList();

        foreach (var item in itensMateriais)
        {
            var itemPedido = processo.PedidoProposta.ItensPedido.FirstOrDefault(ip => ip.ItemMaterialId == item.Id);

            var linha = new LinhaItem
            {
                ItemMaterialId = item.Id,
                NomeItem = item.NomeItem,
                Unidade = item.Unidade,
                QuantidadeSolicitada = itemPedido?.QuantidadeSolicitada
            };

            foreach (var proposta in processo.Propostas)
            {
                var itemProposta = proposta.ItensProposta.FirstOrDefault(ip => ip.ItemMaterialId == item.Id);
                var incluido = itemProposta?.Incluido ?? false;

                linha.IncluidoPorProposta[proposta.Id] = incluido;
                linha.QuantidadePorProposta[proposta.Id] = incluido ? itemProposta!.Quantidade : null;
                linha.PrecoPorProposta[proposta.Id] = incluido ? itemProposta!.PrecoUnitario : null;
                linha.SubtotalPorProposta[proposta.Id] = incluido ? itemProposta!.Subtotal : null;
            }

            var subtotaisPresentes = linha.SubtotalPorProposta.Values.Where(v => v.HasValue).Select(v => v!.Value).ToList();
            if (subtotaisPresentes.Count > 0)
            {
                var menorSubtotal = subtotaisPresentes.Min();
                foreach (var (propostaId, subtotal) in linha.SubtotalPorProposta)
                {
                    linha.MelhorPorProposta[propostaId] = subtotal.HasValue && subtotal.Value == menorSubtotal;
                    linha.DiferencaPorProposta[propostaId] = subtotal.HasValue ? subtotal.Value - menorSubtotal : null;
                }
            }

            vm.Linhas.Add(linha);
        }

        return vm;
    }

    public void ClonarItensPedidoParaProposta(Proposta proposta, IEnumerable<ItemPedido> itensPedido)
    {
        foreach (var ip in itensPedido)
        {
            proposta.ItensProposta.Add(new ItemProposta
            {
                ItemMaterialId = ip.ItemMaterialId,
                QuantidadeSolicitada = ip.QuantidadeSolicitada,
                Quantidade = ip.QuantidadeSolicitada,
                PrecoUnitario = 0m,
                Incluido = true,
                Observacao = null
            });
        }
    }

    private List<PropostaColuna> MontarPropostaColunas(List<Proposta> propostas, List<Criterio> criterios)
    {
        var colunas = propostas
            .Select(p => new PropostaColuna
            {
                PropostaId = p.Id,
                Fornecedor = p.Fornecedor,
                Moeda = p.Moeda,
                TaxaCambio = p.TaxaCambio,
                ValorTotal = p.ValorTotal,
                ValorTotalCVE = p.ValorTotalCVE,
                ValorTotalEUR = p.ValorTotalEUR,
                PrazoEntregaDias = p.PrazoEntregaDias,
                Garantia = p.Garantia,
                PontuacaoPonderada = CalcularPontuacaoPonderada(p, criterios),
                Status = p.Status
            })
            .OrderByDescending(p => p.PontuacaoPonderada)
            .ThenBy(p => p.ValorTotalCVE)
            .ToList();

        // Atribuir posição no ranking
        for (var i = 0; i < colunas.Count; i++)
        {
            colunas[i].PosicaoRanking = i + 1;
        }

        if (colunas.Count > 0)
        {
            var menorValorCve = colunas.Min(p => p.ValorTotalCVE);
            foreach (var col in colunas) col.ValorTotalMelhor = col.ValorTotalCVE == menorValorCve;

            var comPrazo = colunas.Where(p => p.PrazoEntregaDias.HasValue).ToList();
            if (comPrazo.Count > 0)
            {
                var menorPrazo = comPrazo.Min(p => p.PrazoEntregaDias!.Value);
                foreach (var col in comPrazo) col.PrazoMelhor = col.PrazoEntregaDias == menorPrazo;
            }

            var maiorPontuacao = colunas.Max(p => p.PontuacaoPonderada);
            foreach (var col in colunas) col.PontuacaoMelhor = col.PontuacaoPonderada == maiorPontuacao;
        }

        return colunas;
    }
}
