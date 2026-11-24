using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;

namespace ComparacaoPropostas.Services.Stock;

public class LinhaPackingList
{
    public string? Caixa { get; set; }
    public string Codigo { get; set; } = "";
    public string? Modelo { get; set; }
    public string Descricao { get; set; } = "";
    public string? Unidade { get; set; }
    public decimal Quantidade { get; set; }
    public string? Embalagem { get; set; }
    public string? Site { get; set; }
}

public record SeriePackingList(string Codigo, string Serie, string? Caixa);

public class PackingListImportado
{
    public string? Numero { get; set; }
    public string? Fornecedor { get; set; }
    public string? Contrato { get; set; }
    public string? Fatura { get; set; }
    public string? PurchaseOrder { get; set; }
    public DateTime? Data { get; set; }
    public string? ProjetoFornecedor { get; set; }
    public string? Contrato1Mais1 { get; set; }
    public List<LinhaPackingList> Linhas { get; set; } = new();
    public List<SeriePackingList> Series { get; set; } = new();
    public List<string> Avisos { get; set; } = new();
}

public interface IPackingListService
{
    PackingListImportado Ler(Stream ficheiro, string? nomeFicheiro = null);
}

// Lê o Packing List da Huawei (.xls ou .xlsx): folha "PACKING LIST" (linhas por caixa) e,
// se existir, a folha "SN LIST" (um número de série por linha).
public class PackingListService : IPackingListService
{
    static PackingListService() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    // Excel, Word (.docx) ou PDF com texto: ver DocumentoTabelas.
    public PackingListImportado Ler(Stream ficheiro, string? nomeFicheiro = null) => Ler(DocumentoTabelas.Ler(ficheiro, nomeFicheiro));

    // Nomes de colunas aceites (inglês dos fornecedores e português). Comparação sem maiúsculas;
    // nomes com 4+ letras também aceitam o cabeçalho que começa por eles (ex.: "QTY (PCS)").
    internal static readonly string[] ColCodigo = { "part number", "part no", "part no.", "p/n", "código", "codigo", "cód.", "cod.", "referência", "referencia", "ref.", "item code", "material code", "código artigo", "artigo", "sku" };
    internal static readonly string[] ColDescricao = { "description", "descrição", "descricao", "designação", "designacao" };
    internal static readonly string[] ColQuantidade = { "qty", "quantity", "quantidade", "qtd", "qtd.", "quant." };
    internal static readonly string[] ColUnidade = { "uom", "unit", "unidade", "un", "un.", "unid." };
    internal static readonly string[] ColModelo = { "model", "modelo" };
    internal static readonly string[] ColCaixa = { "case no", "case no.", "caixa", "nº caixa", "palete", "pallet" };
    internal static readonly string[] ColNota = { "note", "site", "obs", "observações", "observacoes" };
    internal static readonly string[] ColEmbalagem = { "box name", "embalagem" };
    internal static readonly string[] ColSerie = { "hw serial no", "hw serial no.", "serial no", "serial no.", "serial number", "s/n", "sn", "nº série", "nº de série", "n.º série", "número de série", "numero de serie" };

    public static PackingListImportado Ler(DataSet ds)
    {
        var r = new PackingListImportado();
        var folhas = ds.Tables.Cast<DataTable>().ToList();
        if (folhas.Count == 0)
        {
            r.Avisos.Add("O ficheiro não tem folhas.");
            return r;
        }

        // Folha "packing" primeiro; senão a primeira folha com uma linha de cabeçalho reconhecível.
        var candidatas = folhas.OrderByDescending(t => Norm(t.TableName).Contains("packing")).ToList();
        DataTable? pl = null;
        var linhaCab = -1;
        foreach (var t in candidatas)
        {
            linhaCab = EncontrarCabecalho(t, ColCodigo, ColQuantidade);
            if (linhaCab >= 0) { pl = t; break; }
        }
        if (pl == null)
        {
            r.Avisos.Add("Não foi encontrada a linha de cabeçalho (é preciso uma coluna de código — ex.: \"PART NUMBER\" ou \"Código\" — e uma de quantidade).");
            return r;
        }

        LerMetadados(pl, linhaCab, r);

        int? Col(string[] nomes) => Coluna(pl, linhaCab, nomes);
        var cCaixa = Col(ColCaixa);
        var cCodigo = Col(ColCodigo)!.Value;
        var cModelo = Col(ColModelo);
        var cDesc = Col(ColDescricao);
        var cQtd = Col(ColQuantidade);
        var cUom = Col(ColUnidade);
        var cNota = Col(ColNota);
        var cBox = Col(ColEmbalagem);
        var cSerie = Col(ColSerie);

        string? caixa = null, site = null;
        var seriesDaFolha = new List<SeriePackingList>();
        for (var i = linhaCab + 1; i < pl.Rows.Count; i++)
        {
            var primeira = Norm(Texto(pl, i, 0));
            if (primeira.StartsWith("total")) break;

            var codigo = Texto(pl, i, cCodigo);
            var caixaLinha = cCaixa is int cc ? Texto(pl, i, cc) : null;
            if (!string.IsNullOrWhiteSpace(caixaLinha))
            {
                caixa = caixaLinha;
                site = null; // nova caixa: o site vem na nota desta linha
            }
            var nota = cNota is int cn ? Texto(pl, i, cn) : null;
            if (!string.IsNullOrWhiteSpace(nota)) site = Regex.Replace(nota, @"^\s*site\s*:\s*", "", RegexOptions.IgnoreCase).Trim();

            if (string.IsNullOrWhiteSpace(codigo) || Norm(codigo).StartsWith("total")) continue;

            r.Linhas.Add(new LinhaPackingList
            {
                Caixa = caixa,
                Codigo = codigo.Trim(),
                Modelo = cModelo is int cm ? Texto(pl, i, cm) : null,
                Descricao = (cDesc is int cd ? Texto(pl, i, cd) : null) ?? codigo.Trim(),
                Unidade = cUom is int cu ? Texto(pl, i, cu) : null,
                Quantidade = cQtd is int cq ? Numero(pl.Rows[i][cq]) ?? 0m : 0m,
                Embalagem = cBox is int cb ? Texto(pl, i, cb) : null,
                Site = site
            });

            if (cSerie is int cs)
                foreach (var s in (Texto(pl, i, cs) ?? "").Split(new[] { ' ', '\n', '\r', '\t', ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                    seriesDaFolha.Add(new SeriePackingList(codigo.Trim(), s.Trim(), caixa));
        }

        // Números de série: a folha "SN LIST" (Huawei) é a fonte mais fiável; senão a coluna da própria folha.
        var sn = folhas.FirstOrDefault(t => t != pl && Norm(t.TableName).Replace(" ", "").Contains("snlist"));
        var seriesSn = sn != null ? LerSnList(sn) : new List<SeriePackingList>();
        r.Series = (seriesSn.Count > 0 ? seriesSn : seriesDaFolha)
            .GroupBy(s => (s.Codigo, s.Serie)).Select(g => g.First()).ToList();

        if (r.Linhas.Count == 0) r.Avisos.Add("Não foi encontrada nenhuma linha de material.");
        return r;
    }

    private static List<SeriePackingList> LerSnList(DataTable t)
    {
        var lista = new List<SeriePackingList>();
        var cab = EncontrarCabecalho(t, ColCodigo, ColSerie);
        if (cab < 0) return lista;

        var cCodigo = Coluna(t, cab, ColCodigo);
        var cSerie = Coluna(t, cab, ColSerie);
        var cCaixa = Coluna(t, cab, ColCaixa);
        if (cCodigo == null || cSerie == null) return lista;

        for (var i = cab + 1; i < t.Rows.Count; i++)
        {
            var codigo = Texto(t, i, cCodigo.Value);
            var serie = Texto(t, i, cSerie.Value);
            if (string.IsNullOrWhiteSpace(codigo) || string.IsNullOrWhiteSpace(serie)) continue;
            lista.Add(new SeriePackingList(codigo.Trim(), serie.Trim(), cCaixa is int cc ? Texto(t, i, cc) : null));
        }
        return lista;
    }

    // Rótulos do cabeçalho do documento (inglês / português), sem ":" final.
    private static readonly Dictionary<string, string> Rotulos = new()
    {
        ["p/l no."] = "numero", ["packing list no."] = "numero", ["packing list nº"] = "numero", ["nº packing list"] = "numero",
        ["guia de remessa"] = "numero", ["guia nº"] = "numero", ["nº guia"] = "numero",
        ["contract no."] = "contrato", ["contrato"] = "contrato", ["contrato nº"] = "contrato",
        ["invoice no."] = "fatura", ["invoice"] = "fatura", ["fatura"] = "fatura", ["factura"] = "fatura", ["fatura nº"] = "fatura",
        ["p/o no."] = "po", ["po no."] = "po", ["purchase order"] = "po", ["encomenda"] = "po", ["nº encomenda"] = "po",
        ["project"] = "projeto", ["projeto"] = "projeto", ["projecto"] = "projeto",
        ["date"] = "data", ["data"] = "data"
    };

    private static void LerMetadados(DataTable t, int ateLinha, PackingListImportado r)
    {
        for (var i = 0; i < ateLinha; i++)
        {
            for (var c = 0; c < t.Columns.Count; c++)
            {
                var celula = Texto(t, i, c);
                if (string.IsNullOrWhiteSpace(celula)) continue;
                var rotulo = Norm(celula).TrimEnd(':').Trim();

                if (r.Fornecedor == null && rotulo.Contains("huawei") && !rotulo.StartsWith("to")) r.Fornecedor = celula.Trim();
                string? valor;
                if (Rotulos.TryGetValue(rotulo, out var campo)) valor = Seguinte(t, i, c);
                else if (RotuloComValor(celula) is { } rv) (campo, valor) = rv;   // "P/L No. 0001322..." (PDF)
                else continue;
                switch (campo)
                {
                    case "numero": r.Numero = Vazio(r.Numero) ?? Vazio(valor); break;
                    case "contrato": r.Contrato = Vazio(r.Contrato) ?? Vazio(valor); break;
                    case "fatura": r.Fatura = Vazio(r.Fatura) ?? Vazio(valor); break;
                    case "po": r.PurchaseOrder = Vazio(r.PurchaseOrder) ?? Vazio(valor); break;
                    case "projeto": r.ProjetoFornecedor = Vazio(r.ProjetoFornecedor) ?? Vazio(valor); break;
                    case "data":
                        if (r.Data == null && valor != null && DateTime.TryParse(valor.Replace('/', '-'), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) r.Data = dt.Date;
                        break;
                }
            }
        }
        // PDF: o valor do "Project" pode ficar noutra linha (célula com texto centrado): procura o "1+1".
        if (r.ProjetoFornecedor == null)
            for (var i = 0; i < ateLinha && r.ProjetoFornecedor == null; i++)
                for (var c = 0; c < t.Columns.Count; c++)
                    if (Texto(t, i, c) is { } x && x.Contains("1+1")) { r.ProjetoFornecedor = x.Trim(); break; }
        r.Contrato1Mais1 = Contrato1Mais1(r.ProjetoFornecedor, r.Data);
    }

    private static string? Vazio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    // Etiqueta e valor na mesma célula (ex.: "Contract No. 0001322602230A", "Date 2026/04/10 14:30").
    private static (string Campo, string Valor)? RotuloComValor(string celula)
    {
        foreach (var (rotulo, campo) in Rotulos.OrderByDescending(r => r.Key.Length))
        {
            var padrao = @"^\s*" + string.Join(@"\s*", rotulo.Split(' ').Select(Regex.Escape)) + @"\s*:?\s+(.+)$";
            var m = Regex.Match(celula, padrao, RegexOptions.IgnoreCase);
            if (m.Success) return (campo, m.Groups[1].Value.Trim());
        }
        return null;
    }

    // "Cape Verde CVT 2026-2027 1+1 Package Project" → "1+1_2026". Sem "1+1" no nome: null.
    public static string? Contrato1Mais1(string? projeto, DateTime? data)
    {
        if (string.IsNullOrWhiteSpace(projeto) || !projeto.Contains("1+1")) return null;
        var m = Regex.Match(projeto, @"\b(20\d{2})\b");
        var ano = m.Success ? m.Groups[1].Value : data?.Year.ToString();
        return ano == null ? "1+1" : $"1+1_{ano}";
    }

    // Primeira linha que tem uma coluna de cada grupo (ex.: código e quantidade).
    private static int EncontrarCabecalho(DataTable t, params string[][] grupos)
    {
        for (var i = 0; i < t.Rows.Count; i++)
            if (grupos.All(g => Coluna(t, i, g) != null)) return i;
        return -1;
    }

    private static int? Coluna(DataTable t, int linha, string[] nomes)
    {
        var cabecalhos = Enumerable.Range(0, t.Columns.Count).Select(c => (c, h: Norm(Texto(t, linha, c)).TrimEnd(':').Trim())).ToList();
        foreach (var (c, h) in cabecalhos)
            if (nomes.Contains(h)) return c;
        foreach (var (c, h) in cabecalhos)
            if (h.Length > 0 && nomes.Any(n => n.Length >= 4 && h.StartsWith(n))) return c;
        return null;
    }

    private static string? Seguinte(DataTable t, int linha, int coluna)
    {
        for (var c = coluna + 1; c < t.Columns.Count; c++)
        {
            var v = Texto(t, linha, c);
            if (!string.IsNullOrWhiteSpace(v)) return v.Trim();
        }
        return null;
    }

    private static string? Texto(DataTable t, int linha, int coluna)
    {
        if (linha >= t.Rows.Count || coluna >= t.Columns.Count) return null;
        var v = t.Rows[linha][coluna];
        return v switch
        {
            null or DBNull => null,
            double d => d.ToString(CultureInfo.InvariantCulture),
            DateTime dt => dt.ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture),
            _ => v.ToString()
        };
    }

    private static decimal? Numero(object? v) => v switch
    {
        double d => (decimal)d,
        int i => i,
        decimal m => m,
        string s when decimal.TryParse(s.Replace(',', '.'), NumberStyles.Number, CultureInfo.InvariantCulture, out var x) => x,
        _ => null
    };

    private static string Norm(string? s) => Regex.Replace(s ?? "", @"\s+", " ").Trim().ToLowerInvariant();
}
