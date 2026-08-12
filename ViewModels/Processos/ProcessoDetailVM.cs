using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.ViewModels.Processos;

public class ProcessoDetailVM
{
    public Processo Processo { get; set; } = null!;
    public List<PropostaResumo> Propostas { get; set; } = new();
    public decimal SomaPesos { get; set; }
    public int TotalItensPedido { get; set; }
}

public class PropostaResumo
{
    public int Id { get; set; }
    public string Fornecedor { get; set; } = "";
    public decimal ValorTotal { get; set; }
    public decimal PontuacaoPonderada { get; set; }
    public Models.Entities.Enums.StatusProposta Status { get; set; }
}
