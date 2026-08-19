using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ComparacaoPropostas.Models.Entities;

public class AvaliacaoObra
{
    public int Id { get; set; }

    public int PropostaEmpreiteiroId { get; set; }
    [ValidateNever]
    public PropostaEmpreiteiro PropostaEmpreiteiro { get; set; } = null!;

    public int CriterioObraId { get; set; }
    [ValidateNever]
    public CriterioObra CriterioObra { get; set; } = null!;

    [Display(Name = "Avaliador")]
    public string? Avaliador { get; set; }

    [Range(1, 5, ErrorMessage = "A nota deve ser entre 1 e 5 estrelas.")]
    [Display(Name = "Nota (1-5)")]
    public int Nota { get; set; }

    [Display(Name = "Comentário")]
    public string? Comentario { get; set; }

    [Display(Name = "Avaliado em")]
    public DateTime AvaliadoEm { get; set; } = DateTime.UtcNow;
}
