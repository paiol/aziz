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
}
