namespace ComparacaoPropostas.Models.Entities;

/// <summary>
/// Liga um registo da app ao registo correspondente no Airtable (sincronização App → Airtable).
/// Uma linha por (tabela Airtable, chave da app). A chave é a mesma que vai no campo
/// "ID App" do Airtable, por isso esta tabela funciona como cache: se se perder, o upsert
/// pelo campo "ID App" volta a encontrar os registos sem duplicar.
/// </summary>
public class AirtableLink
{
    public int Id { get; set; }

    /// <summary>ID da tabela no Airtable (tbl...).</summary>
    public string TabelaId { get; set; } = "";

    /// <summary>Chave da app, ex.: "PR9", "P18", "IP55", "A18-12", "IM7".</summary>
    public string Chave { get; set; } = "";

    /// <summary>ID do registo no Airtable (rec...).</summary>
    public string RecordId { get; set; } = "";

    /// <summary>
    /// Processo a que o registo pertence — usado para apagar no Airtable o que deixou de
    /// existir na app. Nulo para registos globais (catálogo Itens/Materiais).
    /// </summary>
    public int? ProcessoId { get; set; }

    public DateTime AtualizadoEm { get; set; } = DateTime.UtcNow;
}
