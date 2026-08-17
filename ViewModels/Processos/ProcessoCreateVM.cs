using System.ComponentModel.DataAnnotations;
using ComparacaoPropostas.Helper;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.ViewModels.Processos;

public class ProcessoCreateVM
{
    [Required(ErrorMessage = "Selecione o Pedido de Proposta.")]
    [Display(Name = "Pedido de Proposta")]
    public int PedidoPropostaId { get; set; }

    [Required(ErrorMessage = "O nome do processo é obrigatório."), Display(Name = "Nome do Processo")]
    public string Nome { get; set; } = "";

    [Display(Name = "Tipo de Compra")]
    public TipoCompra TipoCompra { get; set; } = TipoCompra.Nacional;

    [Display(Name = "Descrição")]
    public string? Descricao { get; set; }

    [Display(Name = "Criado por")]
    public string? CriadoPor { get; set; }

    [Display(Name = "E-mails a Notificar")]
    public string? EmailsNotificacao { get; set; }

    [Display(Name = "Taxa de Câmbio Padrão (EUR/CVE)")]
    public decimal TaxaCambioPadrao { get; set; } = MoedaHelper.TaxaEurCvePadrao;

    [Display(Name = "Fornecedores a Associar")]
    public List<string> Fornecedores { get; set; } = new();
}
