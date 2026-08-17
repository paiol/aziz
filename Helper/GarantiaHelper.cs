using System.Globalization;
using System.Text.RegularExpressions;

namespace ComparacaoPropostas.Helper;

public static class GarantiaHelper
{
    // Extrai o período de garantia em meses de um texto livre (ex: "24 meses", "2 anos", "12").
    // Sem unidade reconhecida, assume-se meses. Devolve null quando não há nenhum número.
    public static int? ParseParaMeses(string? garantia)
    {
        if (string.IsNullOrWhiteSpace(garantia)) return null;

        var match = Regex.Match(garantia, @"(\d+(?:[.,]\d+)?)");
        if (!match.Success) return null;

        var valorTexto = match.Groups[1].Value.Replace(",", ".");
        if (!decimal.TryParse(valorTexto, NumberStyles.Number, CultureInfo.InvariantCulture, out var valor)) return null;
        if (valor <= 0) return null;

        var textoLower = garantia.ToLowerInvariant();
        var meses = textoLower.Contains("ano") ? valor * 12 : valor;

        return (int)Math.Round(meses, MidpointRounding.AwayFromZero);
    }
}
