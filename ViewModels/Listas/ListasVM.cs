using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.ViewModels.Listas;

public class ListasVM
{
    public ListaPlano Lista { get; set; }
    public Dictionary<ListaPlano, int> Contagens { get; set; } = new();
    public List<string> CamposQueUsam { get; set; } = new();
    public List<LinhaValorListaVM> Valores { get; set; } = new();
}

public class LinhaValorListaVM
{
    public ValorLista Valor { get; set; } = null!;
    // Nº de projetos do plano que usam este valor.
    public int Usos { get; set; }
}
