using System.Collections.Generic;
using System.Linq;
using Xunit;
using ComparacaoPropostas.Helper;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Services;

namespace ComparacaoPropostas.Tests;

public class DomainAndBusinessLogicTests
{
    [Fact]
    public void MoedaHelper_ConverteCorretamente_EurParaCve()
    {
        var valorEur = 1000m;
        var taxa = 110.265m;

        var totalCve = MoedaHelper.CalcularTotalCve(valorEur, "EUR", taxa);
        Assert.Equal(110265.00m, totalCve);

        var totalCveDireto = MoedaHelper.CalcularTotalCve(50000m, "CVE", taxa);
        Assert.Equal(50000m, totalCveDireto);
    }

    [Fact]
    public void MoedaHelper_ConverteCorretamente_CveParaEur()
    {
        var valorCve = 110265.00m;
        var taxa = 110.265m;

        var totalEur = MoedaHelper.CalcularTotalEur(valorCve, "CVE", taxa);
        Assert.Equal(1000.00m, totalEur);
    }

    [Fact]
    public void ScoringService_CalculaMediaNotas_MultiplosAvaliadores()
    {
        var scoring = new ScoringService(null!);

        var avaliador1 = new Avaliador { Id = 1, Nome = "Avaliador 1" };
        var avaliador2 = new Avaliador { Id = 2, Nome = "Avaliador 2" };

        var criterio = new Criterio { Id = 10, Nome = "Preço", Peso = 50m };

        var proposta = new Proposta
        {
            Id = 1,
            Fornecedor = "Fornecedor A",
            Avaliacoes = new List<Avaliacao>
            {
                new Avaliacao { CriterioId = 10, AvaliadorId = 1, Nota = 4 },
                new Avaliacao { CriterioId = 10, AvaliadorId = 2, Nota = 5 }
            }
        };

        var media = scoring.ObterNotaMediaCriterio(proposta, 10);
        Assert.Equal(4.5m, media);
    }

    [Fact]
    public void ScoringService_CalculaPontuacaoPonderada_Escala0a5()
    {
        var scoring = new ScoringService(null!);

        var criterioPreco = new Criterio { Id = 1, Nome = "Preço", Peso = 60m };
        var criterioQualidade = new Criterio { Id = 2, Nome = "Qualidade Técnica", Peso = 40m };
        var criterios = new List<Criterio> { criterioPreco, criterioQualidade };

        var proposta = new Proposta
        {
            Id = 1,
            Fornecedor = "Fornecedor Teste",
            Avaliacoes = new List<Avaliacao>
            {
                // Preço: Avaliador 1 deu 4, Avaliador 2 deu 4 -> média 4.0
                new Avaliacao { CriterioId = 1, AvaliadorId = 1, Nota = 4 },
                new Avaliacao { CriterioId = 1, AvaliadorId = 2, Nota = 4 },
                // Qualidade: Avaliador 1 deu 5, Avaliador 2 deu 3 -> média 4.0
                new Avaliacao { CriterioId = 2, AvaliadorId = 1, Nota = 5 },
                new Avaliacao { CriterioId = 2, AvaliadorId = 2, Nota = 3 }
            }
        };

        // Pontuação esperada: (4.0 * 60 / 100) + (4.0 * 40 / 100) = 2.4 + 1.6 = 4.00
        var pontuacao = scoring.CalcularPontuacaoPonderada(proposta, criterios);
        Assert.Equal(4.00m, pontuacao);
    }

    [Fact]
    public void ScoringService_ClonarItensPedidoParaProposta_CopiaItensCorretamente()
    {
        var scoring = new ScoringService(null!);

        var itensPedido = new List<ItemPedido>
        {
            new ItemPedido { ItemMaterialId = 101, QuantidadeSolicitada = 5, Observacao = "Switch 24p" },
            new ItemPedido { ItemMaterialId = 102, QuantidadeSolicitada = 10, Observacao = "Patch Cords" }
        };

        var proposta = new Proposta { Id = 1, Fornecedor = "Fornecedor B" };

        scoring.ClonarItensPedidoParaProposta(proposta, itensPedido);

        Assert.Equal(2, proposta.ItensProposta.Count);
        Assert.Contains(proposta.ItensProposta, i => i.ItemMaterialId == 101 && i.Quantidade == 5 && i.Incluido);
        Assert.Contains(proposta.ItensProposta, i => i.ItemMaterialId == 102 && i.Quantidade == 10 && i.Incluido);
    }

    [Fact]
    public void EmailService_BuildCorpoDecisao_IncluiTodosOsFornecedoresERanking()
    {
        var scoring = new ScoringService(null!);
        var emailService = new EmailService(
            Microsoft.Extensions.Options.Options.Create(new SmtpSettings()),
            scoring,
            new Microsoft.Extensions.Logging.Abstractions.NullLogger<EmailService>());

        var criterio = new Criterio { Id = 1, Nome = "Preço", Peso = 100m };
        var criterios = new List<Criterio> { criterio };

        var propostaA = new Proposta
        {
            Id = 1,
            Fornecedor = "Fornecedor A",
            Moeda = "CVE",
            ValorTotal = 165000m,
            PrazoEntregaDias = 15,
            Garantia = "2 anos",
            Avaliacoes = new List<Avaliacao> { new Avaliacao { CriterioId = 1, AvaliadorId = 1, Nota = 5 } }
        };
        var propostaB = new Proposta
        {
            Id = 2,
            Fornecedor = "Fornecedor B",
            Moeda = "CVE",
            ValorTotal = 156000m,
            PrazoEntregaDias = 20,
            Garantia = "3 anos",
            Avaliacoes = new List<Avaliacao> { new Avaliacao { CriterioId = 1, AvaliadorId = 1, Nota = 3 } }
        };

        var processo = new Processo
        {
            Id = 42,
            NumeroProcesso = "2026-0001",
            Nome = "Aquisição de Torres e Acessórios",
            PedidoProposta = new PedidoProposta { TipoProposta = "Compra", Area = AreaDepartamento.DepPrm },
            Criterios = criterios,
            Propostas = new List<Proposta> { propostaA, propostaB },
            PropostaVencedora = propostaA,
            PropostaVencedoraId = propostaA.Id
        };

        var html = emailService.BuildCorpoDecisao(processo);

        Assert.Contains("2026-0001", html);
        Assert.Contains("Pedido de Aquisição Associado", html);
        Assert.Contains("Fornecedor A", html);
        Assert.Contains("Fornecedor B", html);
        Assert.Contains("Ranking Final", html);
        Assert.Contains("1º", html);
        Assert.Contains("2º", html);
    }

    [Theory]
    [InlineData("24 meses", 24)]
    [InlineData("2 anos", 24)]
    [InlineData("12", 12)]
    [InlineData("1 ano", 12)]
    [InlineData("", null)]
    [InlineData(null, null)]
    [InlineData("garantia total", null)]
    public void GarantiaHelper_ParseParaMeses_InterpretaTextoLivre(string? texto, int? esperado)
    {
        Assert.Equal(esperado, GarantiaHelper.ParseParaMeses(texto));
    }

    [Fact]
    public void ScoringService_CalcularNotaPreco_MelhorPrecoRecebeNotaMaxima()
    {
        var scoring = new ScoringService(null!);
        var item = new ItemMaterial { Id = 1, NomeItem = "Cabo UTP" };

        var propostaCara = new Proposta
        {
            Id = 1,
            Fornecedor = "Fornecedor A",
            ItensProposta = new List<ItemProposta>
            {
                new ItemProposta { ItemMaterialId = 1, ItemMaterial = item, Incluido = true, Quantidade = 10, PrecoUnitario = 100m }
            }
        };
        var propostaBarata = new Proposta
        {
            Id = 2,
            Fornecedor = "Fornecedor B",
            ItensProposta = new List<ItemProposta>
            {
                new ItemProposta { ItemMaterialId = 1, ItemMaterial = item, Incluido = true, Quantidade = 10, PrecoUnitario = 50m }
            }
        };
        var todas = new List<Proposta> { propostaCara, propostaBarata };

        var (notaBarata, _) = scoring.CalcularNotaPreco(propostaBarata, todas);
        var (notaCara, _) = scoring.CalcularNotaPreco(propostaCara, todas);

        Assert.Equal(5.00m, notaBarata);
        Assert.Equal(2.50m, notaCara);
    }

    [Fact]
    public void ScoringService_CalcularNotaPrazo_MenorPrazoRecebeNotaMaxima()
    {
        var scoring = new ScoringService(null!);
        var propostaRapida = new Proposta { Id = 1, Fornecedor = "A", PrazoEntregaDias = 10 };
        var propostaLenta = new Proposta { Id = 2, Fornecedor = "B", PrazoEntregaDias = 20 };
        var todas = new List<Proposta> { propostaRapida, propostaLenta };

        var (notaRapida, justRapida) = scoring.CalcularNotaPrazo(propostaRapida, todas);
        var (notaLenta, _) = scoring.CalcularNotaPrazo(propostaLenta, todas);

        Assert.Equal(5.00m, notaRapida);
        Assert.Equal(2.50m, notaLenta);
        Assert.Contains("Menor prazo", justRapida);
    }

    [Fact]
    public void ScoringService_CalcularNotaGarantia_MaiorGarantiaRecebeNotaMaxima()
    {
        var scoring = new ScoringService(null!);
        var propostaCurta = new Proposta { Id = 1, Fornecedor = "A", Garantia = "12 meses" };
        var propostaLonga = new Proposta { Id = 2, Fornecedor = "B", Garantia = "24 meses" };
        var todas = new List<Proposta> { propostaCurta, propostaLonga };

        var (notaLonga, _) = scoring.CalcularNotaGarantia(propostaLonga, todas);
        var (notaCurta, _) = scoring.CalcularNotaGarantia(propostaCurta, todas);

        Assert.Equal(5.00m, notaLonga);
        Assert.Equal(2.50m, notaCurta);
    }

    [Fact]
    public void ScoringService_CalcularNotaTecnico_TodosItensEQuantidadesAtendidos_NotaMaxima()
    {
        var scoring = new ScoringService(null!);
        var item = new ItemMaterial { Id = 1, NomeItem = "Switch 24p" };

        var pedido = new PedidoProposta
        {
            Id = 1,
            ItensPedido = new List<ItemPedido>
            {
                new ItemPedido { ItemMaterialId = 1, ItemMaterial = item, QuantidadeSolicitada = 10 }
            }
        };
        var processo = new Processo { Id = 1, PedidoProposta = pedido };

        var proposta = new Proposta
        {
            Id = 1,
            Fornecedor = "A",
            ItensProposta = new List<ItemProposta>
            {
                new ItemProposta { ItemMaterialId = 1, ItemMaterial = item, Incluido = true, Quantidade = 10, PrecoUnitario = 5m }
            }
        };

        var (nota, justificativa) = scoring.CalcularNotaTecnico(processo, proposta);

        Assert.Equal(5.00m, nota);
        Assert.Contains("100%", justificativa);
    }

    [Fact]
    public void ScoringService_CalcularNotaTecnico_ItemEmFalta_NotaReduzida()
    {
        var scoring = new ScoringService(null!);
        var item1 = new ItemMaterial { Id = 1, NomeItem = "Switch 24p" };
        var item2 = new ItemMaterial { Id = 2, NomeItem = "Patch Cord" };

        var pedido = new PedidoProposta
        {
            Id = 1,
            ItensPedido = new List<ItemPedido>
            {
                new ItemPedido { ItemMaterialId = 1, ItemMaterial = item1, QuantidadeSolicitada = 10 },
                new ItemPedido { ItemMaterialId = 2, ItemMaterial = item2, QuantidadeSolicitada = 50 }
            }
        };
        var processo = new Processo { Id = 1, PedidoProposta = pedido };

        // Só apresenta o item 1, na quantidade solicitada; item 2 fica em falta.
        var proposta = new Proposta
        {
            Id = 1,
            Fornecedor = "A",
            ItensProposta = new List<ItemProposta>
            {
                new ItemProposta { ItemMaterialId = 1, ItemMaterial = item1, Incluido = true, Quantidade = 10, PrecoUnitario = 5m }
            }
        };

        var (nota, _) = scoring.CalcularNotaTecnico(processo, proposta);

        // Itens apresentados: 1/2 = 50%; Quantidades atendidas: (100%+0%)/2 = 50%.
        Assert.True(nota < 5.00m);
        Assert.True(nota > 0m);
    }
}
