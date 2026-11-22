using System;
using System.Data;
using System.Linq;
using Xunit;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Services.Stock;

namespace ComparacaoPropostas.Tests;

public class PackingListTests
{
    private static DataTable Folha(string nome, int colunas)
    {
        var t = new DataTable(nome);
        for (var c = 0; c < colunas; c++) t.Columns.Add($"C{c}", typeof(object));
        return t;
    }

    private static void Linha(DataTable t, params object?[] valores)
    {
        var r = t.NewRow();
        for (var i = 0; i < valores.Length; i++) r[i] = valores[i] ?? DBNull.Value;
        t.Rows.Add(r);
    }

    // Réplica reduzida do Packing List da Huawei (mesmas posições de colunas).
    private static DataSet PackingList(bool comSnList = true)
    {
        var pl = Folha("PACKING LIST", 18);
        Linha(pl, null, null, null, "Huawei International Co. Limited", null, null, null, null, "B", "PACKINGLIST");
        Linha(pl, "TO:Cabo Verde Telecom, S.A.", null, null, "P/L No.", null, "0001322602230ADPA0002H", "Date", null, "2026/04/10 17:42:26", null, null, "Project", "Cape Verde CVT 2026-2027 1+1 Package Project-Equipment");
        Linha(pl, null, null, null, "Contract No.", null, "0001322602230A");
        Linha(pl, null, null, null, "Invoice No.", null, "HWI2026050500734", "P/O No.", null, "0001322602230A");
        Linha(pl, "CASE NO.", "MATERIAL", "CTNNO.", "PART NUMBER", "MODEL", "DESCRIPTION", "QTY", "UOM", "GW(KG)", "NW(KG)", "SIZE(MM)", "VOLUME(CBM)", "NOTE", "Pack Suite No. ", "BOX NAME", "DEVICE SN", "HW SERIAL NO.", "Manufacturers SN");
        Linha(pl, 1d, "Carton", null, "25033488", "C1050GR09", "Power Cable (Unit:meter)", 20d, "M", 7.9, 7.22, null, null, "Site:Site_Solar converter", null, "1321160G114");
        Linha(pl, 6d, "Carton", null, "02312MML-009", "SMU02C", "Site Monitor Unit", 2d, "PCS", null, null, null, null, "Site:Site_iSitePower_A", null, "1321160F954", null, "10263E767593 10263E767613");
        Linha(pl, null, null, null, "04070006", "CSST00301", "Traditional Signal Cable,3.0m", 20d, "PCS", null, null, null, null, null, null, "1321160F954");
        Linha(pl, "Total:3 CASES    gross weight(KG):151.1");

        var ds = new DataSet();
        ds.Tables.Add(pl);
        if (comSnList)
        {
            var sn = Folha("SN LIST", 10);
            Linha(sn, "CASE NO.", "CTNNO.", "PART NUMBER", "DESCRIPTION", "QTY", "HW SERIAL NO.", "BOX NAME", "Contract No.", "P/L No.", "P/O No.");
            Linha(sn, 6d, null, "02312MML-009", "Site Monitor Unit", 1d, "10263E767593", "1321160F954");
            Linha(sn, 6d, null, "02312MML-009", "Site Monitor Unit", 1d, "10263E767613", "1321160F954");
            Linha(sn, 1d, null, "25033488", "Power Cable", 20d, null, "1321160G114");
            ds.Tables.Add(sn);
        }
        return ds;
    }

    [Fact]
    public void Ler_CabecalhoELinhas()
    {
        var r = PackingListService.Ler(PackingList());

        Assert.Equal("0001322602230ADPA0002H", r.Numero);
        Assert.Equal("Huawei International Co. Limited", r.Fornecedor);
        Assert.Equal("0001322602230A", r.Contrato);
        Assert.Equal("HWI2026050500734", r.Fatura);
        Assert.Equal("0001322602230A", r.PurchaseOrder);
        Assert.Equal(new DateTime(2026, 4, 10), r.Data);
        Assert.Equal("1+1_2026", r.Contrato1Mais1);

        Assert.Equal(3, r.Linhas.Count);
        var cabo = r.Linhas[0];
        Assert.Equal(("1", "25033488", "C1050GR09", 20m, "M", "Site_Solar converter"), (cabo.Caixa, cabo.Codigo, cabo.Modelo, cabo.Quantidade, cabo.Unidade, cabo.Site));
        // Linha sem nº de caixa nem nota herda os da linha anterior (mesma caixa).
        Assert.Equal(("6", "Site_iSitePower_A", "1321160F954"), (r.Linhas[2].Caixa, r.Linhas[2].Site, r.Linhas[2].Embalagem));

        Assert.Equal(2, r.Series.Count);
        Assert.All(r.Series, s => Assert.Equal("02312MML-009", s.Codigo));
        Assert.Empty(r.Avisos);
    }

    [Fact]
    public void Ler_SemSnList_UsaColunaDaFolhaPrincipal()
    {
        var r = PackingListService.Ler(PackingList(comSnList: false));
        Assert.Equal(new[] { "10263E767593", "10263E767613" }, r.Series.Select(s => s.Serie).ToArray());
        Assert.All(r.Series, s => Assert.Equal("6", s.Caixa));
    }

    [Theory]
    [InlineData("Cape Verde CVT 2026-2027 1+1 Package Project-Equipment", "1+1_2026")]
    [InlineData("CVT 1+1 Package", "1+1_2025")]
    [InlineData("Cape Verde CVT Equipment", null)]
    [InlineData(null, null)]
    public void Contrato1Mais1_PeloNomeDoProjeto(string? projeto, string? esperado)
    {
        Assert.Equal(esperado, PackingListService.Contrato1Mais1(projeto, new DateTime(2025, 6, 1)));
    }
}

public class StockCalculosTests
{
    private static MovimentoStock Mov(TipoMovimentoStock tipo, decimal qtd, AreaDepartamento area = AreaDepartamento.DepInf)
        => new() { Tipo = tipo, Quantidade = qtd, Area = area };

    [Fact]
    public void Saldo_EntradasMenosSaidasMaisAjustes_PorArea()
    {
        var movs = new[]
        {
            Mov(TipoMovimentoStock.Entrada, 100),
            Mov(TipoMovimentoStock.Saida, 30),
            Mov(TipoMovimentoStock.Ajuste, -5),
            Mov(TipoMovimentoStock.Entrada, 20, AreaDepartamento.DepSaf)
        };

        Assert.Equal(85m, StockCalculos.Saldo(movs));
        Assert.Equal(65m, StockCalculos.Saldo(movs, AreaDepartamento.DepInf));
        Assert.Equal(20m, StockCalculos.SaldoPorArea(movs)[AreaDepartamento.DepSaf]);
    }

    [Fact]
    public void ValidarSaida_SaldoESeries()
    {
        Assert.Empty(StockCalculos.ValidarSaida(2, 5, seriesEmArmazem: 0, seriesEscolhidas: 0));
        Assert.Contains(StockCalculos.ValidarSaida(6, 5, 0, 0), e => e.Contains("Só há"));
        Assert.Contains(StockCalculos.ValidarSaida(0, 5, 0, 0), e => e.Contains("maior que zero"));
        Assert.Contains(StockCalculos.ValidarSaida(2, 5, seriesEmArmazem: 5, seriesEscolhidas: 1), e => e.Contains("número de série"));
        Assert.Empty(StockCalculos.ValidarSaida(2, 5, seriesEmArmazem: 5, seriesEscolhidas: 2));
    }
}

public class PackingListGenericoTests
{
    private static DataTable Folha(string nome, int colunas)
    {
        var t = new DataTable(nome);
        for (var c = 0; c < colunas; c++) t.Columns.Add($"C{c}", typeof(object));
        return t;
    }

    private static void Linha(DataTable t, params object?[] valores)
    {
        var r = t.NewRow();
        for (var i = 0; i < valores.Length; i++) r[i] = valores[i] ?? DBNull.Value;
        t.Rows.Add(r);
    }

    [Fact]
    public void Ler_OutroFornecedorComCabecalhosEmPortugues()
    {
        var t = Folha("Guia", 6);
        Linha(t, "Eltek Portugal, Lda");
        Linha(t, "Guia de remessa:", "GR-2026/118", null, "Data:", "2026-05-03");
        Linha(t, "Fatura", "FT 2026/77");
        Linha(t, "Código", "Designação", "Qtd.", "Unid.", "Nº Série");
        Linha(t, "RECT-48V", "Retificador 48V 3kW", 2d, "UN", "EL1001, EL1002");
        Linha(t, "CAB-16", "Cabo 16mm2", 50d, "M", null);
        Linha(t, "Total", null, 52d);
        var ds = new DataSet();
        ds.Tables.Add(t);

        var r = PackingListService.Ler(ds);

        Assert.Equal("GR-2026/118", r.Numero);
        Assert.Equal("FT 2026/77", r.Fatura);
        Assert.Equal(new DateTime(2026, 5, 3), r.Data);
        Assert.Null(r.Contrato1Mais1);
        Assert.Equal(2, r.Linhas.Count);
        Assert.Equal(("RECT-48V", "Retificador 48V 3kW", 2m, "UN"), (r.Linhas[0].Codigo, r.Linhas[0].Descricao, r.Linhas[0].Quantidade, r.Linhas[0].Unidade));
        Assert.Equal(new[] { "EL1001", "EL1002" }, r.Series.Select(s => s.Serie).ToArray());
    }

    [Fact]
    public void Inventario_SomaPorCodigoEContaLinhasPorNumeroDeSerie()
    {
        var t = Folha("Inventário", 5);
        Linha(t, "Inventário Armazém Praia 2026");
        Linha(t, "Código", "Descrição", "Unidade", "Quantidade contada", "Nº de série");
        Linha(t, "CAB-16", "Cabo 16mm2", "M", 30d, null);
        Linha(t, "CAB-16", "Cabo 16mm2", "M", 20.5, null);
        Linha(t, "SMU02C", "Site Monitor Unit", "PCS", null, "6R263E040172");
        Linha(t, "SMU02C", "Site Monitor Unit", "PCS", null, "6R263E040198");
        var ds = new DataSet();
        ds.Tables.Add(t);

        var linhas = InventarioExcelService.Ler(ds, out var avisos);

        Assert.Empty(avisos);
        Assert.Equal(2, linhas.Count);
        Assert.Equal(50.5m, linhas.Single(l => l.Codigo == "CAB-16").Quantidade);
        var smu = linhas.Single(l => l.Codigo == "SMU02C");
        Assert.Equal(2m, smu.Quantidade);
        Assert.Equal(2, smu.Series.Count);
    }

    [Theory]
    [InlineData(10, 8, 2)]
    [InlineData(5, 8, -3)]
    [InlineData(8, 8, null)]
    [InlineData(null, 8, null)]
    public void Inventario_AjusteEContadoMenosSistema(int? contado, int sistema, int? esperado)
    {
        Assert.Equal((decimal?)esperado, InventarioCalculos.Ajuste(contado, sistema));
    }
}
