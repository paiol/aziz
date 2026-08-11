namespace ComparacaoPropostas.ViewModels.Criterios;

public class GerenciarCriteriosVM
{
    public int ProcessoId { get; set; }
    public string ProcessoNome { get; set; } = "";
    public string TipoProcesso { get; set; } = "";
    public List<ItemCriterioVM> Itens { get; set; } = new();
}

public class ItemCriterioVM
{
    public int CriterioAvaliacaoId { get; set; }
    public string Nome { get; set; } = "";
    public string? Categoria { get; set; }
    public bool Selecionado { get; set; }
    public decimal Peso { get; set; }
}
