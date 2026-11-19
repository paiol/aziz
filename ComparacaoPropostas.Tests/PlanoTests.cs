using System.IO;
using System.Linq;
using ClosedXML.Excel;
using Xunit;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Services;

namespace ComparacaoPropostas.Tests;

public class PlanoCalculosTests
{
    [Theory]
    [InlineData(SituacaoProjetoPlano.Transitado, CaixaPlano.Entra)]
    [InlineData(SituacaoProjetoPlano.TransitadoParcial, CaixaPlano.Entra)]
    [InlineData(SituacaoProjetoPlano.Novo, CaixaPlano.Entra)]
    [InlineData(SituacaoProjetoPlano.Condicionado, CaixaPlano.Condicionado)]
    [InlineData(SituacaoProjetoPlano.ExecutadoRetirar, CaixaPlano.ForaDoPlano)]
    [InlineData(SituacaoProjetoPlano.Cancelado, CaixaPlano.ForaDoPlano)]
    [InlineData(SituacaoProjetoPlano.ExploracaoDom, CaixaPlano.ForaDoPlano)]
    [InlineData(null, CaixaPlano.SemSituacao)]
    public void CaixaDoPlano_SegueSituacao(SituacaoProjetoPlano? situacao, CaixaPlano esperado)
    {
        Assert.Equal(esperado, PlanoCalculos.CaixaDoPlano(situacao));
    }

    [Fact]
    public void TotalOrcamento_SomaTransitarENovo_ExcetoForaDoPlano()
    {
        var ativo = new ProjetoPlano { Situacao = SituacaoProjetoPlano.Transitado, ValorTransitar = 184944.4m, ValorNovo = 89760m, TotalOrcamentoAnoAnterior = 534090m };
        Assert.Equal(274704.4m, ativo.TotalOrcamento);
        Assert.Equal(274704.4m - 534090m, ativo.DeltaAnoAnterior);

        var cancelado = new ProjetoPlano { Situacao = SituacaoProjetoPlano.Cancelado, ValorTransitar = 1000m, ValorNovo = 500m };
        Assert.Equal(0m, cancelado.TotalOrcamento);

        var semValores = new ProjetoPlano { Situacao = SituacaoProjetoPlano.Novo };
        Assert.Equal(0m, semValores.TotalOrcamento);
    }

    [Fact]
    public void TotalCustos_NaoSomaTerceirizacao()
    {
        var p = new ProjetoPlano { CustoConstrucoes = 195000m, CustoServicos = 35000m, CustoTerceirizacao = 35000m };
        Assert.Equal(230000m, p.TotalCustos);
    }

    [Fact]
    public void TotalAcaoFormacao_AplicaFormulaDaFolha()
    {
        var acao = new AcaoFormacaoPlano { NumeroPessoas = 2, Dias = 5, InscricaoPorPessoa = 1000m, PassagemPorPessoa = 50000m, EstadiaDia = 7300m, AjudaCustoDia = 3900m };
        // 2 × (1000 + 50000 + 5 × (7300 + 3900)) = 2 × 107000
        Assert.Equal(214000m, acao.Total);

        Assert.Equal(0m, new AcaoFormacaoPlano { Dias = 5, EstadiaDia = 7300m }.Total);
    }

    [Fact]
    public void CargaMensal_ReparteEsforcoPelosMesesAtivos_SoProjetosQueEntram()
    {
        var projetos = new[]
        {
            new ProjetoPlano { Situacao = SituacaoProjetoPlano.Novo, EsforcoArquiteturaDias = 6m, MesInicio = 3, MesFim = 5 },
            new ProjetoPlano { Situacao = SituacaoProjetoPlano.Transitado, EsforcoArquiteturaDias = 12m },
            new ProjetoPlano { Situacao = SituacaoProjetoPlano.Condicionado, EsforcoArquiteturaDias = 100m },
            // Fim antes do início: fica só no mês de início.
            new ProjetoPlano { Situacao = SituacaoProjetoPlano.Novo, EsforcoArquiteturaDias = 4m, MesInicio = 10, MesFim = 2 }
        };

        var carga = PlanoCalculos.CargaMensal(projetos, p => p.EsforcoArquiteturaDias);

        Assert.Equal(1m, carga[0]);
        Assert.Equal(3m, carga[2]);
        Assert.Equal(3m, carga[4]);
        Assert.Equal(1m, carga[5]);
        Assert.Equal(5m, carga[9]);
        Assert.Equal(22m, carga.Sum());
    }

    [Fact]
    public void CalcularResumo_TotaisPorRubricaECapacidade()
    {
        var plano = new Plano
        {
            Ano = 2027,
            CapacidadeArquiteturaDias = 10m,
            CapacidadeEnergiaDias = 20m,
            Projetos =
            {
                new ProjetoPlano { DesignacaoRubrica = "Geradores", Situacao = SituacaoProjetoPlano.Transitado, ValorTransitar = 100m, ValorNovo = 50m, TotalOrcamentoAnoAnterior = 200m, EsforcoArquiteturaDias = 5m, EsforcoEnergiaDias = 4m },
                new ProjetoPlano { DesignacaoRubrica = "Geradores", Situacao = SituacaoProjetoPlano.Novo, ValorNovo = 300m, EsforcoArquiteturaDias = 7m },
                new ProjetoPlano { DesignacaoRubrica = "Geradores", Situacao = SituacaoProjetoPlano.Condicionado, ValorNovo = 999m },
                new ProjetoPlano { DesignacaoRubrica = "Hybrid", Situacao = SituacaoProjetoPlano.Novo },
                new ProjetoPlano { DesignacaoRubrica = "DOM", Situacao = SituacaoProjetoPlano.ExploracaoDom, ValorTransitar = 70m }
            },
            AcoesFormacao =
            {
                new AcaoFormacaoPlano { Prioridade = 1, NumeroPessoas = 1, InscricaoPorPessoa = 100m, NoPlano = true },
                new AcaoFormacaoPlano { Prioridade = 2, NumeroPessoas = 2, InscricaoPorPessoa = 100m, Ano = 2027 },
                new AcaoFormacaoPlano { Prioridade = 1, NumeroPessoas = 5, InscricaoPorPessoa = 100m, Ano = 2028 }
            }
        };

        var r = PlanoCalculos.CalcularResumo(plano);

        Assert.Equal(3, r.NumAtivos);
        Assert.Equal(1, r.NumAtivosSemValor);
        Assert.Equal(1, r.NumCondicionados);
        Assert.Equal(100m, r.ValorTransitar);
        Assert.Equal(350m, r.ValorNovo);
        Assert.Equal(450m, r.TotalOrcamento);
        Assert.Equal(250m, r.Delta);
        Assert.Equal(70m, r.TransferidoDom);
        Assert.Equal(1.2m, r.UsoCapacidadeArquitetura);
        Assert.Equal(0.2m, r.UsoCapacidadeEnergia);

        var geradores = r.PorRubrica.Single(l => l.Rubrica == "Geradores");
        Assert.Equal(2, geradores.NumAtivos);
        Assert.Equal(450m, geradores.TotalOrcamento);
        Assert.Equal(1, geradores.NumCondicionados);
        Assert.DoesNotContain(r.PorRubrica, l => l.Rubrica == "DOM");

        Assert.Equal(1, r.PorCaixa.Single(c => c.Caixa == CaixaPlano.ForaDoPlano).NumProjetos);

        // Formação: só as ações de 2027 (sem ano = ano do plano).
        Assert.Equal(100m, r.CenariosFormacao[0].Custo);
        Assert.Equal(300m, r.CenariosFormacao[1].Custo);
        Assert.Equal(3, r.CenariosFormacao[2].Participacoes);
        Assert.Equal(100m, r.CenariosFormacao[3].Custo);
    }
}

public class PlanoExcelServiceTests
{
    private static MemoryStream CriarFicheiro()
    {
        var wb = new XLWorkbook();

        var resumo = wb.Worksheets.Add("Resumo 2027");
        resumo.Cell(1, 1).Value = "Plano 2027 DEP-INF — Resumo";

        var scope = wb.Worksheets.Add("Scope DEP-INF 2027");
        string[] headers =
        {
            "Capex 2025", "Cod Capex 2026", "Cod Capex 2027", "Designação da rubrica", "Tipo", "Ilha",
            "Classificação Projeto", "Projeto", "Descrição", "Observações / justificação 2027", "CAPEX",
            "Situação 2027", "Prioridade", "Execução 2026 (%)", "Zona\n(urbano / rural)", "Fiscalização · civil",
            "Valor a transitar 2027", "Valor novo 2027\n(estimativa)", "TOTAL ORC. 2027", "TOTAL ORC. 2026 (referência)",
            "Equipamento", "Construções", "dos quais terceirização\n(serviços externos)", "Início (mês)", "Fim (mês)",
            "Esforço base · Arquitetura (dias)", "Esforço 2027 · Arquitetura (dias)", "Esforço 2027 · Energia (dias)",
            "Projeto externo Valor manual"
        };
        for (var c = 0; c < headers.Length; c++) scope.Cell(1, c + 1).Value = headers[c];

        object[] linha1 =
        {
            "CVT24_26", "CVT24_26_26", "", "Projetos Geradores", "ENR", "SA",
            "Proj. Gerador", "Substituição Gerador Paul", "Substituição de gerador", "Continuação", "CAPEX 27 _Transitado",
            "Transitado", "Média", 0.53, "Urbano", "Terceiro",
            184944.4, 89760, "", 534090,
            0, 0, 0, "Mar", "Jun",
            6, 1.5, 0, 999
        };
        for (var c = 0; c < linha1.Length; c++) scope.Cell(2, c + 1).Value = XLCellValue.FromObject(linha1[c]);
        scope.Cell(2, 19).FormulaA1 = "Q2+R2";

        scope.Cell(3, 8).Value = ""; // linha sem projeto: ignorada
        scope.Cell(4, 8).Value = "Gerador Loja Maio";
        scope.Cell(4, 12).Value = "Executado - retirar";
        scope.Cell(5, 8).Value = "Projeto com situação estranha";
        scope.Cell(5, 12).Value = "Talvez";

        var formacao = wb.Worksheets.Add("Formação e feiras 2027");
        formacao.Cell(1, 1).Value = "Formação e feiras 2027 — DEP-INF";
        string[] fh = { "Prioridade", "Cenário", "Tipo", "Ação", "Datas 2027", "Local", "Formato", "Ligação aos projetos 2027",
                        "Nº pessoas", "Dias (incl. viagem)", "Inscrição / curso por pessoa", "Passagem por pessoa",
                        "Estadia / dia", "Ajuda de custo / dia", "Total (CVE)", "No plano?", "Participantes (nomes)", "Fonte", "Observações", "Ano" };
        for (var c = 0; c < fh.Length; c++) formacao.Cell(11, c + 1).Value = fh[c];
        formacao.Cell(12, 1).Value = 1;
        formacao.Cell(12, 3).Value = "Feira";
        formacao.Cell(12, 4).Value = "Middle East Energy";
        formacao.Cell(12, 9).Value = 2;
        formacao.Cell(12, 10).Value = 5;
        formacao.Cell(12, 16).Value = "Sim";
        formacao.Cell(12, 20).Value = 2027;
        formacao.Cell(13, 15).FormulaA1 = "IF(I13=\"\",\"\",1)"; // linha vazia só com fórmula: ignorada

        var parametros = wb.Worksheets.Add("Parâmetros plano");
        parametros.Cell(12, 1).Value = "Dias disponíveis para projetos / ano";
        parametros.Cell(12, 2).Value = 138.45;
        parametros.Cell(12, 3).Value = 120;

        var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;
        return ms;
    }

    [Fact]
    public void LerPlanoExcel_LeProjetosFormacaoECapacidade()
    {
        using var ficheiro = CriarFicheiro();
        var r = new PlanoExcelService().LerPlanoExcel(ficheiro);

        Assert.Equal(2027, r.Ano);
        Assert.Equal("Plano 2027 DEP-INF", r.Designacao);
        Assert.Equal(138.45m, r.CapacidadeArquiteturaDias);
        Assert.Equal(120m, r.CapacidadeEnergiaDias);

        Assert.Equal(3, r.Projetos.Count);
        var p = r.Projetos[0];
        Assert.Equal("Substituição Gerador Paul", p.Nome);
        Assert.Equal("CVT24_26", p.CapexAnoMenos2);
        Assert.Equal("CVT24_26_26", p.CodCapexAnoAnterior);
        Assert.Null(p.CodCapex);
        Assert.Equal("Projetos Geradores", p.DesignacaoRubrica);
        Assert.Equal("ENR", p.Tipo);
        Assert.Equal("Proj. Gerador", p.Classificacao);
        Assert.Equal("Continuação", p.Observacoes);
        Assert.Equal("CAPEX 27 _Transitado", p.Capex);
        Assert.Equal(SituacaoProjetoPlano.Transitado, p.Situacao);
        Assert.Equal(PrioridadeProjetoPlano.Media, p.Prioridade);
        Assert.Equal(0.53m, p.ExecucaoAnoAnterior);
        Assert.Equal("Urbano", p.Zona);
        Assert.Equal("Terceiro", p.FiscalizacaoCivil);
        Assert.Equal(184944.4m, p.ValorTransitar);
        Assert.Equal(89760m, p.ValorNovo);
        Assert.Equal(534090m, p.TotalOrcamentoAnoAnterior);
        Assert.Equal(3, p.MesInicio);
        Assert.Equal(6, p.MesFim);
        // "Esforço 2027 · Arquitetura" e não "Esforço base · Arquitetura".
        Assert.Equal(1.5m, p.EsforcoArquiteturaDias);
        Assert.Equal(274704.4m, p.TotalOrcamento);

        Assert.Equal(SituacaoProjetoPlano.ExecutadoRetirar, r.Projetos[1].Situacao);
        Assert.Null(r.Projetos[2].Situacao);
        Assert.Contains(r.Avisos, a => a.Contains("Talvez"));

        var acao = Assert.Single(r.AcoesFormacao);
        Assert.Equal("Middle East Energy", acao.Acao);
        Assert.Equal(TipoAcaoFormacao.Feira, acao.Tipo);
        Assert.Equal(1, acao.Prioridade);
        Assert.Equal(2, acao.NumeroPessoas);
        Assert.Equal(5m, acao.Dias);
        Assert.True(acao.NoPlano);
        Assert.Equal(2027, acao.Ano);
    }

    [Fact]
    public void LerPlanoExcel_SemFolhaScope_DevolveAviso()
    {
        var wb = new XLWorkbook();
        wb.Worksheets.Add("Outra");
        using var ms = new MemoryStream();
        wb.SaveAs(ms);
        ms.Position = 0;

        var r = new PlanoExcelService().LerPlanoExcel(ms);

        Assert.Empty(r.Projetos);
        Assert.NotEmpty(r.Avisos);
    }

    [Theory]
    [InlineData("Jan", 1)]
    [InlineData("dez", 12)]
    [InlineData("7", 7)]
    [InlineData("13", null)]
    [InlineData("", null)]
    public void ParseMes_AceitaNomeOuNumero(string texto, int? esperado)
    {
        Assert.Equal(esperado, PlanoExcelService.ParseMes(texto));
    }
}
