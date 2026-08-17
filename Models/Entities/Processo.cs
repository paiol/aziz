using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using ComparacaoPropostas.Helper;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Models.Entities;

public class Processo
{
    public int Id { get; set; }

    public int PedidoPropostaId { get; set; }
    [ValidateNever]
    public PedidoProposta PedidoProposta { get; set; } = null!;

    [Display(Name = "Nº do Processo")]
    public string NumeroProcesso { get; set; } = "";

    [Required, Display(Name = "Nome do Processo")]
    public string Nome { get; set; } = "";

    [Display(Name = "Descrição")]
    public string? Descricao { get; set; }

    [Display(Name = "Estado")]
    public StatusProcesso Status { get; set; } = StatusProcesso.Criado;

    [Display(Name = "Tipo de Compra")]
    public TipoCompra TipoCompra { get; set; } = TipoCompra.Nacional;

    // Campo legado preservado para retrocompatibilidade
    [Display(Name = "Fornecedor (Legado)")]
    public string? Fornecedor { get; set; } = "";

    [Display(Name = "Criado por")]
    public string? CriadoPor { get; set; }

    [Display(Name = "E-mails a Notificar")]
    public string? EmailsNotificacao { get; set; }

    [Display(Name = "Taxa de Câmbio Padrão (EUR/CVE)")]
    public decimal TaxaCambioPadrao { get; set; } = MoedaHelper.TaxaEurCvePadrao;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    // Dados de Adjudicação
    [Display(Name = "Proposta Vencedora")]
    public int? PropostaVencedoraId { get; set; }
    [ValidateNever]
    public Proposta? PropostaVencedora { get; set; }

    [Display(Name = "Valor Adjudicado")]
    public decimal? ValorAdjudicado { get; set; }

    [Display(Name = "Moeda Adjudicada")]
    public string? ValorAdjudicadoMoeda { get; set; }

    [Display(Name = "Valor Adjudicado (CVE)")]
    public decimal? ValorAdjudicadoCVE { get; set; }

    [Display(Name = "Pontuação da Proposta Vencedora")]
    public decimal? PontuacaoAdjudicada { get; set; }

    [Display(Name = "Data de Adjudicação")]
    public DateTime? DataAdjudicacao { get; set; }

    [Display(Name = "Responsável pela Decisão")]
    public string? ResponsavelAdjudicacao { get; set; }

    [Display(Name = "Justificação da Decisão")]
    public string? JustificativaAdjudicacao { get; set; }

    [Display(Name = "Comunicação Enviada em")]
    public DateTime? EmailResultadoEnviadoEm { get; set; }

    public ICollection<Criterio> Criterios { get; set; } = new List<Criterio>();
    public ICollection<Proposta> Propostas { get; set; } = new List<Proposta>();
}
