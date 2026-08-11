using System.ComponentModel.DataAnnotations;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Models.Entities;

public class Processo
{
    public int Id { get; set; }

    [Required, Display(Name = "Nome do Processo")]
    public string Nome { get; set; } = "";

    [Display(Name = "Descrição")]
    public string? Descricao { get; set; }

    [Display(Name = "Estado")]
    public StatusProcesso Status { get; set; } = StatusProcesso.Aberto;

    [Display(Name = "Prazo Final")]
    [DataType(DataType.Date)]
    public DateTime? PrazoFinal { get; set; }

    [Display(Name = "Orçamento Estimado")]
    public decimal? OrcamentoEstimado { get; set; }

    [Required, Display(Name = "Tipo de Processo")]
    public string TipoProcesso { get; set; } = "";

    [Display(Name = "Criado por")]
    public string? CriadoPor { get; set; }

    [Display(Name = "E-mails a Notificar")]
    public string? EmailsNotificacao { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<ProcessoCriterio> Criterios { get; set; } = new List<ProcessoCriterio>();
    public ICollection<Proposta> Propostas { get; set; } = new List<Proposta>();
}
