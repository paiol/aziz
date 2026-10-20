namespace ComparacaoPropostas.Services.Airtable;

/// <summary>
/// Secção "Airtable" do appsettings. O Token NUNCA vai para o repositório: em produção fica
/// no appsettings.Production.json (ignorado pelo git) ou na variável de ambiente Airtable__Token.
/// </summary>
public class AirtableOptions
{
    public const string Secao = "Airtable";

    /// <summary>Liga/desliga a sincronização sem ser preciso apagar o token.</summary>
    public bool Enabled { get; set; }

    /// <summary>Personal Access Token com data.records:read e data.records:write só nesta base.</summary>
    public string? Token { get; set; }

    public string BaseId { get; set; } = AirtableSchema.BaseId;

    /// <summary>URL pública da app, para o campo "Link App" no Airtable (sem barra final).</summary>
    public string? AppBaseUrl { get; set; }

    /// <summary>De quanto em quanto tempo o worker verifica a fila quando não recebe sinal.</summary>
    public int IntervaloSegundos { get; set; } = 30;

    /// <summary>Espera após uma gravação antes de enviar, para juntar várias gravações seguidas num só envio.</summary>
    public int AtrasoAgruparSegundos { get; set; } = 3;

    public bool Ativo => Enabled && !string.IsNullOrWhiteSpace(Token) && !string.IsNullOrWhiteSpace(BaseId);
}
