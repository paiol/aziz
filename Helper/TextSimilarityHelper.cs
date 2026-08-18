using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace ComparacaoPropostas.Helper;

// Similaridade de texto simples (Levenshtein normalizado, sem acentos/caixa)
// usada para sugerir a que item pedido um nome do Excel do fornecedor pode
// corresponder, quando o nome não bate certo (ex.: "Poste 9m" vs "Poste de 9 metros").
public static class TextSimilarityHelper
{
    public static double Similaridade(string a, string b)
    {
        var na = Normalizar(a);
        var nb = Normalizar(b);

        if (na.Length == 0 && nb.Length == 0) return 1;

        var distancia = Levenshtein(na, nb);
        var maxLen = Math.Max(na.Length, nb.Length);
        return maxLen == 0 ? 1 : 1.0 - (double)distancia / maxLen;
    }

    private static string Normalizar(string texto)
    {
        var semAcentos = RemoverAcentos(texto.Trim().ToLowerInvariant());
        return Regex.Replace(semAcentos, @"\s+", " ");
    }

    private static string RemoverAcentos(string texto)
    {
        var normalizado = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder();
        foreach (var c in normalizado)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static int Levenshtein(string a, string b)
    {
        var dp = new int[a.Length + 1, b.Length + 1];
        for (var i = 0; i <= a.Length; i++) dp[i, 0] = i;
        for (var j = 0; j <= b.Length; j++) dp[0, j] = j;

        for (var i = 1; i <= a.Length; i++)
        {
            for (var j = 1; j <= b.Length; j++)
            {
                var custo = a[i - 1] == b[j - 1] ? 0 : 1;
                dp[i, j] = Math.Min(Math.Min(dp[i - 1, j] + 1, dp[i, j - 1] + 1), dp[i - 1, j - 1] + custo);
            }
        }

        return dp[a.Length, b.Length];
    }
}
