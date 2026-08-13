namespace ComparacaoPropostas.ViewModels.Comparacao;

public class ComparacaoItensViewModel
{
    public Models.Entities.Processo Processo { get; set; } = null!;
    public List<PropostaColuna> Propostas { get; set; } = new();
    public List<LinhaItem> Linhas { get; set; } = new();
}

public class LinhaItem
{
    public int ItemMaterialId { get; set; }
    public string NomeItem { get; set; } = "";
    public string? Unidade { get; set; }
    public decimal? QuantidadeSolicitada { get; set; }
    public Dictionary<int, decimal?> QuantidadePorProposta { get; set; } = new();
    public Dictionary<int, decimal?> PrecoPorProposta { get; set; } = new();
    public Dictionary<int, decimal?> SubtotalPorProposta { get; set; } = new();
    public Dictionary<int, decimal?> DiferencaPorProposta { get; set; } = new();
    public Dictionary<int, bool> IncluidoPorProposta { get; set; } = new();
    public Dictionary<int, bool> MelhorPorProposta { get; set; } = new();
}
