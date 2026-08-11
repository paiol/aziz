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

    public ICollection<ItemProposta> ItensProposta { get; set; } = new List<ItemProposta>();
}
