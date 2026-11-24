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
    // MBB, FBB, INF ENR ou DEI (outros), conforme a Área do armazém.
    public string? AreaTecnica { get; set; }
    // Armazém (SAP): centro, depósito e designação (coluna Observações, ex.: Praia_Projeto).
    public string? Centro { get; set; }
    public string? Deposito { get; set; }
    public string? NomeArmazem { get; set; }
    public List<string> Series { get; set; } = new();
}

public record ArmazemImportado(string? Centro, string Deposito, string? Nome, List<LinhaInventarioImportada> Linhas);

public interface IInventarioExcelService
{
    List<LinhaInventarioImportada> Ler(Stream ficheiro, out List<string> avisos, IReadOnlyCollection<string>? filtroAreas = null, string? deposito = null);
    List<ArmazemImportado> LerPorArmazem(Stream ficheiro, out List<string> avisos, IReadOnlyCollection<string>? filtroAreas = null);
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

    public List<LinhaInventarioImportada> Ler(Stream ficheiro, out List<string> avisos, IReadOnlyCollection<string>? filtroAreas = null, string? deposito = null)
        => Ler(LerDataSet(ficheiro), out avisos, filtroAreas, deposito);

    public List<ArmazemImportado> LerPorArmazem(Stream ficheiro, out List<string> avisos, IReadOnlyCollection<string>? filtroAreas = null)
        => LerPorArmazem(LerDataSet(ficheiro), out avisos, filtroAreas);

    private static DataSet LerDataSet(Stream ficheiro)
    {
        using var reader = ExcelReaderFactory.CreateReader(ficheiro);
        return reader.AsDataSet();
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

    private static readonly string[] ColCentro = { "centro" };
    private static readonly string[] ColDeposito = { "depósito", "deposito", "armazém", "armazem" };
    private static readonly string[] ColNomeArmazem = { "observações", "observacoes", "designação armazém", "localização", "localizacao" };

    // Uma linha do Excel, antes de juntar por código.
    private record Registo(string Folha, bool FolhaTemArea, string Codigo, string? Descricao, string? Unidade, decimal? Quantidade,
        List<string> Series, string? Area, string? Centro, string? Deposito, string? NomeArmazem);

    // Lê todas as folhas com cabeçalho. Sem filtro de áreas, só a primeira folha com linhas (como antes).
    // Com filtro: nas folhas com a coluna Área entra a linha cuja Área passa no filtro; nas folhas sem
    // Área entra o código que, noutra folha do mesmo ficheiro, está numa área do filtro (herda essa área).
    private static List<Registo> LerRegistos(DataSet ds, List<string> avisos, IReadOnlyCollection<string>? filtroAreas, out bool encontrouCabecalho)
    {
        var filtrar = filtroAreas is { Count: > 0 };
        var todos = new List<Registo>();
        encontrouCabecalho = false;

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
            var cCentro = Col(ColCentro);
            var cDep = Col(ColDeposito);
            var cNome = Col(ColNomeArmazem);

            var antes = todos.Count;
            for (var i = cab + 1; i < t.Rows.Count; i++)
            {
                var codigo = Texto(t, i, cCodigo)?.Trim();
                if (string.IsNullOrWhiteSpace(codigo) || codigo.StartsWith("total", StringComparison.OrdinalIgnoreCase)) continue;

                var series = (cSerie is int cs ? Texto(t, i, cs) : null)?
                    .Split(new[] { ' ', ',', ';', '\n', '\r', '\t' }, StringSplitOptions.RemoveEmptyEntries).Select(s => s.Trim()).ToList() ?? new();
                var qtd = cQtd is int cq ? Numero(t.Rows[i][cq]) : null;
                // Uma linha por equipamento sem quantidade: conta 1 por número de série.
                if (qtd == null && series.Count > 0) qtd = series.Count;
                // A coluna da unidade só conta se tiver texto (no SAP "Utiliz.livre" pode trazer números).
                var unidade = cUn is int cu && Numero(t.Rows[i][cu]) == null ? Vazio(Texto(t, i, cu)) : null;

                todos.Add(new Registo(t.TableName, cArea != null, codigo,
                    cDesc is int cd ? Vazio(Texto(t, i, cd)) : null, unidade, qtd, series,
                    cArea is int ca ? Vazio(Texto(t, i, ca)) : null,
                    cCentro is int cc ? Vazio(Texto(t, i, cc)) : null,
                    cDep is int cdp ? Vazio(Texto(t, i, cdp)) : null,
                    cNome is int cn ? Vazio(Texto(t, i, cn)) : null));
            }
            // Sem filtro mantém-se o comportamento antigo: a primeira folha com linhas é a contagem.
            if (!filtrar && todos.Count > antes) break;
        }

        if (!filtrar) return todos;

        // Área de cada código nas folhas que têm a coluna Área (só as que passam no filtro).
        var areaPorCodigo = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in todos.Where(r => r.FolhaTemArea && AreaPassa(r.Area, filtroAreas)))
            areaPorCodigo.TryAdd(r.Codigo, r.Area!);

        var resultado = new List<Registo>();
        var ignoradas = new Dictionary<string, int>();
        foreach (var r in todos)
        {
            if (r.FolhaTemArea)
            {
                if (AreaPassa(r.Area, filtroAreas)) resultado.Add(r);
                else ignoradas[r.Folha] = ignoradas.GetValueOrDefault(r.Folha) + 1;
            }
            else if (areaPorCodigo.TryGetValue(r.Codigo, out var area))
                resultado.Add(r with { Area = area });
            else ignoradas[r.Folha] = ignoradas.GetValueOrDefault(r.Folha) + 1;
        }

        var semArea = todos.Where(r => !r.FolhaTemArea).Select(r => r.Folha).Distinct().ToList();
        avisos.Add($"Filtro {string.Join(", ", filtroAreas!)}: {resultado.Count} linha(s) aceites; "
            + string.Join("; ", todos.Select(r => r.Folha).Distinct().Select(fl =>
                $"\"{fl}\" {resultado.Count(r => r.Folha == fl)} aceite(s), {ignoradas.GetValueOrDefault(fl)} de outras áreas")) + ".");
        if (semArea.Count > 0)
            avisos.Add($"Folha(s) sem coluna Área ({string.Join(", ", semArea.Select(s => $"\"{s}\""))}): entram só os códigos que noutra folha estão em {string.Join(", ", filtroAreas!)}, com a mesma área.");
        if (semArea.Count > 0 && areaPorCodigo.Count == 0)
            avisos.Add("Nenhuma folha do ficheiro tem a coluna Área: não há como saber que linhas são destes projetos.");
        return resultado;
    }

    private static List<LinhaInventarioImportada> Juntar(IEnumerable<Registo> registos)
    {
        var porCodigo = new Dictionary<string, LinhaInventarioImportada>(StringComparer.OrdinalIgnoreCase);
        foreach (var r in registos)
        {
            if (!porCodigo.TryGetValue(r.Codigo, out var linha))
            {
                linha = new LinhaInventarioImportada
                {
                    Codigo = r.Codigo, Descricao = r.Descricao, Unidade = r.Unidade,
                    Centro = r.Centro, Deposito = r.Deposito, NomeArmazem = r.NomeArmazem
                };
                porCodigo[r.Codigo] = linha;
            }
            if (r.Quantidade != null) linha.Quantidade = (linha.Quantidade ?? 0m) + r.Quantidade.Value;
            linha.Series.AddRange(r.Series.Where(s => !linha.Series.Contains(s, StringComparer.OrdinalIgnoreCase)));
            linha.AreaTecnica ??= AreasTecnicas.De(r.Area);
            if (r.Area != null)
                linha.AreaArmazem = linha.AreaArmazem == null ? r.Area
                    : linha.AreaArmazem.Split(" · ").Contains(r.Area, StringComparer.OrdinalIgnoreCase) ? linha.AreaArmazem : $"{linha.AreaArmazem} · {r.Area}";
        }
        return porCodigo.Values.ToList();
    }

    // Linhas para um inventário. deposito: só as linhas desse armazém (as linhas sem depósito também entram).
    public static List<LinhaInventarioImportada> Ler(DataSet ds, out List<string> avisos, IReadOnlyCollection<string>? filtroAreas = null, string? deposito = null)
    {
        avisos = new List<string>();
        var registos = LerRegistos(ds, avisos, filtroAreas, out var encontrou);
        if (!encontrou)
        {
            avisos.Add("Não foi encontrada a linha de cabeçalho (é preciso uma coluna de código — ex.: \"Código\", \"Material\" ou \"Part Number\").");
            return new List<LinhaInventarioImportada>();
        }
        if (deposito != null)
        {
            var outros = registos.Count(r => r.Deposito != null && !r.Deposito.Equals(deposito, StringComparison.OrdinalIgnoreCase));
            registos = registos.Where(r => r.Deposito == null || r.Deposito.Equals(deposito, StringComparison.OrdinalIgnoreCase)).ToList();
            if (outros > 0) avisos.Add($"{outros} linha(s) de outros armazéns ignoradas (este inventário é do armazém {deposito}).");
        }
        return Juntar(registos);
    }

    // Um grupo de linhas por armazém (depósito), para criar um inventário por armazém.
    public static List<ArmazemImportado> LerPorArmazem(DataSet ds, out List<string> avisos, IReadOnlyCollection<string>? filtroAreas = null)
    {
        avisos = new List<string>();
        var registos = LerRegistos(ds, avisos, filtroAreas, out var encontrou);
        if (!encontrou)
        {
            avisos.Add("Não foi encontrada a linha de cabeçalho (é preciso uma coluna de código — ex.: \"Código\", \"Material\" ou \"Part Number\").");
            return new List<ArmazemImportado>();
        }
        var semDeposito = registos.Count(r => r.Deposito == null);
        if (semDeposito > 0) avisos.Add($"{semDeposito} linha(s) sem depósito ignoradas.");
        return registos.Where(r => r.Deposito != null)
            .GroupBy(r => r.Deposito!, StringComparer.OrdinalIgnoreCase)
            .Select(g => new ArmazemImportado(
                g.Select(r => r.Centro).FirstOrDefault(c => c != null),
                g.Key.ToUpperInvariant(),
                g.Select(r => r.NomeArmazem).FirstOrDefault(n => n != null),
                Juntar(g)))
            .OrderBy(a => a.Deposito)
            .ToList();
    }

    private static string? Vazio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

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
