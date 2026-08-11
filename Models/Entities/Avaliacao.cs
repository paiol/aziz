using System.ComponentModel.DataAnnotations;

namespace ComparacaoPropostas.Models.Entities;

public class Avaliacao
{
    public int Id { get; set; }

    public int PropostaId { get; set; }
    public Proposta Proposta { get; set; } = null!;

    public int ProcessoCriterioId { get; set; }
    public ProcessoCriterio ProcessoCriterio { get; set; } = null!;

    [Display(Name = "Nota")]
    public decimal Nota { get; set; }

    [Display(Name = "Comentário")]
    public string? Comentario { get; set; }
}
