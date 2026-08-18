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

    public int? ItemMaterialId { get; set; }
    [ValidateNever]
    public ItemMaterial? ItemMaterial { get; set; }

    // Nome tal como veio do Excel do fornecedor, usado só quando o item foi
    // confirmado como "diferente do pedido" e não existe no catálogo — evita
    // criar entradas novas em ItensMaterial para nomes usados uma única vez.
    [Display(Name = "Nome do Item")]
    public string? NomeItemLivre { get; set; }

    // Marcado quando o item veio do Excel do fornecedor com um nome que não
    // corresponde a nenhum item pedido neste processo, e o utilizador confirmou
    // que é mesmo um item diferente (não uma nomenclatura diferente do mesmo item).
    [Display(Name = "Não Solicitado")]
    public bool NaoSolicitado { get; set; }

    [Display(Name = "Incluído")]
    public bool Incluido { get; set; } = true;

    [Display(Name = "Quantidade Solicitada")]
    public decimal? QuantidadeSolicitada { get; set; }

    [Display(Name = "Quantidade")]
    public decimal Quantidade { get; set; }

    [Display(Name = "Preço Unitário")]
    public decimal PrecoUnitario { get; set; }

    [Display(Name = "Observação")]
    public string? Observacao { get; set; }

    [NotMapped]
    public decimal Subtotal => Incluido ? Quantidade * PrecoUnitario : 0m;

    [NotMapped]
    public string NomeExibicao => ItemMaterial?.NomeItem ?? NomeItemLivre ?? "(sem nome)";
}
