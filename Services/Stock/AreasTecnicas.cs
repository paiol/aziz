namespace ComparacaoPropostas.Services.Stock;

// Área técnica do material no armazém (dentro da área DEP — ex.: DEP-INF), tirada da coluna "Área"
// do ficheiro do SAP do armazém:
//   DEI_TORRE → MBB · DEI_FTTH e AIRPON → FBB · ENERGIA → INF ENR · outros DEI → DEI (outros).
public static class AreasTecnicas
{
    public const string Mbb = "MBB";
    public const string Fbb = "FBB";
    public const string InfEnr = "INF ENR";
    public const string DeiOutros = "DEI (outros)";

    public static readonly string[] Todas = { Mbb, Fbb, InfEnr, DeiOutros };

    private static readonly (string Palavra, string Area)[] Regras =
    {
        ("TORRE", Mbb),
        ("FTTH", Fbb),
        ("AIRPON", Fbb),
        ("ENERGIA", InfEnr)
    };

    // Primeira palavra reconhecida no texto (ex.: "DEI_TORRE E ENERGIA" → MBB; "RAM, ENERGIA" → INF ENR).
    public static string? De(string? areaArmazem)
    {
        var palavras = NomesMaterial.Normalizar(areaArmazem).Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var p in palavras)
            foreach (var (palavra, area) in Regras)
                if (p == palavra) return area;
        return palavras.Contains("DEI") ? DeiOutros : null;
    }
}
