using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Services.Stock;

public record SugestaoSap(MaterialStock Material, decimal Pontuacao, List<string> EmComum, bool ModeloIgual, bool CodigoNaDescricao = false)
{
    public string Confianca => Pontuacao >= 0.7m ? "Alta" : Pontuacao >= 0.45m ? "Média" : "Baixa";
}

// Para um material que chegou só com o código do fornecedor, procura no catálogo os materiais SAP que
// podem ser o mesmo: código do fornecedor escrito na descrição SAP, modelo igual (ex.: ESM-48150B1), medidas iguais (16mm2, 48V, 150Ah) e palavras da
// descrição em comum (inglês do fornecedor traduzido para as palavras do SAP). A DEI confirma.
public static class SugestoesSap
{
    public const decimal Minimo = 0.3m;

    private static readonly HashSet<string> Ignorar = new(StringComparer.Ordinal)
    {
        "DE", "DA", "DO", "DAS", "DOS", "PARA", "COM", "SEM", "EM", "E", "A", "O", "AS", "OS", "C", "P",
        "THE", "FOR", "OF", "AND", "WITH", "WITHOUT", "TO", "IN", "NA", "UNIT", "UNID", "UN", "PCS", "PC",
        "CCC", "CE", "UL", "VDE", "ROHS", "IEC"
    };

    // Inglês (fornecedor) → palavra usada no SAP.
    private static readonly Dictionary<string, string> Traducao = new(StringComparer.Ordinal)
    {
        ["CABLE"] = "CABO", ["POWER"] = "ENERGIA", ["ENERGY"] = "ENERGIA", ["BATTERY"] = "BATERIA", ["BATTERIES"] = "BATERIA",
        ["YELLOW"] = "AMARELO", ["GREEN"] = "VERDE", ["BLACK"] = "PRETO", ["RED"] = "VERMELHO", ["BLUE"] = "AZUL",
        ["WHITE"] = "BRANCO", ["GREY"] = "CINZENTO", ["GRAY"] = "CINZENTO",
        ["FIBER"] = "FIBRA", ["FIBRE"] = "FIBRA", ["FIBERS"] = "FIBRA", ["FIBRAS"] = "FIBRA", ["OPTICAL"] = "OTICA", ["OPTICA"] = "OTICA",
        ["SCREEN"] = "ECRA", ["DISPLAY"] = "ECRA", ["CABINET"] = "ARMARIO", ["RECTIFIER"] = "RETIFICADOR",
        ["MODULE"] = "MODULO", ["POLE"] = "POSTE", ["METER"] = "METRO", ["METERS"] = "METRO", ["METROS"] = "METRO",
        ["ANTENNA"] = "ANTENA", ["BRACKET"] = "SUPORTE", ["SUPPORT"] = "SUPORTE", ["CONNECTOR"] = "CONECTOR",
        ["SWITCH"] = "SWITCH", ["BOARD"] = "PLACA", ["FAN"] = "VENTILADOR", ["BREAKER"] = "DISJUNTOR",
        ["CIRCUIT"] = "CIRCUITO", ["GROUND"] = "TERRA", ["GROUNDING"] = "TERRA", ["EARTH"] = "TERRA",
        ["OUTDOOR"] = "EXTERIOR", ["INDOOR"] = "INTERIOR", ["STORAGE"] = "ARMAZENAMENTO", ["LITHIUM"] = "LITIO",
        ["PATCH"] = "PATCH", ["CORD"] = "CORD", ["PIGTAIL"] = "PIGTAIL", ["SPLITTER"] = "SPLITTER"
    };

    public static HashSet<string> Palavras(string? texto)
    {
        var res = new HashSet<string>(StringComparer.Ordinal);
        // "16mm^2" / "16 mm2" → 16MM2: junta número e unidade.
        var t = System.Text.RegularExpressions.Regex.Replace(texto ?? "", @"(\d)\s*(mm\^?2|mm²)", "$1MM2", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        foreach (var p in NomesMaterial.Normalizar(t).Split(' ', StringSplitOptions.RemoveEmptyEntries))
        {
            if (Ignorar.Contains(p)) continue;
            if (p.Length < 2 && !p.Any(char.IsDigit)) continue;
            res.Add(Traducao.GetValueOrDefault(p, p));
        }
        return res;
    }

    private static string Compacto(string? s) => NomesMaterial.Normalizar(s).Replace(" ", "");

    private static int Peso(string palavra) => palavra.Any(char.IsDigit) ? 2 : 1;

    public static decimal Pontuar(MaterialStock fornecedor, MaterialStock sap, out List<string> emComum, out bool modeloIgual)
    {
        var a = Palavras($"{fornecedor.Descricao} {fornecedor.Modelo}");
        var b = Palavras($"{sap.Descricao} {sap.Modelo}");
        emComum = a.Intersect(b).OrderByDescending(Peso).ThenBy(p => p).ToList();
        var comum = emComum.Sum(Peso);
        var pa = a.Sum(Peso);
        var pb = b.Sum(Peso);
        decimal pont = 0m;
        if (pa > 0 && pb > 0)
        {
            var dice = 2m * comum / (pa + pb);
            var cobreSap = (decimal)comum / pb;   // quanto da descrição SAP (curta) aparece no fornecedor
            pont = 0.5m * dice + 0.5m * cobreSap;
        }

        // Medidas da mesma unidade mas diferentes (10MM2 vs 16MM2, 48V vs 12V): provavelmente outro material.
        if (MedidasDiferentes(a, b)) pont *= 0.6m;

        // Modelo do fornecedor escrito na descrição / modelo SAP (ex.: ESM-48150B1).
        var modelo = Compacto(fornecedor.Modelo);
        modeloIgual = modelo.Length >= 5 && (Compacto(sap.Descricao).Contains(modelo) || Compacto(sap.Modelo) == modelo);
        if (modeloIgual) pont = Math.Max(pont, 0.9m);
        return Math.Round(Math.Min(pont, 1m), 2);
    }

    private static readonly System.Text.RegularExpressions.Regex Medida = new(@"^(d+(?:d)*)(MM2|MM|KVA|KW|W|V|AH|A|M)$");

    private static bool MedidasDiferentes(HashSet<string> a, HashSet<string> b)
    {
        static ILookup<string, string> PorUnidade(HashSet<string> s) => s.Select(p => Medida.Match(p)).Where(m => m.Success)
            .ToLookup(m => m.Groups[2].Value, m => m.Groups[1].Value);
        var ua = PorUnidade(a);
        var ub = PorUnidade(b);
        return ua.Select(g => g.Key).Intersect(ub.Select(g => g.Key)).Any(u => !ua[u].Intersect(ub[u]).Any());
    }

    // Sugestão só com palavras genéricas em comum (ex.: só "CABO") não serve.
    private static bool Relevante(SugestaoSap s) =>
        s.CodigoNaDescricao || s.ModeloIgual || s.Pontuacao >= 0.4m || s.EmComum.Any(p => p.Length >= 2 && p.Any(char.IsDigit));

    // O SAP escreve muitas vezes o código do fornecedor na descrição (ex.: "25030429-PwCable450/750V,16mm2").
    public static bool CodigoNaDescricao(MaterialStock fornecedor, MaterialStock sap)
    {
        var desc = Compacto(sap.Descricao);
        return fornecedor.CodigosFornecedor.Select(c => c.Codigo).Append(fornecedor.Codigo)
            .Select(Compacto).Where(c => c.Length >= 6).Distinct()
            .Any(desc.Contains);
    }

    public static List<SugestaoSap> Sugerir(MaterialStock fornecedor, IEnumerable<MaterialStock> catalogoSap, int maximo = 3)
        => catalogoSap
            .Where(s => s.Id != fornecedor.Id && !s.CodigoProvisorio)
            .Select(s =>
            {
                var pont = Pontuar(fornecedor, s, out var comum, out var modelo);
                var codigo = CodigoNaDescricao(fornecedor, s);
                return new SugestaoSap(s, codigo ? 1m : pont, comum, modelo, codigo);
            })
            .Where(s => s.Pontuacao >= Minimo && Relevante(s))
            .OrderByDescending(s => s.Pontuacao)
            .Take(maximo)
            .ToList();
}
