using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using ComparacaoPropostas.Helper;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Models.Entities;

public class Proposta
{
    public int Id { get; set; }

    public int ProcessoId { get; set; }

    [ValidateNever]
    public Processo Processo { get; set; } = null!;

    [Required, Display(Name = "Fornecedor")]
    public string Fornecedor { get; set; } = "";

    [Display(Name = "Moeda")]
    public string Moeda { get; set; } = MoedaHelper.MoedaCve;

    [Display(Name = "Taxa de Câmbio (EUR/CVE)")]
    public decimal TaxaCambio { get; set; } = MoedaHelper.TaxaEurCvePadrao;

    [Display(Name = "Valor Total")]
    public decimal ValorTotal { get; set; }

    [Display(Name = "Prazo de Entrega (dias)")]
    public int? PrazoEntregaDias { get; set; }

    [Display(Name = "Garantia")]
    public string? Garantia { get; set; }

    [Display(Name = "Validade da Proposta")]
    [DataType(DataType.Date)]
    public DateTime? ValidadeProposta { get; set; }

    [Display(Name = "Estado")]
    public StatusProposta Status { get; set; } = StatusProposta.Recebida;

    [Display(Name = "Observações")]
    public string? Observacoes { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    [NotMapped]
    public decimal ValorTotalCVE => MoedaHelper.CalcularTotalCve(ValorTotal, Moeda, TaxaCambio);

    [NotMapped]
    public decimal? ValorTotalEUR => MoedaHelper.CalcularTotalEur(ValorTotal, Moeda, TaxaCambio);

    public ICollection<Avaliacao> Avaliacoes { get; set; } = new List<Avaliacao>();
    public ICollection<ItemProposta> ItensProposta { get; set; } = new List<ItemProposta>();
    public ICollection<PropostaAnexo> Anexos { get; set; } = new List<PropostaAnexo>();
    public ICollection<MemoriaCalculoAvaliacao> MemoriaCalculo { get; set; } = new List<MemoriaCalculoAvaliacao>();
}
