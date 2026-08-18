namespace ComparacaoPropostas.ViewModels.ItensProposta;

public class ConfirmarImportacaoVM
{
    public int PropostaId { get; set; }
    public string PropostaFornecedor { get; set; } = "";
    public List<OpcaoItemPedidoVM> ItensPedidoDisponiveis { get; set; } = new();
    public List<ItemPendenteVM> Itens { get; set; } = new();
}

public class OpcaoItemPedidoVM
{
    public int ItemPedidoId { get; set; }
    public int ItemMaterialId { get; set; }
    public string NomeItem { get; set; } = "";
}

public class ItemPendenteVM
{
    public string NomeItem { get; set; } = "";
    public decimal QuantidadeFornecida { get; set; }
    public decimal PrecoUnitario { get; set; }
    public string? Observacao { get; set; }

    // Pré-preenchido com o item pedido mais parecido, se houver um razoavelmente próximo.
    public int? SugestaoItemPedidoId { get; set; }

    // Escolha do utilizador ao confirmar: o Id do ItemPedido a que corresponde,
    // ou null/vazio se confirmar que é mesmo um item diferente (não pedido).
    public int? EscolhaItemPedidoId { get; set; }
}
