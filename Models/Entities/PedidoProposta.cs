using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Models.Entities;

public class PedidoProposta
{
    public int Id { get; set; }

    public int ProcessoId { get; set; }
    [ValidateNever]
    public Processo Processo { get; set; } = null!;

    [Required, Display(Name = "Fornecedor")]
    public string Fornecedor { get; set; } = "";

    [Display(Name = "Estado")]
    public StatusPedido Status { get; set; } = StatusPedido.Pendente;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<ItemPedido> ItensPedido { get; set; } = new List<ItemPedido>();
    public ICollection<Proposta> Propostas { get; set; } = new List<Proposta>();
}
