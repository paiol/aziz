using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Services;

public interface IEmailService
{
    Task EnviarNotificacaoDecisaoAsync(Processo processo);
}
