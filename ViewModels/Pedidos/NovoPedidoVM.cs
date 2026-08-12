using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.ViewModels.Pedidos;

public class NovoPedidoVM
{
    public int ProcessoId { get; set; }
    public string Fornecedor { get; set; } = "";
    public List<ItemPedido> Itens { get; set; } = new();
}
