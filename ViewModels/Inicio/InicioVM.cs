using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Services;

namespace ComparacaoPropostas.ViewModels.Inicio;

// Página Início da Plataforma DEI: um cartão por módulo, com indicadores e gráficos.
public class InicioVM
{
    // Plano Capex (plano do ano corrente; senão o próximo; senão o mais recente)
    public Plano? Plano { get; set; }
    public ResumoPlano? ResumoPlano { get; set; }
    public int NumPlanos { get; set; }

    // Capex: execução dos projetos aceites desse plano, por área
    public ExecucaoProjetoCapex ExecucaoCapex { get; set; } = new();
    public List<(AreaDepartamento Area, decimal Orcamento, decimal Adjudicado)> CapexPorArea { get; set; } = new();

    // Compras
    public int PedidosEmCurso { get; set; }
    public int PedidosSemProcesso { get; set; }
    public int PedidosSemProjeto { get; set; }
    public Dictionary<StatusProcesso, int> ProcessosPorEstado { get; set; } = new();
    public decimal AdjudicadoAno { get; set; }
    public List<(string Mes, decimal Valor)> AdjudicadoPorMes { get; set; } = new();

    // Obras
    public Dictionary<StatusProjetoObra, int> ObrasPorEstado { get; set; } = new();

    // Base de dados
    public int NumFornecedores { get; set; }
    public int NumItensMaterial { get; set; }
    public int NumAvaliadores { get; set; }
}
