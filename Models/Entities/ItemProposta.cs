using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ComparacaoPropostas.Models.Entities;

public class ItemProposta
{
    public int Id { get; set; }

    public int PropostaId { get; set; }
    [ValidateNever]
    public Proposta Proposta { get; set; } = null!;

    public int ItemMaterialId { get; set; }
    [ValidateNever]
    public ItemMaterial ItemMaterial { get; set; } = null!;

    [Display(Name = "Incluído")]
    public bool Incluido { get; set; } = true;

    [Display(Name = "Quantidade")]
    public decimal Quantidade { get; set; }

    [Display(Name = "Preço Unitário")]
    public decimal PrecoUnitario { get; set; }

    [Display(Name = "Observação")]
    public string? Observacao { get; set; }

    [NotMapped]
    public decimal Subtotal => Incluido ? Quantidade * PrecoUnitario : 0m;
}
