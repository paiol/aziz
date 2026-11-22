namespace ComparacaoPropostas.Services.Airtable.Scope;

/// <summary>
/// Secção "AirtableScope" do appsettings: envio dos projetos aceites dos planos concluídos para
/// a base SCOPE do Airtable. Independente da integração "Airtable" (processos de compra).
/// O Token NUNCA vai para o repositório; se ficar vazio usa-se o Token da secção "Airtable"
/// (o Personal Access Token tem então de ter acesso também a esta base).
/// </summary>
public class ScopeOptions
{
    public const string Secao = "AirtableScope";

    public bool Enabled { get; set; }

    public string? Token { get; set; }

    /// <summary>Base de destino (por omissão a base de TESTE "SCOPE DEI Teste (Plataforma DEI)").</summary>
    public string BaseId { get; set; } = "appsDBG53SXZgJE77";

    /// <summary>Tabela de destino (por omissão "SCOPE Plano 2027 (teste)").</summary>
    public string TabelaId { get; set; } = "tblMSNvmtIu3SmiuL";

    /// <summary>Campo de texto que guarda a chave do projeto na app (PP&lt;id&gt;); usado para não duplicar.</summary>
    public string CampoIdApp { get; set; } = "ID App";

    public int IntervaloSegundos { get; set; } = 30;

    public string? TokenEfetivo(string? tokenAirtable) => string.IsNullOrWhiteSpace(Token) ? tokenAirtable : Token;
}
