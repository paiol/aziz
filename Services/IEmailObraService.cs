using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Services;

public interface IEmailObraService
{
    string ConstruirLinkMailto(ProjetoObra projeto);
    (string Destinatarios, string Assunto, string Corpo) ObterDadosEmailParaCopiar(ProjetoObra projeto);
}
