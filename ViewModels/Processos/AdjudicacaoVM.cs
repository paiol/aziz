using System.ComponentModel.DataAnnotations;

namespace ComparacaoPropostas.ViewModels.Processos;

public class AdjudicacaoVM
{
    public int ProcessoId { get; set; }
    public string ProcessoNome { get; set; } = "";

    [Required(ErrorMessage = "Selecione a proposta vencedora.")]
    [Display(Name = "Fornecedor / Proposta Vencedora")]
    public int PropostaVencedoraId { get; set; }

    [Required(ErrorMessage = "Indique o responsável pela decisão.")]
    [Display(Name = "Responsável pela Decisão")]
    public string ResponsavelAdjudicacao { get; set; } = "";

    [Display(Name = "Justificação da Decisão")]
    public string? JustificativaAdjudicacao { get; set; }

    public int? PrimeiroLugarRankingId { get; set; }
    public string? PrimeiroLugarFornecedor { get; set; }

    public List<OpcaoPropostaAdjudicacao> PropostasDisponiveis { get; set; } = new();
}

public class OpcaoPropostaAdjudicacao
{
    public int Id { get; set; }
    public string Fornecedor { get; set; } = "";
    public int PosicaoRanking { get; set; }
    public decimal PontuacaoPonderada { get; set; }
    public decimal ValorTotal { get; set; }
    public decimal ValorTotalCVE { get; set; }
    public string Moeda { get; set; } = "CVE";
    public int? PrazoEntregaDias { get; set; }
    public string? Garantia { get; set; }
    public bool IsPrimeiroLugar => PosicaoRanking == 1;
}
