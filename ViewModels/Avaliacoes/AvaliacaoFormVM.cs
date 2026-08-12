namespace ComparacaoPropostas.ViewModels.Avaliacoes;

public class AvaliacaoFormVM
{
    public int PropostaId { get; set; }
    public string PropostaFornecedor { get; set; } = "";
    public int ProcessoId { get; set; }
    public List<ItemAvaliacaoVM> Itens { get; set; } = new();
}

public class ItemAvaliacaoVM
{
    public int CriterioId { get; set; }
    public string CriterioNome { get; set; } = "";
    public decimal Peso { get; set; }
    public decimal Nota { get; set; }
    public string? Comentario { get; set; }
}
