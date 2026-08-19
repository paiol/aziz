using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.ViewModels.ComparacaoObra;

public class ComparacaoObraViewModel
{
    public ProjetoObra ProjetoObra { get; set; } = null!;
    public List<PropostaEmpreiteiroColuna> Propostas { get; set; } = new();
    public List<LinhaCriterioObra> LinhasCriterios { get; set; } = new();
    public List<LinhaItemMQT> LinhasItens { get; set; } = new();
}

public class PropostaEmpreiteiroColuna
{
    public int PropostaId { get; set; }
    public string Empreiteiro { get; set; } = "";
    public decimal ValorTotal { get; set; }
    public bool ValorTotalMelhor { get; set; }
    public int? PrazoEntregaDias { get; set; }
    public bool PrazoMelhor { get; set; }
    public decimal PontuacaoPonderada { get; set; }
    public bool PontuacaoMelhor { get; set; }
    public int PosicaoRanking { get; set; }
    public StatusPropostaEmpreiteiro Status { get; set; }
}

public class LinhaCriterioObra
{
    public int CriterioObraId { get; set; }
    public string CriterioNome { get; set; } = "";
    public decimal Peso { get; set; }
    public Dictionary<int, decimal?> NotasPorProposta { get; set; } = new();
    public Dictionary<int, bool> MelhorPorProposta { get; set; } = new();
}

public class LinhaItemMQT
{
    public int ItemMQTId { get; set; }
    public string Descricao { get; set; } = "";
    public string? Unidade { get; set; }
    public decimal QuantidadeSolicitada { get; set; }
    public bool NaoPrevisto { get; set; }
    public Dictionary<int, decimal?> QuantidadePorProposta { get; set; } = new();
    public Dictionary<int, decimal?> PrecoPorProposta { get; set; } = new();
    public Dictionary<int, decimal?> SubtotalPorProposta { get; set; } = new();
    public Dictionary<int, bool> IncluidoPorProposta { get; set; } = new();
    public Dictionary<int, bool> MelhorPorProposta { get; set; } = new();
}
