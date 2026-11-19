using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Services;

namespace ComparacaoPropostas.ViewModels.Planos;

public class PlanoDetailVM
{
    public Plano Plano { get; set; } = null!;
    public ResumoPlano Resumo { get; set; } = new();
}
