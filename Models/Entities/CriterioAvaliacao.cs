using System.ComponentModel.DataAnnotations;

namespace ComparacaoPropostas.Models.Entities;

public class CriterioAvaliacao
{
    public int Id { get; set; }

    [Required, Display(Name = "Critério")]
    public string Nome { get; set; } = "";

    [Display(Name = "Categoria")]
    public string? Categoria { get; set; }

    [Display(Name = "Descrição")]
    public string? Descricao { get; set; }

    [Required, Display(Name = "Domínio")]
    public string Dominio { get; set; } = "";

    public ICollection<ProcessoCriterio> ProcessosCriterio { get; set; } = new List<ProcessoCriterio>();
}
