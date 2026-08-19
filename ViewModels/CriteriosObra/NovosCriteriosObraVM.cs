using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.ViewModels.CriteriosObra;

public class NovosCriteriosObraVM
{
    public int ProjetoObraId { get; set; }
    public List<CriterioObra> Itens { get; set; } = new();
}
