using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Services;

namespace ComparacaoPropostas.Models.Entities;

// Uma linha da folha "Formação e feiras" do plano. Custos em regra OPEX: não entram no TOTAL ORC. CAPEX.
public class AcaoFormacaoPlano
{
    public int Id { get; set; }

    public int PlanoId { get; set; }
    [ValidateNever]
    public Plano Plano { get; set; } = null!;

    // 1 = recomendado (cenário mínimo), 2 = se houver orçamento (alargado), 3 = opcional.
    [Range(1, 3), Display(Name = "Prioridade")]
    public int Prioridade { get; set; } = 1;

    [Display(Name = "Tipo")]
    public TipoAcaoFormacao Tipo { get; set; } = TipoAcaoFormacao.Formacao;

    [Required, Display(Name = "Ação")]
    public string Acao { get; set; } = "";

    [Display(Name = "Datas")]
    public string? Datas { get; set; }

    [Display(Name = "Local")]
    public string? Local { get; set; }

    [Display(Name = "Formato")]
    public string? Formato { get; set; }

    [Display(Name = "Ligação aos projetos")]
    public string? LigacaoProjetos { get; set; }

    [Range(0, 1000), Display(Name = "Nº pessoas")]
    public int? NumeroPessoas { get; set; }

    [Display(Name = "Dias (incl. viagem)")]
    public decimal? Dias { get; set; }

    [Display(Name = "Inscrição / curso por pessoa")]
    public decimal? InscricaoPorPessoa { get; set; }

    [Display(Name = "Passagem por pessoa")]
    public decimal? PassagemPorPessoa { get; set; }

    [Display(Name = "Estadia / dia")]
    public decimal? EstadiaDia { get; set; }

    [Display(Name = "Ajuda de custo / dia")]
    public decimal? AjudaCustoDia { get; set; }

    [Display(Name = "No plano?")]
    public bool NoPlano { get; set; }

    [Display(Name = "Participantes")]
    public string? Participantes { get; set; }

    [Display(Name = "Fonte")]
    public string? Fonte { get; set; }

    [Display(Name = "Observações")]
    public string? Observacoes { get; set; }

    // Ano em que a ação se realiza (as de anos seguintes ficam para os planos seguintes).
    [Display(Name = "Ano")]
    public int? Ano { get; set; }

    [NotMapped]
    public decimal Total => PlanoCalculos.TotalAcaoFormacao(this);
}
