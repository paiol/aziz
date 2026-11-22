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
    PackingListImportado Ler(Stream ficheiro);
}

// Lê o Packing List da Huawei (.xls ou .xlsx): folha "PACKING LIST" (linhas por caixa) e,
// se existir, a folha "SN LIST" (um número de série por linha).
public class PackingListService : IPackingListService
{
    static PackingListService() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    public PackingListImportado Ler(Stream ficheiro)
    {
        DataSet ds;
        using (var reader = ExcelReaderFactory.CreateReader(ficheiro))
            ds = reader.AsDataSet();
        return Ler(ds);
    }

    public static PackingListImportado Ler(DataSet ds)
    {
        var r = new PackingListImportado();
        var folhas = ds.Tables.Cast<DataTable>().ToList();
        var pl = folhas.FirstOrDefault(t => Norm(t.TableName).Contains("packing")) ?? folhas.FirstOrDefault();
        if (pl == null)
        {
            r.Avisos.Add("O ficheiro não tem folhas.");
            return r;
        }

        var linhaCab = EncontrarCabecalho(pl, "part number");
        if (linhaCab < 0)
        {
            r.Avisos.Add("Não foi encontrada a linha de cabeçalho com \"PART NUMBER\".");
            return r;
        }

        LerMetadados(pl, linhaCab, r);

        int? Col(DataTable t, int linha, params string[] nomes)
        {
            for (var c = 0; c < t.Columns.Count; c++)
            {
                var h = Norm(Texto(t, linha, c));
                if (nomes.Any(n => h == n || h.StartsWith(n))) return c;
            }
            return null;
        }

        var cCaixa = Col(pl, linhaCab, "case no");
        var cCodigo = Col(pl, linhaCab, "part number")!.Value;
        var cModelo = Col(pl, linhaCab, "model");
        var cDesc = Col(pl, linhaCab, "description");
        var cQtd = Col(pl, linhaCab, "qty", "quantity");
        var cUom = Col(pl, linhaCab, "uom", "unit");
        var cNota = Col(pl, linhaCab, "note");
        var cBox = Col(pl, linhaCab, "box name");
        var cSerie = Col(pl, linhaCab, "hw serial no");

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

            if (string.IsNullOrWhiteSpace(codigo)) continue;

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

        // Números de série: a folha "SN LIST" é a fonte mais fiável (a coluna da folha principal pode vir cortada).
        var sn = folhas.FirstOrDefault(t => Norm(t.TableName).Replace(" ", "").Contains("snlist"));
        var seriesSn = sn != null ? LerSnList(sn) : new List<SeriePackingList>();
        r.Series = (seriesSn.Count > 0 ? seriesSn : seriesDaFolha)
            .GroupBy(s => (s.Codigo, s.Serie)).Select(g => g.First()).ToList();

        if (r.Linhas.Count == 0) r.Avisos.Add("Não foi encontrada nenhuma linha de material.");
        return r;
    }

    private static List<SeriePackingList> LerSnList(DataTable t)
    {
        var lista = new List<SeriePackingList>();
        var cab = EncontrarCabecalho(t, "part number");
        if (cab < 0) return lista;

        int? Col(string nome)
        {
            for (var c = 0; c < t.Columns.Count; c++)
                if (Norm(Texto(t, cab, c)).StartsWith(nome)) return c;
            return null;
        }
        var cCodigo = Col("part number");
        var cSerie = Col("hw serial no");
        var cCaixa = Col("case no");
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

    private static void LerMetadados(DataTable t, int ateLinha, PackingListImportado r)
    {
        for (var i = 0; i < ateLinha; i++)
        {
            for (var c = 0; c < t.Columns.Count; c++)
            {
                var celula = Texto(t, i, c);
                if (string.IsNullOrWhiteSpace(celula)) continue;
                var rotulo = Norm(celula);

                if (r.Fornecedor == null && rotulo.Contains("huawei") && !rotulo.StartsWith("to")) r.Fornecedor = celula.Trim();

                string? Valor() => Seguinte(t, i, c);
                switch (rotulo)
                {
                    case "p/l no.": r.Numero ??= Valor(); break;
                    case "contract no.": r.Contrato ??= Valor(); break;
                    case "invoice no.": r.Fatura ??= Valor(); break;
                    case "p/o no.": r.PurchaseOrder ??= Valor(); break;
                    case "project": r.ProjetoFornecedor ??= Valor(); break;
                    case "date":
                        var d = Valor();
                        if (d != null && DateTime.TryParse(d.Replace('/', '-'), CultureInfo.InvariantCulture, DateTimeStyles.None, out var dt)) r.Data = dt.Date;
                        break;
                }
            }
        }
        r.Contrato1Mais1 = Contrato1Mais1(r.ProjetoFornecedor, r.Data);
    }

    // "Cape Verde CVT 2026-2027 1+1 Package Project" → "1+1_2026". Sem "1+1" no nome: null.
    public static string? Contrato1Mais1(string? projeto, DateTime? data)
    {
        if (string.IsNullOrWhiteSpace(projeto) || !projeto.Contains("1+1")) return null;
        var m = Regex.Match(projeto, @"\b(20\d{2})\b");
        var ano = m.Success ? m.Groups[1].Value : data?.Year.ToString();
        return ano == null ? "1+1" : $"1+1_{ano}";
    }

    private static int EncontrarCabecalho(DataTable t, string nome)
    {
        for (var i = 0; i < t.Rows.Count; i++)
            for (var c = 0; c < t.Columns.Count; c++)
                if (Norm(Texto(t, i, c)) == nome) return i;
        return -1;
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
