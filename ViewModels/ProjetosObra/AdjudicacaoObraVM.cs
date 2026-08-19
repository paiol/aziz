using System.ComponentModel.DataAnnotations;

namespace ComparacaoPropostas.ViewModels.ProjetosObra;

public class AdjudicacaoObraVM
{
    public int ProjetoObraId { get; set; }
    public string ProjetoObraNome { get; set; } = "";

    [Required(ErrorMessage = "Selecione a proposta vencedora.")]
    [Display(Name = "Empreiteiro / Proposta Vencedora")]
    public int PropostaVencedoraId { get; set; }

    [Required(ErrorMessage = "Indique o responsável pela decisão.")]
    [Display(Name = "Responsável pela Decisão")]
    public string ResponsavelAdjudicacao { get; set; } = "";

    [Display(Name = "Justificação da Decisão")]
    public string? JustificativaAdjudicacao { get; set; }

    public int? PrimeiroLugarRankingId { get; set; }
    public string? PrimeiroLugarEmpreiteiro { get; set; }

    public List<OpcaoPropostaEmpreiteiroAdjudicacao> PropostasDisponiveis { get; set; } = new();
}

public class OpcaoPropostaEmpreiteiroAdjudicacao
{
    public int Id { get; set; }
    public string Empreiteiro { get; set; } = "";
    public int PosicaoRanking { get; set; }
    public decimal PontuacaoPonderada { get; set; }
    public decimal ValorTotal { get; set; }
    public int? PrazoEntregaDias { get; set; }
    public bool IsPrimeiroLugar => PosicaoRanking == 1;
}
