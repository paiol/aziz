using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Services;

public interface IEmailService
{
    Task EnviarNotificacaoDecisaoAsync(Processo processo);
    string ConstruirLinkMailto(Processo processo);
    (string Destinatarios, string Assunto, string Corpo) ObterDadosEmailParaCopiar(Processo processo);
    byte[] GerarDocumentoWord(Processo processo);
    byte[] GerarDocumentoPdf(Processo processo);
}
