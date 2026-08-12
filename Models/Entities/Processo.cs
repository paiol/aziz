using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Models.Entities;

public class Processo
{
    public int Id { get; set; }

    // The Pedido de Proposta that originated this Processo (1-1, required — a
    // Processo is always born from a Pedido now, not the other way around).
    public int PedidoPropostaId { get; set; }
    [ValidateNever]
    public PedidoProposta PedidoProposta { get; set; } = null!;

    [Required, Display(Name = "Nome do Processo")]
    public string Nome { get; set; } = "";

    [Display(Name = "Descrição")]
    public string? Descricao { get; set; }

    [Display(Name = "Estado")]
    public StatusProcesso Status { get; set; } = StatusProcesso.Aberto;

    [Display(Name = "Prazo Final")]
    [DataType(DataType.Date)]
    public DateTime? PrazoFinal { get; set; }

    [Display(Name = "Orçamento Estimado")]
    public decimal? OrcamentoEstimado { get; set; }

    [Required, Display(Name = "Fornecedor")]
    public string Fornecedor { get; set; } = "";

    [Display(Name = "Criado por")]
    public string? CriadoPor { get; set; }

    [Display(Name = "E-mails a Notificar")]
    public string? EmailsNotificacao { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<Criterio> Criterios { get; set; } = new List<Criterio>();
    public ICollection<Proposta> Propostas { get; set; } = new List<Proposta>();
    public ICollection<ItemPedido> ItensPedido { get; set; } = new List<ItemPedido>();
}
