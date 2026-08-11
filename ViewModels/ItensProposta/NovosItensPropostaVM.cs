using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.ViewModels.ItensProposta;

public class NovosItensPropostaVM
{
    public int PropostaId { get; set; }
    public List<ItemProposta> Itens { get; set; } = new() { new ItemProposta { Incluido = true, Quantidade = 1 } };
}
