using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ComparacaoPropostas.Models.Entities;

public class ItemPropostaEmpreiteiro
{
    public int Id { get; set; }

    public int PropostaEmpreiteiroId { get; set; }
    [ValidateNever]
    public PropostaEmpreiteiro PropostaEmpreiteiro { get; set; } = null!;

    public int ItemMQTId { get; set; }
    [ValidateNever]
    public ItemMQT ItemMQT { get; set; } = null!;

    [Display(Name = "Incluído")]
    public bool Incluido { get; set; } = true;

    [Display(Name = "Quantidade")]
    public decimal QuantidadeFornecida { get; set; }

    [Display(Name = "Preço Unitário")]
    public decimal PrecoUnitario { get; set; }

    [NotMapped]
    public decimal Subtotal => Incluido ? QuantidadeFornecida * PrecoUnitario : 0m;
}
