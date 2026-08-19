using System.ComponentModel.DataAnnotations;

namespace ComparacaoPropostas.Models.Entities;

public class ItemCcv
{
    public int Id { get; set; }

    [Required, Display(Name = "Nome")]
    public string Nome { get; set; } = "";

    [Display(Name = "Tipo")]
    public string? Tipo { get; set; }

    [Display(Name = "Categoria")]
    public string? Categoria { get; set; }

    [Display(Name = "Unidade")]
    public string? Unidade { get; set; }

    [Display(Name = "Descrição")]
    public string? Descricao { get; set; }

    [Display(Name = "Domínio")]
    public string? Dominio { get; set; }
}
