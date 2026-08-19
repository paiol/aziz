using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ComparacaoPropostas.Models.Entities;

public class CriterioObra
{
    public int Id { get; set; }

    public int ProjetoObraId { get; set; }
    [ValidateNever]
    public ProjetoObra ProjetoObra { get; set; } = null!;

    [Required, Display(Name = "Critério")]
    public string Nome { get; set; } = "";

    [Display(Name = "Categoria")]
    public string? Categoria { get; set; }

    [Display(Name = "Peso (%)")]
    public decimal Peso { get; set; }

    public ICollection<AvaliacaoObra> Avaliacoes { get; set; } = new List<AvaliacaoObra>();
}
