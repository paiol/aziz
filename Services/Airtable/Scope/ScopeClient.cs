using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Options;

namespace ComparacaoPropostas.Services.Airtable.Scope;

public interface IScopeClient
{
    /// <summary>Cria ou atualiza (pelo campo ID App). Devolve chave → ID do registo (rec…).</summary>
    Task<IReadOnlyDictionary<string, string>> UpsertAsync(IReadOnlyList<(string Chave, Dictionary<string, object?> Campos)> registos, CancellationToken ct);

    Task ApagarAsync(IReadOnlyList<string> recordIds, CancellationToken ct);
}

/// <summary>
/// Cliente mínimo da Airtable Web API para a base SCOPE. Usa os NOMES dos campos, para o mesmo
/// código servir a base de teste e a base real (desde que os campos tenham os mesmos nomes).
/// Respeita o limite de 5 pedidos/segundo por base e repete após 429 / erros 5xx.
/// </summary>
public class ScopeClient : IScopeClient
{
    private const string ApiUrl = "https://api.airtable.com/v0/";
    private const int MaxRegistosPorPedido = 10;

    private static readonly SemaphoreSlim Ritmo = new(1, 1);
    private static DateTime _ultimoPedido = DateTime.MinValue;
    private static readonly TimeSpan IntervaloMinimo = TimeSpan.FromMilliseconds(250);

    private readonly HttpClient _http;
    private readonly IOptionsMonitor<ScopeOptions> _options;
    private readonly IOptionsMonitor<AirtableOptions> _airtable;
    private readonly ILogger<ScopeClient> _logger;

    public ScopeClient(HttpClient http, IOptionsMonitor<ScopeOptions> options, IOptionsMonitor<AirtableOptions> airtable, ILogger<ScopeClient> logger)
    {
        _http = http;
        _options = options;
        _airtable = airtable;
        _logger = logger;
    }

    public async Task<IReadOnlyDictionary<string, string>> UpsertAsync(IReadOnlyList<(string Chave, Dictionary<string, object?> Campos)> registos, CancellationToken ct)
    {
        var resultado = new Dictionary<string, string>();
        var campoChave = _options.CurrentValue.CampoIdApp;

        foreach (var lote in registos.Chunk(MaxRegistosPorPedido))
        {
            var corpo = new
            {
                performUpsert = new { fieldsToMergeOn = new[] { campoChave } },
                typecast = true,
                records = lote.Select(r => new { fields = new Dictionary<string, object?>(r.Campos) { [campoChave] = r.Chave } }).ToArray()
            };

            using var resposta = await EnviarAsync(() => new HttpRequestMessage(HttpMethod.Patch, UrlTabela()) { Content = JsonContent.Create(corpo) }, ct);
            await GarantirSucessoAsync(resposta, "enviar projetos", ct);

            await using var stream = await resposta.Content.ReadAsStreamAsync(ct);
            using var json = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
            foreach (var rec in json.RootElement.GetProperty("records").EnumerateArray())
            {
                var id = rec.GetProperty("id").GetString();
                if (id != null && rec.TryGetProperty("fields", out var campos)
                    && campos.TryGetProperty(campoChave, out var chave) && chave.ValueKind == JsonValueKind.String)
                    resultado[chave.GetString()!] = id;
            }
        }
        return resultado;
    }

    public async Task ApagarAsync(IReadOnlyList<string> recordIds, CancellationToken ct)
    {
        foreach (var id in recordIds.Distinct())
        {
            using var resposta = await EnviarAsync(
                () => new HttpRequestMessage(HttpMethod.Delete, UrlTabela() + "/" + Uri.EscapeDataString(id)), ct);
            // Já apagado à mão no Airtable: não é erro.
            if (resposta.IsSuccessStatusCode || resposta.StatusCode is HttpStatusCode.NotFound or HttpStatusCode.Forbidden) continue;
            await GarantirSucessoAsync(resposta, $"apagar {id}", ct);
        }
    }

    private string UrlTabela()
    {
        var o = _options.CurrentValue;
        return $"{ApiUrl}{o.BaseId}/{o.TabelaId}";
    }

    private async Task<HttpResponseMessage> EnviarAsync(Func<HttpRequestMessage> criarPedido, CancellationToken ct)
    {
        var token = _options.CurrentValue.TokenEfetivo(_airtable.CurrentValue.Token);
        if (string.IsNullOrWhiteSpace(token))
            throw new InvalidOperationException("Token do Airtable não configurado (AirtableScope:Token ou Airtable:Token).");

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
            pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            var resposta = await _http.SendAsync(pedido, ct);

            if (resposta.StatusCode == HttpStatusCode.TooManyRequests && tentativa < 4)
            {
                _logger.LogWarning("Airtable SCOPE: limite de pedidos atingido, a aguardar 30 s.");
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
        throw new AirtableException(resposta.StatusCode, $"Airtable SCOPE respondeu {(int)resposta.StatusCode} ao {operacao}: {corpo}");
    }
}
