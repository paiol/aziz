using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ComparacaoPropostas.Models.Entities;

public class ItemProposta
{
    public int Id { get; set; }

    public int PropostaId { get; set; }
    public Proposta Proposta { get; set; } = null!;

    public int ItemMaterialId { get; set; }
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
