using System.Text.Json;

namespace ComparacaoPropostas.Helper;

// Gera a configuração Chart.js (JSON) usada por wwwroot/js/graficos.js:
// <canvas class="js-grafico" data-grafico="@GraficoHelper.Barras(...)"></canvas>
public static class GraficoHelper
{
    private static readonly JsonSerializerOptions Opcoes = new() { PropertyNamingPolicy = JsonNamingPolicy.CamelCase };

    public record Serie(string Label, IEnumerable<decimal> Valores, string? Tipo = null);

    public static string Barras(IEnumerable<string> etiquetas, IEnumerable<Serie> series, bool horizontal = false, bool empilhado = false)
    {
        object? escalas = empilhado ? new { x = new { stacked = true }, y = new { stacked = true } } : null;
        return JsonSerializer.Serialize(new
        {
            type = "bar",
            data = new
            {
                labels = etiquetas.ToList(),
                datasets = series.Select(s => s.Tipo == null
                    ? (object)new { label = s.Label, data = s.Valores.ToList() }
                    : new { label = s.Label, data = s.Valores.ToList(), type = s.Tipo }).ToList()
            },
            options = new { indexAxis = horizontal ? "y" : "x", scales = escalas }
        }, Opcoes);
    }

    public static string Barras(IEnumerable<string> etiquetas, string label, IEnumerable<decimal> valores, bool horizontal = false)
        => Barras(etiquetas, new[] { new Serie(label, valores) }, horizontal);

    public static string Circular(IEnumerable<string> etiquetas, IEnumerable<decimal> valores)
        => JsonSerializer.Serialize(new
        {
            type = "doughnut",
            data = new { labels = etiquetas.ToList(), datasets = new[] { new { data = valores.ToList() } } }
        }, Opcoes);

    public static string Linhas(IEnumerable<string> etiquetas, IEnumerable<Serie> series)
        => JsonSerializer.Serialize(new
        {
            type = "line",
            data = new
            {
                labels = etiquetas.ToList(),
                datasets = series.Select(s => new { label = s.Label, data = s.Valores.ToList() }).ToList()
            }
        }, Opcoes);
}
