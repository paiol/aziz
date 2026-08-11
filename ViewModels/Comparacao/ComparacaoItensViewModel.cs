using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.ViewModels.Comparacao;

public class ComparacaoItensViewModel
{
    public Processo Processo { get; set; } = null!;
    public List<PropostaColunaSimples> Propostas { get; set; } = new();
    public List<LinhaItem> Linhas { get; set; } = new();
}

public class PropostaColunaSimples
{
    public int PropostaId { get; set; }
    public string Fornecedor { get; set; } = "";
}

public class LinhaItem
{
    public int ItemMaterialId { get; set; }
    public string NomeItem { get; set; } = "";
    public string? Unidade { get; set; }
    public Dictionary<int, decimal?> PrecoPorProposta { get; set; } = new();
    public Dictionary<int, bool> IncluidoPorProposta { get; set; } = new();
    public Dictionary<int, bool> MelhorPorProposta { get; set; } = new();
}
