namespace ComparacaoPropostas.ViewModels.ItensPedido;

public class NovoItensPedidoVM
{
    public int PedidoId { get; set; }
    public List<NovaLinhaItemPedido> Itens { get; set; } = new();
}

public class NovaLinhaItemPedido
{
    // Composite key from the Material dropdown, e.g. "energia:12", "mbb:7".
    public string ChaveItem { get; set; } = "";
    public decimal QuantidadeSolicitada { get; set; }
    public string? Observacao { get; set; }
}

public class ItemBuscaResultado
{
    public string Chave { get; set; } = "";
    public string Nome { get; set; } = "";
    public string? Categoria { get; set; }
    public string? Unidade { get; set; }
    public string? Dominio { get; set; }
    public string Origem { get; set; } = "";
}
