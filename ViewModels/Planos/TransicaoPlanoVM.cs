using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.ViewModels.Planos;

// Revisão, projeto a projeto, do que passa do plano de um ano para o plano do ano seguinte.
public class TransicaoPlanoVM
{
    public int PlanoAnteriorId { get; set; }
    public string PlanoAnteriorDesignacao { get; set; } = "";
    public int AnoNovo { get; set; }
    public string DesignacaoNova { get; set; } = "";
    // Se já existe um plano para o ano seguinte, os projetos são acrescentados a esse plano.
    public int? PlanoExistenteId { get; set; }
    public List<LinhaTransicaoVM> Linhas { get; set; } = new();
}

public class LinhaTransicaoVM
{
    public int ProjetoAnteriorId { get; set; }
    public string Nome { get; set; } = "";
    public AreaDepartamento Area { get; set; }
    public string? Rubrica { get; set; }
    public SituacaoProjetoPlano? SituacaoAnterior { get; set; }
    public EstadoAprovacaoProjeto EstadoAnterior { get; set; }
    public decimal Orcamento { get; set; }
    public decimal Adjudicado { get; set; }
    // Já transitou antes para o plano do ano seguinte (não volta a ser copiado).
    public bool JaTransitado { get; set; }

    // Decisão: null = não copiar; Transitado / Transitado parcial / Condicionado = entra;
    // Executado / Cancelado / Exploração - DOM = fica registado como fora do plano.
    public SituacaoProjetoPlano? Decisao { get; set; }
    public decimal? ValorTransitar { get; set; }
}
