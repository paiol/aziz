using System.ComponentModel.DataAnnotations;

namespace ComparacaoPropostas.Models.Entities;

public class ItemMaterial
{
    public int Id { get; set; }

    [Required, Display(Name = "Nome do Item")]
    public string NomeItem { get; set; } = "";

    [Display(Name = "Categoria")]
    public string? Categoria { get; set; }

    [Display(Name = "Unidade")]
    public string? Unidade { get; set; }

    [Display(Name = "Descrição")]
    public string? Descricao { get; set; }

    [Display(Name = "Item Pai")]
    public int? ItemPaiId { get; set; }
    public ItemMaterial? ItemPai { get; set; }
    public ICollection<ItemMaterial> SubItens { get; set; } = new List<ItemMaterial>();

    public ICollection<ItemProposta> ItensProposta { get; set; } = new List<ItemProposta>();
}
