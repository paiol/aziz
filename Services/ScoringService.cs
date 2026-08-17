using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Helper;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;
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
        decimal totalPonderado = 0m;
        foreach (var c in criterios)
        {
            var nota = c.TipoAutomatico != TipoCriterioAutomatico.Nenhum
                ? proposta.MemoriaCalculo.FirstOrDefault(m => m.CriterioId == c.Id)?.Nota ?? 0m
                : ObterNotaMediaCriterio(proposta, c.Id);

            totalPonderado += nota * (c.Peso / 100m);
        }

        return Math.Round(totalPonderado, 2, MidpointRounding.AwayFromZero);
    }

    public decimal? MenorValorOfertado(Processo processo)
        => processo.Propostas.Count == 0 ? null : processo.Propostas.Min(p => p.ValorTotalCVE);

    public ComparacaoViewModel BuildComparacao(int processoId)
    {
        var processo = _db.Processos
            .Include(p => p.Criterios)
            .Include(p => p.PedidoProposta).ThenInclude(pp => pp.ItensPedido)
            .Include(p => p.Propostas).ThenInclude(pr => pr.Avaliacoes).ThenInclude(a => a.Avaliador)
            .Include(p => p.Propostas).ThenInclude(pr => pr.ItensProposta)
            .Include(p => p.Propostas).ThenInclude(pr => pr.MemoriaCalculo)
            .FirstOrDefault(p => p.Id == processoId)
            ?? throw new KeyNotFoundException($"Processo {processoId} não encontrado.");

        AtualizarAvaliacaoAutomatica(processo);

        var vm = new ComparacaoViewModel { Processo = processo };

        vm.Propostas = MontarPropostaColunas(processo.Propostas.ToList(), processo.Criterios.ToList(), processo.PedidoProposta.PrazoEntrega);

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
            var criterio = processo.Criterios.First(c => c.Id == linha.CriterioId);

            foreach (var proposta in processo.Propostas)
            {
                if (criterio.TipoAutomatico != TipoCriterioAutomatico.Nenhum)
                {
                    var memoria = proposta.MemoriaCalculo.FirstOrDefault(m => m.CriterioId == linha.CriterioId);
                    linha.NotasPorProposta[proposta.Id] = memoria?.Nota;
                    continue;
                }

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
            .Include(p => p.Propostas).ThenInclude(pr => pr.MemoriaCalculo)
            .Include(p => p.PedidoProposta).ThenInclude(pp => pp.ItensPedido)
            .FirstOrDefault(p => p.Id == processoId)
            ?? throw new KeyNotFoundException($"Processo {processoId} não encontrado.");

        AtualizarAvaliacaoAutomatica(processo);

        var vm = new ComparacaoItensViewModel
        {
            Processo = processo,
            Propostas = MontarPropostaColunas(processo.Propostas.ToList(), processo.Criterios.ToList(), processo.PedidoProposta.PrazoEntrega)
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

    public void AtualizarAvaliacaoAutomatica(Processo processo)
    {
        var criteriosAutomaticos = processo.Criterios.Where(c => c.TipoAutomatico != TipoCriterioAutomatico.Nenhum).ToList();
        if (criteriosAutomaticos.Count == 0) return;

        var propostas = processo.Propostas.ToList();
        if (propostas.Count == 0) return;

        foreach (var criterio in criteriosAutomaticos)
        {
            switch (criterio.TipoAutomatico)
            {
                case TipoCriterioAutomatico.Preco:
                    CalcularPreco(criterio, propostas);
                    break;
                case TipoCriterioAutomatico.Prazo:
                    CalcularPrazo(criterio, propostas);
                    break;
                case TipoCriterioAutomatico.Garantia:
                    CalcularGarantia(criterio, propostas);
                    break;
                case TipoCriterioAutomatico.Tecnico:
                    CalcularTecnico(processo, criterio, propostas);
                    break;
            }
        }

        _db.SaveChanges();
    }

    private void UpsertMemoria(Proposta proposta, Criterio criterio, decimal nota, string justificativa)
    {
        var existente = proposta.MemoriaCalculo.FirstOrDefault(m => m.CriterioId == criterio.Id)
            ?? _db.MemoriaCalculoAvaliacao.FirstOrDefault(m => m.PropostaId == proposta.Id && m.CriterioId == criterio.Id);

        if (existente != null)
        {
            existente.Nota = nota;
            existente.Justificativa = justificativa;
            existente.CalculadoEm = DateTime.UtcNow;
        }
        else
        {
            existente = new MemoriaCalculoAvaliacao
            {
                PropostaId = proposta.Id,
                CriterioId = criterio.Id,
                Nota = nota,
                Justificativa = justificativa,
                CalculadoEm = DateTime.UtcNow
            };
            _db.MemoriaCalculoAvaliacao.Add(existente);
            proposta.MemoriaCalculo.Add(existente);
        }
    }

    private void CalcularPreco(Criterio criterio, List<Proposta> propostas)
    {
        foreach (var proposta in propostas)
        {
            var (nota, justificativa) = CalcularNotaPreco(proposta, propostas);
            UpsertMemoria(proposta, criterio, nota, justificativa);
        }
    }

    // Pontuação do preço = menor preço do item entre todos os fornecedores / preço do fornecedor × 5,
    // calculada item a item e agregada pela média entre os itens comparáveis.
    internal (decimal Nota, string Justificativa) CalcularNotaPreco(Proposta proposta, List<Proposta> todasPropostas)
    {
        var itensIds = todasPropostas
            .SelectMany(p => p.ItensProposta.Where(i => i.Incluido))
            .Select(i => i.ItemMaterialId)
            .Distinct()
            .ToList();

        var scoresItens = new List<decimal>();
        var melhores = 0;
        var totalComparaveis = 0;

        foreach (var itemId in itensIds)
        {
            var precosItem = todasPropostas
                .Select(p => p.ItensProposta.FirstOrDefault(i => i.ItemMaterialId == itemId && i.Incluido))
                .Where(i => i != null && i.PrecoUnitario > 0)
                .Select(i => i!.PrecoUnitario)
                .ToList();

            if (precosItem.Count == 0) continue;

            var itemDestaProposta = proposta.ItensProposta.FirstOrDefault(i => i.ItemMaterialId == itemId && i.Incluido);
            if (itemDestaProposta == null || itemDestaProposta.PrecoUnitario <= 0) continue;

            totalComparaveis++;
            var menorPreco = precosItem.Min();
            scoresItens.Add(Math.Min(5m, menorPreco / itemDestaProposta.PrecoUnitario * 5m));
            if (itemDestaProposta.PrecoUnitario == menorPreco) melhores++;
        }

        var nota = scoresItens.Count > 0 ? Math.Round(scoresItens.Average(), 2, MidpointRounding.AwayFromZero) : 0m;
        var justificativa = totalComparaveis > 0
            ? $"Melhor preço em {melhores} de {totalComparaveis} item(ns) comparado(s)."
            : "Sem itens com preço para comparar.";

        return (nota, justificativa);
    }

    private void CalcularPrazo(Criterio criterio, List<Proposta> propostas)
    {
        foreach (var proposta in propostas)
        {
            var (nota, justificativa) = CalcularNotaPrazo(proposta, propostas);
            UpsertMemoria(proposta, criterio, nota, justificativa);
        }
    }

    // Pontuação do prazo = menor prazo entre os fornecedores / prazo do fornecedor × 5.
    internal (decimal Nota, string Justificativa) CalcularNotaPrazo(Proposta proposta, List<Proposta> todasPropostas)
    {
        var comPrazo = todasPropostas.Where(p => p.PrazoEntregaDias is > 0).ToList();
        var menorPrazo = comPrazo.Count > 0 ? comPrazo.Min(p => p.PrazoEntregaDias!.Value) : (int?)null;

        if (!menorPrazo.HasValue || proposta.PrazoEntregaDias is not > 0)
        {
            return (0m, "Prazo de entrega não informado.");
        }

        var nota = Math.Round(Math.Min(5m, (decimal)menorPrazo.Value / proposta.PrazoEntregaDias!.Value * 5m), 2, MidpointRounding.AwayFromZero);
        var justificativa = proposta.PrazoEntregaDias.Value == menorPrazo.Value
            ? $"Menor prazo entre os fornecedores ({proposta.PrazoEntregaDias.Value} dias)."
            : $"Prazo de {proposta.PrazoEntregaDias.Value} dias (o melhor é {menorPrazo.Value} dias).";

        return (nota, justificativa);
    }

    private void CalcularGarantia(Criterio criterio, List<Proposta> propostas)
    {
        foreach (var proposta in propostas)
        {
            var (nota, justificativa) = CalcularNotaGarantia(proposta, propostas);
            UpsertMemoria(proposta, criterio, nota, justificativa);
        }
    }

    // Pontuação da garantia = garantia do fornecedor / maior garantia entre os fornecedores × 5.
    // A garantia é texto livre (ex: "24 meses", "2 anos") — GarantiaHelper converte para meses.
    internal (decimal Nota, string Justificativa) CalcularNotaGarantia(Proposta proposta, List<Proposta> todasPropostas)
    {
        var mesesPorProposta = todasPropostas.ToDictionary(p => p.Id, p => GarantiaHelper.ParseParaMeses(p.Garantia));
        var comGarantia = mesesPorProposta.Values.Where(m => m is > 0).Select(m => m!.Value).ToList();
        var maiorGarantia = comGarantia.Count > 0 ? comGarantia.Max() : (int?)null;

        var meses = GarantiaHelper.ParseParaMeses(proposta.Garantia);
        if (!maiorGarantia.HasValue || meses is not > 0)
        {
            return (0m, "Garantia não informada ou não reconhecida.");
        }

        var nota = Math.Round(Math.Min(5m, (decimal)meses.Value / maiorGarantia.Value * 5m), 2, MidpointRounding.AwayFromZero);
        var justificativa = meses.Value == maiorGarantia.Value
            ? $"Garantia de {meses.Value} meses (a maior entre os fornecedores)."
            : $"Garantia de {meses.Value} meses (a maior é {maiorGarantia.Value} meses).";

        return (nota, justificativa);
    }

    private void CalcularTecnico(Processo processo, Criterio criterio, List<Proposta> propostas)
    {
        foreach (var proposta in propostas)
        {
            var (nota, justificativa) = CalcularNotaTecnico(processo, proposta);
            UpsertMemoria(proposta, criterio, nota, justificativa);
        }
    }

    // Técnico = combinação de "itens apresentados" (proporção de itens do pedido incluídos na
    // proposta) e "quantidades atendidas" (proporção da quantidade solicitada efetivamente
    // fornecida, limitada a 100%). Os pesos relativos (25%/30%) vêm da especificação e são
    // normalizados a 100% entre estes dois subcritérios, já que "Especificações técnicas" e
    // "Conformidade geral" (35%+10% na especificação) não têm dados estruturados para comparar
    // automaticamente hoje.
    internal (decimal Nota, string Justificativa) CalcularNotaTecnico(Processo processo, Proposta proposta)
    {
        var itensPedido = processo.PedidoProposta?.ItensPedido?.ToList() ?? new List<ItemPedido>();

        if (itensPedido.Count == 0)
        {
            return (0m, "Sem itens no pedido para comparar.");
        }

        var itensApresentados = 0;
        var somaAtendimento = 0m;

        foreach (var itemPedido in itensPedido)
        {
            var itemProposta = proposta.ItensProposta.FirstOrDefault(i => i.ItemMaterialId == itemPedido.ItemMaterialId && i.Incluido);
            if (itemProposta == null) continue;

            itensApresentados++;
            var solicitada = itemPedido.QuantidadeSolicitada;
            somaAtendimento += solicitada > 0 ? Math.Min(1m, itemProposta.Quantidade / solicitada) : 1m;
        }

        var pctItens = (decimal)itensApresentados / itensPedido.Count;
        var pctQuantidades = somaAtendimento / itensPedido.Count;

        var nota = Math.Round((pctItens * (25m / 55m) + pctQuantidades * (30m / 55m)) * 5m, 2, MidpointRounding.AwayFromZero);
        var justificativa = $"{Math.Round(pctItens * 100, 0)}% dos itens apresentados, {Math.Round(pctQuantidades * 100, 0)}% das quantidades atendidas.";

        return (nota, justificativa);
    }

    private List<PropostaColuna> MontarPropostaColunas(List<Proposta> propostas, List<Criterio> criterios, DateTime? prazoSolicitado = null)
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

            if (prazoSolicitado.HasValue)
            {
                var diasDisponiveis = (prazoSolicitado.Value.Date - DateTime.Today).Days;
                foreach (var col in comPrazo)
                {
                    col.PrazoDentroDoSolicitado = col.PrazoEntregaDias!.Value <= diasDisponiveis;
                    col.PrazoDiasDeAtraso = col.PrazoEntregaDias.Value - diasDisponiveis;
                }
            }

            var maiorPontuacao = colunas.Max(p => p.PontuacaoPonderada);
            foreach (var col in colunas) col.PontuacaoMelhor = col.PontuacaoPonderada == maiorPontuacao;
        }

        return colunas;
    }
}
