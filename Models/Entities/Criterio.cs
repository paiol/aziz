using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ComparacaoPropostas.Models.Entities;

public class Criterio
{
    public int Id { get; set; }

    public int ProcessoId { get; set; }
    [ValidateNever]
    public Processo Processo { get; set; } = null!;

    [Required, Display(Name = "Critério")]
    public string Nome { get; set; } = "";

    [Display(Name = "Categoria")]
    public string? Categoria { get; set; }

    [Display(Name = "Descrição")]
    public string? Descricao { get; set; }

    [Display(Name = "Peso (%)")]
    public decimal Peso { get; set; }

    public ICollection<Avaliacao> Avaliacoes { get; set; } = new List<Avaliacao>();
}
