using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.ViewModels.ItensMQT;

public class NovosItensMQTVM
{
    public int ProjetoObraId { get; set; }
    public List<ItemMQT> Itens { get; set; } = new() { new ItemMQT { Quantidade = 1 } };
}
