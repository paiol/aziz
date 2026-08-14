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

    public int AvaliadorId { get; set; }
    [ValidateNever]
    public Avaliador Avaliador { get; set; } = null!;

    [Range(1, 5, ErrorMessage = "A nota deve ser entre 1 e 5 estrelas.")]
    [Display(Name = "Nota (1-5)")]
    public int Nota { get; set; }

    [Display(Name = "Comentário")]
    public string? Comentario { get; set; }

    [Display(Name = "Avaliado em")]
    public DateTime AvaliadoEm { get; set; } = DateTime.UtcNow;
}
