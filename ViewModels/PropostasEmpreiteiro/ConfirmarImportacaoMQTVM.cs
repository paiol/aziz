namespace ComparacaoPropostas.ViewModels.PropostasEmpreiteiro;

public class ConfirmarImportacaoMQTVM
{
    public int PropostaEmpreiteiroId { get; set; }
    public string Empreiteiro { get; set; } = "";
    public List<OpcaoItemMQTVM> ItensMQTDisponiveis { get; set; } = new();
    public List<ItemPendenteMQTVM> Itens { get; set; } = new();
}

public class OpcaoItemMQTVM
{
    public int ItemMQTId { get; set; }
    public string Descricao { get; set; } = "";
}

public class ItemPendenteMQTVM
{
    public string NomeItem { get; set; } = "";
    public decimal QuantidadeFornecida { get; set; }
    public decimal PrecoUnitario { get; set; }

    // Pré-preenchido com o item do MQT mais parecido, se houver um razoavelmente próximo.
    public int? SugestaoItemMQTId { get; set; }

    // Escolha do utilizador ao confirmar: o Id do ItemMQT a que corresponde,
    // ou null/vazio se confirmar que é mesmo um item diferente (não estava no MQT).
    public int? EscolhaItemMQTId { get; set; }
}
