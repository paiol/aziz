using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ComparacaoPropostas.Services.Airtable;

public interface IAirtableClient
{
    /// <summary>
    /// Cria ou atualiza registos numa tabela, usando o campo "ID App" como chave (performUpsert).
    /// Devolve chave → ID do registo no Airtable (rec...).
    /// </summary>
    Task<IReadOnlyDictionary<string, string>> UpsertAsync(string tabelaId, IReadOnlyList<RegistoAirtable> registos, CancellationToken ct);

    /// <summary>Apaga registos. Registos que já não existem no Airtable são ignorados.</summary>
    Task ApagarAsync(string tabelaId, IReadOnlyList<string> recordIds, CancellationToken ct);
}

public class AirtableException : Exception
{
    public HttpStatusCode Status { get; }
    public AirtableException(HttpStatusCode status, string mensagem) : base(mensagem) => Status = status;
}

/// <summary>
/// Cliente mínimo da Airtable Web API (https://airtable.com/developers/web/api).
/// Respeita o limite de 5 pedidos/segundo por base e repete após 429 / erros 5xx.
/// </summary>
public class AirtableClient : IAirtableClient
{
    private const string ApiUrl = "https://api.airtable.com/v0/";
    private const int MaxRegistosPorPedido = 10; // limite da API

    // Partilhado por todas as instâncias: o limite é por base, não por pedido HTTP.
    private static readonly SemaphoreSlim Ritmo = new(1, 1);
    private static DateTime _ultimoPedido = DateTime.MinValue;
    private static readonly TimeSpan IntervaloMinimo = TimeSpan.FromMilliseconds(250);

    private readonly HttpClient _http;
    private readonly IOptionsMonitor<AirtableOptions> _options;
    private readonly ILogger<AirtableClient> _logger;

    public AirtableClient(HttpClient http, IOptionsMonitor<AirtableOptions> options, ILogger<AirtableClient> logger)
    {
        _http = http;
        _options = options;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, string>> UpsertAsync(string tabelaId, IReadOnlyList<RegistoAirtable> registos, CancellationToken ct)
    {
        var resultado = new Dictionary<string, string>();
        if (registos.Count == 0) return resultado;

        var campoChave = AirtableSchema.CampoIdApp(tabelaId);

        foreach (var lote in registos.Chunk(MaxRegistosPorPedido))
        {
            var corpo = new
            {
                performUpsert = new { fieldsToMergeOn = new[] { campoChave } },
                typecast = true,
                returnFieldsByFieldId = true,
                records = lote.Select(r =>
                {
                    var campos = new Dictionary<string, object?>(r.Campos) { [campoChave] = r.Chave };
                    return new { fields = campos };
                }).ToArray()
            };

            using var resposta = await EnviarAsync(
                () => new HttpRequestMessage(HttpMethod.Patch, UrlTabela(tabelaId)) { Content = JsonContent.Create(corpo) }, ct);
            await GarantirSucessoAsync(resposta, $"upsert em {tabelaId}", ct);

            await using var stream = await resposta.Content.ReadAsStreamAsync(ct);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            foreach (var rec in json.RootElement.GetProperty("records").EnumerateArray())
            {
                var id = rec.GetProperty("id").GetString();
                if (id != null
                    && rec.TryGetProperty("fields", out var campos)
                    && campos.TryGetProperty(campoChave, out var chave)
                    && chave.ValueKind == JsonValueKind.String)
                {
                    resultado[chave.GetString()!] = id;
                }
            }
        }

        return resultado;
    }

    public async Task ApagarAsync(string tabelaId, IReadOnlyList<string> recordIds, CancellationToken ct)
    {
        foreach (var lote in recordIds.Distinct().Chunk(MaxRegistosPorPedido))
        {
            using var resposta = await EnviarAsync(
                () => new HttpRequestMessage(HttpMethod.Delete, UrlApagar(tabelaId, lote)), ct);

            if (resposta.IsSuccessStatusCode) continue;

            // Um registo inexistente faz falhar o lote inteiro: apaga um a um e ignora os que já não existem.
            var falhaPorRegistoInexistente = resposta.StatusCode is HttpStatusCode.NotFound
                or HttpStatusCode.UnprocessableEntity or HttpStatusCode.Forbidden;
            if (falhaPorRegistoInexistente && lote.Length > 1)
            {
                foreach (var id in lote)
                {
                    using var r = await EnviarAsync(() => new HttpRequestMessage(HttpMethod.Delete, UrlApagar(tabelaId, new[] { id })), ct);
                    if (r.IsSuccessStatusCode || r.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden) continue;
                    await GarantirSucessoAsync(r, $"apagar {id} em {tabelaId}", ct);
                }
                continue;
            }

            if (resposta.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden) continue;
            await GarantirSucessoAsync(resposta, $"apagar em {tabelaId}", ct);
        }
    }

    private async Task<HttpResponseMessage> EnviarAsync(Func<HttpRequestMessage> criarPedido, CancellationToken ct)
    {
        var opcoes = _options.CurrentValue;
        if (string.IsNullOrWhiteSpace(opcoes.Token))
            throw new InvalidOperationException("Token do Airtable não configurado (Airtable:Token).");

        for (var tentativa = 1; ; tentativa++)
        {
            await Ritmo.WaitAsync(ct);
            try
            {
                var espera = _ultimoPedido + IntervaloMinimo - DateTime.UtcNow;
                if (espera > TimeSpan.Zero) await Task.Delay(espera, ct);
                _ultimoPedido = DateTime.UtcNow;
            }
            finally
            {
                Ritmo.Release();
            }

            var pedido = criarPedido();
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", opcoes.Token);

            var resposta = await _http.SendAsync(pedido, ct);

            if (resposta.StatusCode == HttpStatusCode.TooManyRequests && tentativa < 4)
            {
                // A Airtable pede 30 s de pausa depois de um 429.
                _logger.LogWarning("Airtable: limite de pedidos atingido, a aguardar 30 s.");
                resposta.Dispose();
                await Task.Delay(TimeSpan.FromSeconds(30), ct);
                continue;
            }

            if ((int)resposta.StatusCode >= 500 && tentativa < 3)
            {
                resposta.Dispose();
                await Task.Delay(TimeSpan.FromSeconds(2 * tentativa), ct);
                continue;
            }

            return resposta;
        }
    }

    private static async Task GarantirSucessoAsync(HttpResponseMessage resposta, string operacao, CancellationToken ct)
    {
        if (resposta.IsSuccessStatusCode) return;

        var corpo = await resposta.Content.ReadAsStringAsync(ct);
        if (corpo.Length > 600) corpo = corpo[..600] + "…";
        throw new AirtableException(resposta.StatusCode,
            $"Airtable respondeu {(int)resposta.StatusCode} ao {operacao}: {corpo}");
    }

    private string UrlTabela(string tabelaId) => $"{ApiUrl}{_options.CurrentValue.BaseId}/{tabelaId}";

    private string UrlApagar(string tabelaId, IEnumerable<string> ids)
        => UrlTabela(tabelaId) + "?" + string.Join("&", ids.Select(id => "records%5B%5D=" + Uri.EscapeDataString(id)));
}
