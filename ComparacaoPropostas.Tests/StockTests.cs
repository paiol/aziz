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

    [Fact]
    public void Inventario_FormatoSapDoArmazem_SoAreasDeiAirponEnergia()
    {
        var praia = Folha("Armazém Praia_Projeto 26082025", 8);
        Linha(praia, "Centro", "Depósito", "Material", "TxtBreveMaterial", "Contagem Total", "Utiliz.livre", "Observações", "Área");
        Linha(praia, "CPRA", "DPRP", "12453", "Pigtail SC/PC 1m", 275d, "UN", "Praia_Projeto", null);
        Linha(praia, "CPRA", "DPRP", "20076", "Escudete para Espia", 367d, "UN", "Praia_Projeto", "DEI_FTTH");
        Linha(praia, "CPRA", "DPRP", "20211", "Oculos 6020", 8d, "UN", "Praia_Projeto", "DEI_TORRE");
        Linha(praia, "CPRA", "DPRP", "30001", "Cabo RAM", 5d, "M", "Praia_Projeto", "RAM");
        Linha(praia, "CPRA", "DPRP", "30002", "Bateria 48V", 4d, "UN", "Praia_Projeto", "RAM, ENERGIA");
        Linha(praia, "CPRA", "DPRP", "30003", "ONT", 10d, "UN", "Praia_Projeto", "AIRPON");
        Linha(praia, "CPRA", "DPRP", "30004", "Retificador", 2d, "UN", "Praia_Projeto", "ENERGIA_Projeto Power Mindelo");
        Linha(praia, "CPRA", "DPRP", "30005", "Switch", 1d, "UN", "Praia_Projeto", "DOM_REDE IP");
        Linha(praia, "CPRA", "DPRP", "30003", "ONT", 5d, "UN", "Praia_Projeto", "DEI_FTTH, AIRPON");
        var sal = Folha("SAL jun 2025", 7);
        Linha(sal, "Centro", "Depósito", "Material", "TxtBreveMaterial", "Contagem ", "Utiliz.livre", "Observações");
        Linha(sal, "CPRA", "DSRP", "12596", "Patch Cord", 1d, "UN", "SAL_Projeto");
        var ds = new DataSet();
        ds.Tables.Add(praia);
        ds.Tables.Add(sal);

        var linhas = InventarioExcelService.Ler(ds, out var avisos, InventarioExcelService.SepararFiltro("DEI, AIRPON, ENERGIA"));

        // 12596 vem do SAL_Projeto (armazém de projeto = DEI, sem coluna Área).
        Assert.Equal(new[] { "12596", "20076", "20211", "30002", "30003", "30004" }, linhas.Select(l => l.Codigo).OrderBy(c => c).ToArray());
        Assert.Equal("DEI (outros)", linhas.Single(l => l.Codigo == "12596").AreaTecnica);
        var escudete = linhas.Single(l => l.Codigo == "20076");
        Assert.Equal(("Escudete para Espia", "UN", 367m, "DEI_FTTH"), (escudete.Descricao, escudete.Unidade, escudete.Quantidade, escudete.AreaArmazem));
        Assert.Equal("FBB", escudete.AreaTecnica);
        Assert.Equal("INF ENR", linhas.Single(l => l.Codigo == "30004").AreaTecnica);
        var ont = linhas.Single(l => l.Codigo == "30003");
        Assert.Equal(15m, ont.Quantidade);
        Assert.Equal("AIRPON · DEI_FTTH, AIRPON", ont.AreaArmazem);
        Assert.Contains(avisos, a => a.Contains("SAL jun 2025"));   // folha sem coluna Área
        Assert.Contains(avisos, a => a.Contains("6 aceite(s), 3 de outras áreas"));   // 12453 (sem área), RAM e DOM_REDE IP
    }

    [Fact]
    public void Inventario_UmGrupoPorArmazem_ArmazensDeProjetoEntramInteiros()
    {
        var praia = Folha("Armazém Praia", 8);
        Linha(praia, "Centro", "Depósito", "Material", "TxtBreveMaterial", "Contagem Total", "Utiliz.livre", "Observações", "Área");
        Linha(praia, "CPRA", "DPRP", "20076", "Escudete para Espia", 367d, "UN", "Praia_Projeto", "DEI_FTTH");
        Linha(praia, "CPRA", "DPRP", "20211", "Oculos 6020", 8d, "UN", "Praia_Projeto", "DEI_TORRE");
        Linha(praia, "CPRA", "DPRP", "30001", "Cabo RAM", 5d, "M", "Praia_Projeto", "RAM");
        var sal = Folha("SAL jun 2025", 7);
        Linha(sal, "Centro", "Depósito", "Material", "TxtBreveMaterial", "Contagem ", "Utiliz.livre", "Observações");
        Linha(sal, "CPRA", "DSRP", "20076", "Escudete p/ Espia", 12d, "UN", "SAL_Projeto");
        Linha(sal, "CPRA", "DSRP", "30001", "Cabo RAM", 3d, "M", "SAL_Projeto");    // armazém de projeto: entra (DEI)
        Linha(sal, "CPRA", "DSRP", "99999", "Outro", 1d, "UN", "SAL_Projeto");      // idem, área técnica DEI (outros)
        Linha(sal, "CPRA", "DBVS", "20076", "Escudete", 50d, "UN", "Boavista_Técnico");  // armazém técnico: fica de fora
        Linha(sal, "CPRA", "DSRP", "20211", "Oculos 6020", 2d, "UN", "SAL_Projeto");
        var ds = new DataSet();
        ds.Tables.Add(praia);
        ds.Tables.Add(sal);

        var armazens = InventarioExcelService.LerPorArmazem(ds, out var avisos, InventarioExcelService.SepararFiltro("DEI, AIRPON, ENERGIA"));

        Assert.Equal(new[] { "DPRP", "DSRP" }, armazens.Select(a => a.Deposito).ToArray());   // DBVS (Técnico) não entra
        var dsrp = armazens[1];
        Assert.Equal(("CPRA", "SAL_Projeto"), (dsrp.Centro, dsrp.Nome));
        Assert.Equal(new[] { "20076", "20211", "30001", "99999" }, dsrp.Linhas.Select(l => l.Codigo).OrderBy(c => c).ToArray());
        Assert.Equal("DEI (outros)", dsrp.Linhas.Single(l => l.Codigo == "99999").AreaTecnica);
        var escudete = dsrp.Linhas.Single(l => l.Codigo == "20076");
        Assert.Equal((12m, "FBB", "DEI_FTTH", "Escudete p/ Espia"), (escudete.Quantidade, escudete.AreaTecnica, escudete.AreaArmazem, escudete.Descricao));
        Assert.Equal("MBB", dsrp.Linhas.Single(l => l.Codigo == "20211").AreaTecnica);
        Assert.Equal(2, armazens[0].Linhas.Count);

        // Um inventário de um armazém só leva as linhas desse depósito.
        var soSal = InventarioExcelService.Ler(ds, out var avisos2, InventarioExcelService.SepararFiltro("DEI, AIRPON, ENERGIA"), "DSRP");
        Assert.Equal(12m, soSal.Single(l => l.Codigo == "20076").Quantidade);
        Assert.Contains(avisos2, a => a.Contains("outros armazéns"));
    }

    [Theory]
    [InlineData("DEI_FTTH", true)]
    [InlineData("DEI PROJETO", true)]
    [InlineData("SPARE_ENERGIA_DPSBLADE", true)]
    [InlineData("Energia CORE PS", true)]
    [InlineData("DEI_FTTH, 10 DEI, 10 CV MULTIMEDIA", true)]
    [InlineData("RAM", false)]
    [InlineData("DOM_REDE IP", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void Inventario_FiltroDeAreas(string? area, bool passa) =>
        Assert.Equal(passa, InventarioExcelService.AreaPassa(area, new[] { "DEI", "AIRPON", "ENERGIA" }));

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

public class StockAlarmesTests
{
    private static readonly DateTime Hoje = new(2026, 11, 22);

    private static MaterialStock Material(decimal? minimo, int? diasParado, params (TipoMovimentoStock Tipo, decimal Qtd, AreaDepartamento Area, int DiasAtras)[] movs)
    {
        var m = new MaterialStock { Id = 1, Codigo = "X", Descricao = "Material X", StockMinimo = minimo, DiasParado = diasParado };
        foreach (var (t, q, a, d) in movs)
            m.Movimentos.Add(new MovimentoStock { Tipo = t, Quantidade = q, Area = a, Data = Hoje.AddDays(-d) });
        return m;
    }

    [Fact]
    public void AbaixoDoMinimo_EsgotadoE_SaldoNegativo()
    {
        var abaixo = Material(10, null, (TipoMovimentoStock.Entrada, 12, AreaDepartamento.DepInf, 5), (TipoMovimentoStock.Saida, 5, AreaDepartamento.DepInf, 1));
        var esgotado = Material(2, null, (TipoMovimentoStock.Entrada, 3, AreaDepartamento.DepInf, 5), (TipoMovimentoStock.Saida, 3, AreaDepartamento.DepInf, 1));
        var negativo = Material(null, null, (TipoMovimentoStock.Entrada, 3, AreaDepartamento.DepInf, 5), (TipoMovimentoStock.Saida, 4, AreaDepartamento.DepSaf, 1));

        Assert.Equal(TipoAlarmeStock.AbaixoMinimo, Assert.Single(StockAlarmes.Calcular(new[] { abaixo }, Hoje)).Tipo);
        Assert.Equal(TipoAlarmeStock.Esgotado, Assert.Single(StockAlarmes.Calcular(new[] { esgotado }, Hoje)).Tipo);
        var n = Assert.Single(StockAlarmes.Calcular(new[] { negativo }, Hoje));
        Assert.Equal((TipoAlarmeStock.SaldoNegativo, SeveridadeAlarme.Critico, (AreaDepartamento?)AreaDepartamento.DepSaf), (n.Tipo, n.Severidade, n.Area));
    }

    [Fact]
    public void StockParado_PelaUltimaSaidaOuPelaEntrada()
    {
        var semSaidas = Material(null, null, (TipoMovimentoStock.Entrada, 5, AreaDepartamento.DepInf, 200));
        var saidaRecente = Material(null, null, (TipoMovimentoStock.Entrada, 5, AreaDepartamento.DepInf, 400), (TipoMovimentoStock.Saida, 1, AreaDepartamento.DepInf, 10));
        var limiteProprio = Material(null, 30, (TipoMovimentoStock.Entrada, 5, AreaDepartamento.DepInf, 40));
        var vazio = Material(null, null, (TipoMovimentoStock.Entrada, 5, AreaDepartamento.DepInf, 400), (TipoMovimentoStock.Saida, 5, AreaDepartamento.DepInf, 300));

        Assert.Equal(TipoAlarmeStock.Parado, Assert.Single(StockAlarmes.Calcular(new[] { semSaidas }, Hoje)).Tipo);
        Assert.Empty(StockAlarmes.Calcular(new[] { saidaRecente }, Hoje));
        Assert.Equal(TipoAlarmeStock.Parado, Assert.Single(StockAlarmes.Calcular(new[] { limiteProprio }, Hoje)).Tipo);
        Assert.Empty(StockAlarmes.Calcular(new[] { vazio }, Hoje));
    }

    [Fact]
    public void FiltroPorArea_SoConsideraMovimentosDaArea()
    {
        var m = Material(10, null, (TipoMovimentoStock.Entrada, 20, AreaDepartamento.DepInf, 5), (TipoMovimentoStock.Entrada, 2, AreaDepartamento.DepSaf, 5));
        Assert.Empty(StockAlarmes.Calcular(new[] { m }, Hoje));
        Assert.Equal(TipoAlarmeStock.AbaixoMinimo, Assert.Single(StockAlarmes.Calcular(new[] { m }, Hoje, AreaDepartamento.DepSaf)).Tipo);
    }
}

public class NomesMaterialTests
{
    private static MaterialStock Material() => new() { Id = 7, Codigo = "02312ABC", Descricao = "Cabo de fibra óptica 24F" };

    [Theory]
    [InlineData("Cabo de fibra óptica 24F", "CABO DE FIBRA OPTICA 24F")]
    [InlineData("Cabo de fibra óptica 24F", "  cabo  de fibra-óptica, 24F ")]
    public void MesmoNomeComAcentosOuPontuacaoDiferentesNaoDifere(string a, string b) =>
        Assert.False(NomesMaterial.Diferem(a, b));

    [Fact]
    public void NomesDiferentesDiferem()
    {
        Assert.True(NomesMaterial.Diferem("Cabo de fibra óptica 24F", "Cabo FO 24 fibras"));
        Assert.False(NomesMaterial.Diferem("Cabo", null));
    }

    [Fact]
    public void AlertaSoParaNomeNovo()
    {
        var m = Material();
        Assert.True(NomesMaterial.PrecisaAlerta(m, "Cabo FO 24 fibras", []));
        Assert.False(NomesMaterial.PrecisaAlerta(m, "cabo de fibra optica 24f", []));
        Assert.False(NomesMaterial.PrecisaAlerta(m, "02312abc", []));   // vem só o código
        Assert.False(NomesMaterial.PrecisaAlerta(m, "", []));
    }

    [Fact]
    public void NomeJaVistoOuDecididoNaoRepeteAlerta()
    {
        var m = Material();
        var pendente = new DivergenciaNomeMaterial { NomeCatalogo = m.Descricao, NomeRecebido = "Cabo FO 24 fibras" };
        Assert.False(NomesMaterial.PrecisaAlerta(m, "CABO FO 24 FIBRAS", [pendente]));

        // Catálogo atualizado para o nome do armazém: o nome antigo continua conhecido.
        m.Descricao = "Cabo FO 24 fibras";
        var decidida = new DivergenciaNomeMaterial { NomeCatalogo = "Cabo de fibra óptica 24F", NomeRecebido = "Cabo FO 24 fibras", Estado = EstadoDivergenciaNome.NomeAtualizado };
        Assert.False(NomesMaterial.PrecisaAlerta(m, "Cabo de fibra óptica 24F", [decidida]));
        Assert.True(NomesMaterial.PrecisaAlerta(m, "Fibra 24", [decidida]));
    }

    [Fact]
    public void AlertaPendenteApareceNosAlarmes()
    {
        var m = Material();
        m.DivergenciasNome.Add(new DivergenciaNomeMaterial { Id = 3, NomeCatalogo = m.Descricao, NomeRecebido = "Cabo FO 24 fibras", Origem = OrigemNomeMaterial.Inventario, Referencia = "Inventário #1" });
        m.DivergenciasNome.Add(new DivergenciaNomeMaterial { Id = 4, NomeCatalogo = m.Descricao, NomeRecebido = "Outro", Estado = EstadoDivergenciaNome.MantidoCatalogo });

        var alarmes = StockAlarmes.Calcular([m], DateTime.Today);

        var a = Assert.Single(alarmes);
        Assert.Equal(TipoAlarmeStock.NomeDiferente, a.Tipo);
        Assert.Equal(3, a.Divergencia!.Id);
        Assert.Contains("Cabo FO 24 fibras", a.Mensagem);
    }
}

public class AreasTecnicasTests
{
    [Theory]
    [InlineData("DEI_TORRE", "MBB")]
    [InlineData("DEI_TORRE, RAM", "MBB")]
    [InlineData("DEI_FTTH", "FBB")]
    [InlineData("DEI_FTTH, AIRPON", "FBB")]
    [InlineData("AIRPON", "FBB")]
    [InlineData("RAM, AIRPON", "FBB")]
    [InlineData("ENERGIA", "INF ENR")]
    [InlineData("ENERGIA_Projeto Power Mindelo", "INF ENR")]
    [InlineData("RAM, ENERGIA", "INF ENR")]
    [InlineData("SPARE_ENERGIA_DPSBLADE", "INF ENR")]
    [InlineData("DEI_TORRE E ENERGIA", "MBB")]
    [InlineData("DEI PROJETO", "DEI (outros)")]
    [InlineData("DEI_RAM, RAM", "DEI (outros)")]
    [InlineData("RAM", null)]
    [InlineData(null, null)]
    public void AreaDoArmazemDaAreaTecnica(string? areaArmazem, string? esperado) =>
        Assert.Equal(esperado, AreasTecnicas.De(areaArmazem));
}

public class SaldoPorArmazemTests
{
    [Fact]
    public void SaldoPorAreaEArmazem()
    {
        var movs = new[]
        {
            new MovimentoStock { Area = AreaDepartamento.DepInf, Armazem = "DPRP", Tipo = TipoMovimentoStock.Ajuste, Quantidade = 10 },
            new MovimentoStock { Area = AreaDepartamento.DepInf, Armazem = "DSRP", Tipo = TipoMovimentoStock.Ajuste, Quantidade = 4 },
            new MovimentoStock { Area = AreaDepartamento.DepInf, Armazem = "dprp", Tipo = TipoMovimentoStock.Saida, Quantidade = 3 },
            new MovimentoStock { Area = AreaDepartamento.DepInf, Tipo = TipoMovimentoStock.Entrada, Quantidade = 2 }
        };
        Assert.Equal(7m, StockCalculos.Saldo(movs, AreaDepartamento.DepInf, "DPRP"));
        Assert.Equal(4m, StockCalculos.Saldo(movs, AreaDepartamento.DepInf, "DSRP"));
        Assert.Equal(13m, StockCalculos.Saldo(movs, AreaDepartamento.DepInf, null));
        var porArmazem = StockCalculos.SaldoPorArmazem(movs);
        Assert.Equal((7m, 4m, 2m), (porArmazem["DPRP"], porArmazem["DSRP"], porArmazem[""]));
    }
}

public class ArmazemDeProjetoTests
{
    [Theory]
    [InlineData("Praia_Projeto", true)]
    [InlineData("SAL_Projeto", true)]
    [InlineData("Fogo_S.Filipe_Projeto", true)]
    [InlineData("S.Vicente_Mindelo_Projeto", true)]
    [InlineData("Boavista_Técnico", false)]
    [InlineData("S.Vicente_D Gest Rede Mind", false)]
    [InlineData(null, false)]
    public void ArmazemDeProjeto(string? nome, bool esperado) =>
        Assert.Equal(esperado, InventarioExcelService.EhArmazemDeProjeto(nome));
}
