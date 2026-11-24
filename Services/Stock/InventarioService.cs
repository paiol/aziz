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
    // Projeto / área indicada pelo armazém (coluna "Área"), ex.: DEI_FTTH, AIRPON, ENERGIA.
    public string? AreaArmazem { get; set; }
    public List<string> Series { get; set; } = new();
}

public interface IInventarioExcelService
{
    List<LinhaInventarioImportada> Ler(Stream ficheiro, out List<string> avisos, IReadOnlyCollection<string>? filtroAreas = null);
}

// Lê a folha de inventário do armazém (Excel .xls/.xlsx): código, descrição, unidade, quantidade
// contada e, se houver, nº de série (uma linha por equipamento ou vários separados por espaço/vírgula).
// Linhas com o mesmo código são somadas.
public class InventarioExcelService : IInventarioExcelService
{
    static InventarioExcelService() => Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);

    // Formato SAP do armazém: Material | TxtBreveMaterial | Contagem Total | Utiliz.livre (unidade) | Área.
    private static readonly string[] ColCodigo = PackingListService.ColCodigo.Concat(new[] { "material", "nº material", "n.º material" }).ToArray();
    private static readonly string[] ColDescricao = PackingListService.ColDescricao.Concat(new[] { "txtbrevematerial", "texto breve material", "texto breve" }).ToArray();
    private static readonly string[] ColUnidade = PackingListService.ColUnidade.Concat(new[] { "utiliz.livre", "umb" }).ToArray();
    private static readonly string[] ColArea = { "área", "area", "projeto", "projecto" };

    private static readonly string[] ColQuantidade = new[]
    {
        "quantidade contada", "qtd contada", "contado", "contagem", "contagem total", "qtd física", "qtd fisica", "existência", "existencia", "stock"
    }.Concat(PackingListService.ColQuantidade).ToArray();

    public List<LinhaInventarioImportada> Ler(Stream ficheiro, out List<string> avisos, IReadOnlyCollection<string>? filtroAreas = null)
    {
        DataSet ds;
        using (var reader = ExcelReaderFactory.CreateReader(ficheiro))
            ds = reader.AsDataSet();
        return Ler(ds, out avisos, filtroAreas);
    }

    // A linha é de uma das áreas pedidas? Compara palavra a palavra, sem maiúsculas/acentos:
    // "DEI_FTTH", "DEI PROJETO", "RAM, ENERGIA" e "SPARE_ENERGIA_DPSBLADE" passam no filtro DEI/ENERGIA.
    public static bool AreaPassa(string? areaArmazem, IReadOnlyCollection<string>? filtroAreas)
    {
        if (filtroAreas == null || filtroAreas.Count == 0) return true;
        var palavras = NomesMaterial.Normalizar(areaArmazem).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return filtroAreas.Select(NomesMaterial.Normalizar).Where(f => f.Length > 0).Any(f => palavras.Contains(f));
    }

    public static List<string> SepararFiltro(string? texto) =>
        (texto ?? "").Split(new[] { ',', ';', '/', '\n' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).Where(s => s.Length > 0).ToList();

    public static List<LinhaInventarioImportada> Ler(DataSet ds, out List<string> avisos, IReadOnlyCollection<string>? filtroAreas = null)
    {
        avisos = new List<string>();
        var filtrar = filtroAreas is { Count: > 0 };
        var porCodigo = new Dictionary<string, LinhaInventarioImportada>(StringComparer.OrdinalIgnoreCase);
        var folhasLidas = new List<string>();
        var ignoradasArea = 0;
        var encontrouCabecalho = false;

        foreach (DataTable t in ds.Tables)
        {
            var cab = Cabecalho(t);
            if (cab < 0) continue;
            encontrouCabecalho = true;

            int? Col(string[] nomes) => Coluna(t, cab, nomes);
            var cCodigo = Col(ColCodigo)!.Value;
            var cDesc = Col(ColDescricao);
            var cUn = Col(ColUnidade);
            var cQtd = Col(ColQuantidade);
            var cSerie = Col(PackingListService.ColSerie);
            var cArea = Col(ColArea);

            // Com filtro de áreas, só servem as folhas que dizem a área de cada linha.
            if (filtrar && cArea == null)
            {
                avisos.Add($"Folha \"{t.TableName}\" ignorada: não tem a coluna Área para filtrar ({string.Join(", ", filtroAreas!)}).");
                continue;
            }

            var linhasFolha = 0;
            for (var i = cab + 1; i < t.Rows.Count; i++)
            {
                var codigo = Texto(t, i, cCodigo)?.Trim();
                if (string.IsNullOrWhiteSpace(codigo) || codigo.StartsWith("total", StringComparison.OrdinalIgnoreCase)) continue;

                var area = cArea is int ca ? Texto(t, i, ca)?.Trim() : null;
                if (filtrar && !AreaPassa(area, filtroAreas))
                {
                    ignoradasArea++;
                    continue;
                }

                var series = (cSerie is int cs ? Texto(t, i, cs) : null)?
                    .Split(new[] { ' ', ',', ';', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList() ?? new();
                var qtd = cQtd is int cq ? Numero(t.Rows[i][cq]) : null;
                // Uma linha por equipamento sem quantidade: conta 1 por número de série.
                if (qtd == null && series.Count > 0) qtd = series.Count;

                if (!porCodigo.TryGetValue(codigo, out var linha))
                {
                    // A coluna da unidade só conta se tiver texto (no SAP "Utiliz.livre" pode trazer números).
                    var unidade = cUn is int cu && Numero(t.Rows[i][cu]) == null ? Texto(t, i, cu)?.Trim() : null;
                    linha = new LinhaInventarioImportada
                    {
                        Codigo = codigo,
                        Descricao = cDesc is int cd ? Texto(t, i, cd)?.Trim() : null,
                        Unidade = string.IsNullOrEmpty(unidade) ? null : unidade
                    };
                    porCodigo[codigo] = linha;
                }
                if (qtd != null) linha.Quantidade = (linha.Quantidade ?? 0m) + qtd.Value;
                linha.Series.AddRange(series.Where(s => !linha.Series.Contains(s, StringComparer.OrdinalIgnoreCase)));
                if (!string.IsNullOrWhiteSpace(area))
                    linha.AreaArmazem = linha.AreaArmazem == null ? area
                        : linha.AreaArmazem.Split(" · ").Contains(area, StringComparer.OrdinalIgnoreCase) ? linha.AreaArmazem : $"{linha.AreaArmazem} · {area}";
                linhasFolha++;
            }

            if (linhasFolha > 0) folhasLidas.Add(t.TableName);
            // Sem filtro mantém-se o comportamento antigo: a primeira folha com linhas é a contagem.
            if (!filtrar && porCodigo.Count > 0) break;
        }

        if (!encontrouCabecalho)
        {
            avisos.Add("Não foi encontrada a linha de cabeçalho (é preciso uma coluna de código — ex.: \"Código\", \"Material\" ou \"Part Number\").");
            return new List<LinhaInventarioImportada>();
        }
        if (filtrar)
            avisos.Insert(0, $"Filtro {string.Join(", ", filtroAreas!)}: {porCodigo.Count} material(is) da(s) folha(s) {string.Join(", ", folhasLidas.Select(f => $"\"{f}\""))}; {ignoradasArea} linha(s) de outras áreas (ou sem área) ignoradas.");
        return porCodigo.Values.ToList();
    }

    private static int Cabecalho(DataTable t)
    {
        for (var i = 0; i < Math.Min(t.Rows.Count, 30); i++)
            if (Coluna(t, i, ColCodigo) != null
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
