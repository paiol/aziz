using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.ViewModels.ProjetosObra;

public class ProjetoObraDetailVM
{
    public ComparacaoPropostas.Models.Entities.ProjetoObra ProjetoObra { get; set; } = null!;
    public List<PropostaEmpreiteiroResumo> Propostas { get; set; } = new();
    public decimal SomaPesos { get; set; }
    public int TotalItensMQT { get; set; }
    public string? MailtoResultado { get; set; }
    public string? EmailDestinatarios { get; set; }
    public string? EmailAssunto { get; set; }
    public string? EmailCorpo { get; set; }
}

public class PropostaEmpreiteiroResumo
{
    public int Id { get; set; }
    public string Empreiteiro { get; set; } = "";
    public decimal ValorTotal { get; set; }
    public int? PrazoEntregaDias { get; set; }
    public decimal PontuacaoPonderada { get; set; }
    public int PosicaoRanking { get; set; }
    public StatusPropostaEmpreiteiro Status { get; set; }
}
