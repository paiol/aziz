using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.ViewModels.Processos;

namespace ComparacaoPropostas.Helper;

// Alertas de tempo por estado do processo — quando um processo fica tempo
// demasiado longo no mesmo estado, sinaliza-se com uma cor de alerta em vez
// da cor normal do estado.
public static class ProcessoAlertaHelper
{
    private const int LimiteDiasCriado = 20;
    private const int LimiteDiasRecebido = 60;
    private const int LimiteDiasConcluidoSemAdjudicacao = 10;

    private static int DiasNoEstadoAtual(DateTime statusAlteradoEm)
        => Math.Max(0, (int)(DateTime.UtcNow - statusAlteradoEm).TotalDays);

    private static bool TemAlertaDePrazo(StatusProcesso status, DateTime statusAlteradoEm, bool temVencedor)
    {
        var dias = DiasNoEstadoAtual(statusAlteradoEm);
        return status switch
        {
            StatusProcesso.Criado => dias >= LimiteDiasCriado,
            StatusProcesso.Recebido => dias >= LimiteDiasRecebido,
            StatusProcesso.Concluido => dias >= LimiteDiasConcluidoSemAdjudicacao && !temVencedor,
            _ => false
        };
    }

    private static string? MensagemAlertaDePrazo(StatusProcesso status, DateTime statusAlteradoEm, bool temVencedor)
    {
        if (!TemAlertaDePrazo(status, statusAlteradoEm, temVencedor)) return null;
        var dias = DiasNoEstadoAtual(statusAlteradoEm);

        return status switch
        {
            StatusProcesso.Criado => $"Há {dias} dias em Criado sem receber propostas (limite: {LimiteDiasCriado} dias).",
            StatusProcesso.Recebido => $"Há {dias} dias em Recebido sem ser concluído (limite: {LimiteDiasRecebido} dias).",
            StatusProcesso.Concluido => $"Concluído há {dias} dias sem adjudicação registada (limite: {LimiteDiasConcluidoSemAdjudicacao} dias).",
            _ => null
        };
    }

    private static string ToBadgeClassComAlerta(StatusProcesso status, DateTime statusAlteradoEm, bool temVencedor)
        => TemAlertaDePrazo(status, statusAlteradoEm, temVencedor) ? "badge bg-danger" : status.ToBadgeClass();

    public static int DiasNoEstadoAtual(this Processo processo) => DiasNoEstadoAtual(processo.StatusAlteradoEm);
    public static bool TemAlertaDePrazo(this Processo processo) => TemAlertaDePrazo(processo.Status, processo.StatusAlteradoEm, processo.PropostaVencedoraId.HasValue);
    public static string? MensagemAlertaDePrazo(this Processo processo) => MensagemAlertaDePrazo(processo.Status, processo.StatusAlteradoEm, processo.PropostaVencedoraId.HasValue);
    public static string ToBadgeClassComAlerta(this Processo processo) => ToBadgeClassComAlerta(processo.Status, processo.StatusAlteradoEm, processo.PropostaVencedoraId.HasValue);

    public static bool TemAlertaDePrazo(this ProcessoIndexVM processo) => TemAlertaDePrazo(processo.Status, processo.StatusAlteradoEm, processo.TemVencedor);
    public static string? MensagemAlertaDePrazo(this ProcessoIndexVM processo) => MensagemAlertaDePrazo(processo.Status, processo.StatusAlteradoEm, processo.TemVencedor);
    public static string ToBadgeClassComAlerta(this ProcessoIndexVM processo) => ToBadgeClassComAlerta(processo.Status, processo.StatusAlteradoEm, processo.TemVencedor);
}
