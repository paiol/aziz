using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Helper;

public static class StatusBadgeExtensions
{
    public static string ToBadgeClass(this StatusProcesso status) => status switch
    {
        StatusProcesso.Aberto => "badge bg-primary",
        StatusProcesso.EmAvaliacao => "badge bg-warning text-dark",
        StatusProcesso.Decidido => "badge bg-success",
        StatusProcesso.Cancelado => "badge bg-secondary",
        _ => "badge bg-secondary"
    };

    public static string ToLabel(this StatusProcesso status) => status switch
    {
        StatusProcesso.Aberto => "Aberto",
        StatusProcesso.EmAvaliacao => "Em Avaliação",
        StatusProcesso.Decidido => "Decidido",
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
}
