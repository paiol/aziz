using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.ViewModels.ItensProcesso;

public class NovosItensProcessoVM
{
    public int ProcessoId { get; set; }
    public List<ItemPedido> Itens { get; set; } = new();
}
