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
}
