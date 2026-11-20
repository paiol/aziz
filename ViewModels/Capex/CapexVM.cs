using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Services;

namespace ComparacaoPropostas.ViewModels.Capex;

public class CapexVM
{
    public Plano Plano { get; set; } = null!;
    public List<Plano> Planos { get; set; } = new();
    public AreaDepartamento? Area { get; set; }
    public List<ResumoAreaCapexVM> PorArea { get; set; } = new();
    public List<LinhaCapexVM> Linhas { get; set; } = new();
    public ExecucaoProjetoCapex Total { get; set; } = new();
}

public class ResumoAreaCapexVM
{
    public AreaDepartamento Area { get; set; }
    public int NumProjetos { get; set; }
    public decimal Orcamento { get; set; }
    public decimal Adjudicado { get; set; }
    public decimal EmCurso { get; set; }
    public decimal Saldo => Orcamento - Adjudicado;
}

public class LinhaCapexVM
{
    public ProjetoPlano Projeto { get; set; } = null!;
    public ExecucaoProjetoCapex Execucao { get; set; } = new();
}
