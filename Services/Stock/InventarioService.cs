using System.Data;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using ExcelDataReader;

namespace ComparacaoPropostas.Services.Stock;

public class LinhaInventarioImportada
{
    public string Codigo { get; set; } = "";
    public string? Descricao { get; set; }
    public string? Unidade { get; set; }
    public decimal? Quantidade { get; set; }
    public List<string> Series { get; set; } = new();
}

public interface IInventarioExcelService
{
    List<LinhaInventarioImportada> Ler(Stream ficheiro, out List<string> avisos);
}

// Lê a folha de inventário do armazém (Excel .xls/.xlsx): código, descrição, unidade, quantidade
// contada e, se houver, nº de série (uma linha por equipamento ou vários separados por espaço/vírgula).
// Linhas com o mesmo código são somadas.
public class InventarioExcelService : IInventarioExcelService
{
    static InventarioExcelService() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    private static readonly string[] ColQuantidade = new[]
    {
        "quantidade contada", "qtd contada", "contado", "contagem", "qtd física", "qtd fisica", "existência", "existencia", "stock"
    }.Concat(PackingListService.ColQuantidade).ToArray();

    public List<LinhaInventarioImportada> Ler(Stream ficheiro, out List<string> avisos)
    {
        DataSet ds;
        using (var reader = ExcelReaderFactory.CreateReader(ficheiro))
            ds = reader.AsDataSet();
        return Ler(ds, out avisos);
    }

    public static List<LinhaInventarioImportada> Ler(DataSet ds, out List<string> avisos)
    {
        avisos = new List<string>();
        foreach (DataTable t in ds.Tables)
        {
            var cab = Cabecalho(t);
            if (cab < 0) continue;

            int? Col(string[] nomes) => Coluna(t, cab, nomes);
            var cCodigo = Col(PackingListService.ColCodigo)!.Value;
            var cDesc = Col(PackingListService.ColDescricao);
            var cUn = Col(PackingListService.ColUnidade);
            var cQtd = Col(ColQuantidade);
            var cSerie = Col(PackingListService.ColSerie);

            var porCodigo = new Dictionary<string, LinhaInventarioImportada>(StringComparer.OrdinalIgnoreCase);
            for (var i = cab + 1; i < t.Rows.Count; i++)
            {
                var codigo = Texto(t, i, cCodigo)?.Trim();
                if (string.IsNullOrWhiteSpace(codigo) || codigo.StartsWith("total", StringComparison.OrdinalIgnoreCase)) continue;

                var series = (cSerie is int cs ? Texto(t, i, cs) : null)?
                    .Split(new[] { ' ', ',', ';', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList() ?? new();
                var qtd = cQtd is int cq ? Numero(t.Rows[i][cq]) : null;
                // Uma linha por equipamento sem quantidade: conta 1 por número de série.
                if (qtd == null && series.Count > 0) qtd = series.Count;

                if (!porCodigo.TryGetValue(codigo, out var linha))
                {
                    linha = new LinhaInventarioImportada
                    {
                        Codigo = codigo,
                        Descricao = cDesc is int cd ? Texto(t, i, cd)?.Trim() : null,
                        Unidade = cUn is int cu ? Texto(t, i, cu)?.Trim() : null
                    };
                    porCodigo[codigo] = linha;
                }
                if (qtd != null) linha.Quantidade = (linha.Quantidade ?? 0m) + qtd.Value;
                linha.Series.AddRange(series.Where(s => !linha.Series.Contains(s, StringComparer.OrdinalIgnoreCase)));
            }

            if (porCodigo.Count > 0) return porCodigo.Values.ToList();
        }

        avisos.Add("Não foi encontrada a linha de cabeçalho (é preciso uma coluna de código — ex.: \"Código\" ou \"Part Number\").");
        return new List<LinhaInventarioImportada>();
    }

    private static int Cabecalho(DataTable t)
    {
        for (var i = 0; i < Math.Min(t.Rows.Count, 30); i++)
            if (Coluna(t, i, PackingListService.ColCodigo) != null
                && (Coluna(t, i, ColQuantidade) != null || Coluna(t, i, PackingListService.ColSerie) != null))
                return i;
        return -1;
    }

    private static int? Coluna(DataTable t, int linha, string[] nomes)
    {
        var cab = Enumerable.Range(0, t.Columns.Count).Select(c => (c, h: Norm(Texto(t, linha, c)).TrimEnd(':').Trim())).ToList();
        foreach (var (c, h) in cab) if (nomes.Contains(h)) return c;
        foreach (var (c, h) in cab) if (h.Length > 0 && nomes.Any(n => n.Length >= 4 && h.StartsWith(n))) return c;
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

public static class InventarioCalculos
{
    // Ajuste ao fechar: contado − saldo da plataforma. Linhas por contar (vazias) não geram ajuste.
    public static decimal? Ajuste(decimal? contado, decimal saldoSistema)
        => contado is decimal c && c != saldoSistema ? c - saldoSistema : null;

    public static List<string> SepararSeries(string? texto)
        => (texto ?? "").Split(new[] { ' ', ',', ';', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries)
            .Select(s => s.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).ToList();
}
