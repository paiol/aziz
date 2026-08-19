using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Models.Entities;

public class ProjetoObra
{
    public int Id { get; set; }

    [Required, Display(Name = "Designação")]
    public string Designacao { get; set; } = "";

    [Display(Name = "Tipo")]
    public TipoProjetoObra Tipo { get; set; } = TipoProjetoObra.Edificacao;

    [Display(Name = "Local")]
    public string? Local { get; set; }

    [Display(Name = "Cliente")]
    public string? Cliente { get; set; }

    [Display(Name = "Valor Estimado")]
    public decimal? ValorEstimado { get; set; }

    [Display(Name = "Prazo (dias)")]
    public int? Prazo { get; set; }

    [Display(Name = "Estado")]
    public StatusProjetoObra Status { get; set; } = StatusProjetoObra.EmConcurso;

    [Display(Name = "E-mails a Notificar")]
    public string? EmailsNotificacao { get; set; }

    [Display(Name = "Criado por")]
    public string? CriadoPor { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    // Dados de Adjudicação — mesmo padrão de Processo.cs
    [Display(Name = "Proposta Vencedora")]
    public int? PropostaVencedoraId { get; set; }
    [ValidateNever]
    public PropostaEmpreiteiro? PropostaVencedora { get; set; }

    [Display(Name = "Valor Adjudicado")]
    public decimal? ValorAdjudicado { get; set; }

    [Display(Name = "Data de Adjudicação")]
    public DateTime? DataAdjudicacao { get; set; }

    [Display(Name = "Responsável pela Decisão")]
    public string? ResponsavelAdjudicacao { get; set; }

    [Display(Name = "Justificação da Decisão")]
    public string? JustificativaAdjudicacao { get; set; }

    public ICollection<ProjetoObraAnexo> Anexos { get; set; } = new List<ProjetoObraAnexo>();
    public ICollection<ItemMQT> ItensMQT { get; set; } = new List<ItemMQT>();
    public ICollection<CriterioObra> Criterios { get; set; } = new List<CriterioObra>();
    public ICollection<PropostaEmpreiteiro> Propostas { get; set; } = new List<PropostaEmpreiteiro>();

    [NotMapped]
    public decimal SomaPesos => Criterios.Sum(c => c.Peso);
}
