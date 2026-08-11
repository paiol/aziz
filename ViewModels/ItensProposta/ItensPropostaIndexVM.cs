using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.ViewModels.ItensProposta;

public class ItensPropostaIndexVM
{
    public Proposta Proposta { get; set; } = null!;
    public List<Models.Entities.ItemProposta> Itens { get; set; } = new();
    public List<ResumoItemVM> ResumoPorItem { get; set; } = new();
    public decimal QuantidadeGeral { get; set; }
    public decimal ValorGeral { get; set; }
}

public class ResumoItemVM
{
    public string NomeItem { get; set; } = "";
    public decimal QuantidadeTotal { get; set; }
    public decimal ValorTotal { get; set; }
}
