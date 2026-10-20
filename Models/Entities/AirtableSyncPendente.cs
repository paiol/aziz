namespace ComparacaoPropostas.Models.Entities;

/// <summary>
/// Fila persistente (outbox) de alterações a enviar para o Airtable. Cada gravação na app
/// que mexe num processo, proposta, avaliação, critério ou item acrescenta aqui uma linha; o
/// AirtableSyncWorker processa-as em segundo plano. Por estar na base de dados, nada se perde
/// se o IIS reciclar a aplicação ou se o Airtable estiver em baixo — fica para nova tentativa.
/// </summary>
public class AirtableSyncPendente
{
    public int Id { get; set; }

    /// <summary>"Processo", "Pedido", "Proposta" ou "ItemMaterial".</summary>
    public string Tipo { get; set; } = "";

    public int EntidadeId { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public int Tentativas { get; set; }

    public DateTime ProximaTentativaEm { get; set; } = DateTime.UtcNow;

    public string? Erro { get; set; }

    public DateTime? ProcessadoEm { get; set; }
}
