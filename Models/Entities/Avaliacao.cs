using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ComparacaoPropostas.Models.Entities;

public class Avaliacao
{
    public int Id { get; set; }

    public int PropostaId { get; set; }
    [ValidateNever]
    public Proposta Proposta { get; set; } = null!;

    public int CriterioId { get; set; }
    [ValidateNever]
    public Criterio Criterio { get; set; } = null!;

    [Display(Name = "Nota")]
    public decimal Nota { get; set; }

    [Display(Name = "Comentário")]
    public string? Comentario { get; set; }
}
