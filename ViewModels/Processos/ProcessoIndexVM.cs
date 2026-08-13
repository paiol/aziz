using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.ViewModels.Processos;

public class ProcessoIndexVM
{
    public int Id { get; set; }
    public string Nome { get; set; } = "";
    public StatusProcesso Status { get; set; }
    public DateTime? PrazoEntrega { get; set; }
    public string TipoProposta { get; set; } = "";
    public AreaDepartamento Area { get; set; }
    public string Fornecedor { get; set; } = "";
    public decimal? OrcamentoEstimado { get; set; }
    public int TotalPropostas { get; set; }
    public decimal? MenorValorOfertado { get; set; }
}
