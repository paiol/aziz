using System.ComponentModel.DataAnnotations;

namespace ComparacaoPropostas.Models.Entities;

public class ProcessoCriterio
{
    public int Id { get; set; }

    public int ProcessoId { get; set; }
    public Processo Processo { get; set; } = null!;

    public int CriterioAvaliacaoId { get; set; }
    public CriterioAvaliacao CriterioAvaliacao { get; set; } = null!;

    [Display(Name = "Peso (%)")]
    public decimal Peso { get; set; }

    public ICollection<Avaliacao> Avaliacoes { get; set; } = new List<Avaliacao>();
}
