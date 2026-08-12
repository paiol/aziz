using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.ViewModels.Criterios;

public class NovosCriteriosVM
{
    public int ProcessoId { get; set; }
    public List<Criterio> Itens { get; set; } = new();
}
