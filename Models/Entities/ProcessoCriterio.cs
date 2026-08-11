using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ComparacaoPropostas.Models.Entities;

public class ProcessoCriterio
{
    public int Id { get; set; }

    public int ProcessoId { get; set; }
    [ValidateNever]
    public Processo Processo { get; set; } = null!;

    public int CriterioAvaliacaoId { get; set; }
    [ValidateNever]
    public CriterioAvaliacao CriterioAvaliacao { get; set; } = null!;

    [Display(Name = "Peso (%)")]
    public decimal Peso { get; set; }

    public ICollection<Avaliacao> Avaliacoes { get; set; } = new List<Avaliacao>();
}
