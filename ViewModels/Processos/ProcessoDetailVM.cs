using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.ViewModels.Processos;

public class ProcessoDetailVM
{
    public Processo Processo { get; set; } = null!;
    public List<PropostaResumo> Propostas { get; set; } = new();
    public decimal SomaPesos { get; set; }
    public int TotalItensPedido { get; set; }
    public string? MailtoResultado { get; set; }
}

public class PropostaResumo
{
    public int Id { get; set; }
    public string Fornecedor { get; set; } = "";
    public string Moeda { get; set; } = "CVE";
    public decimal TaxaCambio { get; set; }
    public decimal ValorTotal { get; set; }
    public decimal ValorTotalCVE { get; set; }
    public decimal? ValorTotalEUR { get; set; }
    public int? PrazoEntregaDias { get; set; }
    public bool? PrazoDentroDoSolicitado { get; set; }
    public int? PrazoDiasDeAtraso { get; set; }
    public string? Garantia { get; set; }
    public decimal PontuacaoPonderada { get; set; }
    public int PosicaoRanking { get; set; }
    public int TotalAvaliadores { get; set; }
    public StatusProposta Status { get; set; }
}
