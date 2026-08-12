using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.ViewModels.Comparacao;

public class ComparacaoViewModel
{
    public Processo Processo { get; set; } = null!;
    public List<PropostaColuna> Propostas { get; set; } = new();
    public List<LinhaCriterio> LinhasCriterios { get; set; } = new();
}

public class PropostaColuna
{
    public int PropostaId { get; set; }
    public string Fornecedor { get; set; } = "";
    public decimal ValorTotal { get; set; }
    public bool ValorTotalMelhor { get; set; }
    public int? PrazoEntregaDias { get; set; }
    public bool PrazoMelhor { get; set; }
    public decimal PontuacaoPonderada { get; set; }
    public bool PontuacaoMelhor { get; set; }
    public StatusProposta Status { get; set; }
}

public class LinhaCriterio
{
    public int CriterioId { get; set; }
    public string CriterioNome { get; set; } = "";
    public decimal Peso { get; set; }
    public Dictionary<int, decimal?> NotasPorProposta { get; set; } = new();
    public Dictionary<int, bool> MelhorPorProposta { get; set; } = new();
}
