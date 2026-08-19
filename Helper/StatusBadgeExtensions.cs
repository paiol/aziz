using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Helper;

public static class StatusBadgeExtensions
{
    public static string ToBadgeClass(this StatusProcesso status) => status switch
    {
        StatusProcesso.Criado => "badge bg-primary",
        StatusProcesso.Recebido => "badge bg-warning text-dark",
        StatusProcesso.Concluido => "badge bg-success",
        StatusProcesso.Cancelado => "badge bg-secondary",
        _ => "badge bg-secondary"
    };

    public static string ToLabel(this StatusProcesso status) => status switch
    {
        StatusProcesso.Criado => "Criado",
        StatusProcesso.Recebido => "Recebido",
        StatusProcesso.Concluido => "Concluído",
        StatusProcesso.Cancelado => "Cancelado",
        _ => status.ToString()
    };

    public static string ToBadgeClass(this StatusProposta status) => status switch
    {
        StatusProposta.Recebida => "badge bg-info text-dark",
        StatusProposta.EmAnalise => "badge bg-warning text-dark",
        StatusProposta.Aceite => "badge bg-success",
        StatusProposta.Rejeitada => "badge bg-danger",
        _ => "badge bg-secondary"
    };

    public static string ToLabel(this StatusProposta status) => status switch
    {
        StatusProposta.Recebida => "Recebida",
        StatusProposta.EmAnalise => "Em Análise",
        StatusProposta.Aceite => "Aceite",
        StatusProposta.Rejeitada => "Rejeitada",
        _ => status.ToString()
    };

    public static string ToBadgeClass(this StatusPedido status) => status switch
    {
        StatusPedido.EmCurso => "badge bg-warning text-dark",
        StatusPedido.Finalizado => "badge bg-success",
        StatusPedido.Cancelado => "badge bg-secondary",
        _ => "badge bg-secondary"
    };

    public static string ToLabel(this StatusPedido status) => status switch
    {
        StatusPedido.EmCurso => "Em Curso",
        StatusPedido.Finalizado => "Finalizado",
        StatusPedido.Cancelado => "Cancelado",
        _ => status.ToString()
    };

    public static string ToLabel(this AreaDepartamento area) => area switch
    {
        AreaDepartamento.DepInf => "DEP-INF",
        AreaDepartamento.DepPrcs => "DEP-PRCS",
        AreaDepartamento.DepPrm => "DEP-PRM",
        AreaDepartamento.DepSaf => "DEP-SAF",
        _ => area.ToString()
    };

    public static string ToLabel(this TipoCompra tipoCompra) => tipoCompra switch
    {
        TipoCompra.Nacional => "Nacional",
        TipoCompra.Internacional => "Internacional",
        _ => tipoCompra.ToString()
    };

    public static string ToBadgeClass(this StatusProjetoObra status) => status switch
    {
        StatusProjetoObra.EmConcurso => "badge bg-primary",
        StatusProjetoObra.EmExecucao => "badge bg-warning text-dark",
        StatusProjetoObra.Concluido => "badge bg-success",
        _ => "badge bg-secondary"
    };

    public static string ToLabel(this StatusProjetoObra status) => status switch
    {
        StatusProjetoObra.EmConcurso => "Em Concurso",
        StatusProjetoObra.EmExecucao => "Em Execução",
        StatusProjetoObra.Concluido => "Concluído",
        _ => status.ToString()
    };

    public static string ToLabel(this TipoProjetoObra tipo) => tipo switch
    {
        TipoProjetoObra.Edificacao => "Edificação",
        TipoProjetoObra.Infraestrutura => "Infraestrutura",
        TipoProjetoObra.Especiais => "Especiais",
        _ => tipo.ToString()
    };

    public static string ToBadgeClass(this StatusPropostaEmpreiteiro status) => status switch
    {
        StatusPropostaEmpreiteiro.Recebida => "badge bg-info text-dark",
        StatusPropostaEmpreiteiro.EmAnalise => "badge bg-warning text-dark",
        StatusPropostaEmpreiteiro.Aceite => "badge bg-success",
        StatusPropostaEmpreiteiro.Rejeitada => "badge bg-danger",
        _ => "badge bg-secondary"
    };

    public static string ToLabel(this StatusPropostaEmpreiteiro status) => status switch
    {
        StatusPropostaEmpreiteiro.Recebida => "Recebida",
        StatusPropostaEmpreiteiro.EmAnalise => "Em Análise",
        StatusPropostaEmpreiteiro.Aceite => "Aceite",
        StatusPropostaEmpreiteiro.Rejeitada => "Rejeitada",
        _ => status.ToString()
    };

    public static string ToLabel(this TipoDocumentoObra tipo) => tipo switch
    {
        TipoDocumentoObra.Desenhos => "Desenhos",
        TipoDocumentoObra.CadernoEncargos => "Caderno de Encargos",
        TipoDocumentoObra.MapaQuantidades => "Mapa de Quantidades",
        TipoDocumentoObra.Fotografias => "Fotografias",
        TipoDocumentoObra.DocDiversos => "Doc. Diversos",
        _ => tipo.ToString()
    };
}
