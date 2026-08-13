using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
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

    public decimal CalcularNotaPonderada(Avaliacao avaliacao)
        => avaliacao.Nota * (avaliacao.Criterio.Peso / 100m);

    public decimal CalcularNotaMedia(Proposta proposta)
    {
        if (proposta.Avaliacoes.Count == 0) return 0m;
        return proposta.Avaliacoes.Average(a => a.Nota);
    }

    public decimal CalcularPontuacaoPonderada(Proposta proposta)
    {
        if (proposta.Avaliacoes.Count == 0) return 0m;
        return proposta.Avaliacoes.Sum(a => CalcularNotaPonderada(a));
    }

    public decimal? MenorValorOfertado(Processo processo)
        => processo.Propostas.Count == 0 ? null : processo.Propostas.Min(p => p.ValorTotal);

    public ComparacaoViewModel BuildComparacao(int processoId)
    {
        var processo = _db.Processos
            .Include(p => p.Criterios)
            .Include(p => p.Propostas).ThenInclude(pr => pr.Avaliacoes)
            .FirstOrDefault(p => p.Id == processoId)
            ?? throw new KeyNotFoundException($"Processo {processoId} não encontrado.");

        var vm = new ComparacaoViewModel { Processo = processo };

        vm.Propostas = MontarPropostaColunas(processo.Propostas.ToList());

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
                var avaliacao = proposta.Avaliacoes.FirstOrDefault(a => a.CriterioId == linha.CriterioId);
                linha.NotasPorProposta[proposta.Id] = avaliacao?.Nota;
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
            .Include(p => p.Propostas).ThenInclude(pr => pr.ItensProposta).ThenInclude(ip => ip.ItemMaterial)
            .Include(p => p.Propostas).ThenInclude(pr => pr.Avaliacoes).ThenInclude(a => a.Criterio)
            .FirstOrDefault(p => p.Id == processoId)
            ?? throw new KeyNotFoundException($"Processo {processoId} não encontrado.");

        var vm = new ComparacaoItensViewModel
        {
            Processo = processo,
            Propostas = MontarPropostaColunas(processo.Propostas.ToList())
        };

        var itensMateriais = processo.Propostas
            .SelectMany(p => p.ItensProposta)
            .Select(ip => ip.ItemMaterial)
            .DistinctBy(im => im.Id)
            .OrderBy(im => im.NomeItem)
            .ToList();

        foreach (var item in itensMateriais)
        {
            var linha = new LinhaItem
            {
                ItemMaterialId = item.Id,
                NomeItem = item.NomeItem,
                Unidade = item.Unidade
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

    private List<PropostaColuna> MontarPropostaColunas(List<Proposta> propostas)
    {
        var colunas = propostas
            .OrderBy(p => p.Fornecedor)
            .Select(p => new PropostaColuna
            {
                PropostaId = p.Id,
                Fornecedor = p.Fornecedor,
                ValorTotal = p.ValorTotal,
                PrazoEntregaDias = p.PrazoEntregaDias,
                PontuacaoPonderada = CalcularPontuacaoPonderada(p),
                Status = p.Status
            })
            .ToList();

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

        return colunas;
    }
}
