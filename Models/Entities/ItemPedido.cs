using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ComparacaoPropostas.Models.Entities;

public class ItemPedido
{
    public int Id { get; set; }

    public int PedidoPropostaId { get; set; }
    [ValidateNever]
    public PedidoProposta PedidoProposta { get; set; } = null!;

    public int ItemMaterialId { get; set; }
    [ValidateNever]
    public ItemMaterial ItemMaterial { get; set; } = null!;

    [Display(Name = "Quantidade Solicitada")]
    public decimal QuantidadeSolicitada { get; set; }

    [Display(Name = "Observação")]
    public string? Observacao { get; set; }
}
