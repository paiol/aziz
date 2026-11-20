using System.Globalization;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Helper;

public static class PlanoLabelExtensions
{
    private static readonly CultureInfo PtPT = new("pt-PT");

    public static string ToLabel(this SituacaoProjetoPlano situacao) => situacao switch
    {
        SituacaoProjetoPlano.Transitado => "Transitado",
        SituacaoProjetoPlano.TransitadoParcial => "Transitado parcial",
        SituacaoProjetoPlano.Novo => "Novo",
        SituacaoProjetoPlano.Condicionado => "Condicionado",
        SituacaoProjetoPlano.ExecutadoRetirar => "Executado – retirar",
        SituacaoProjetoPlano.Cancelado => "Cancelado",
        SituacaoProjetoPlano.ExploracaoDom => "Exploração - DOM",
        _ => situacao.ToString()
    };

    public static string ToLabel(this CaixaPlano caixa) => caixa switch
    {
        CaixaPlano.Entra => "Entra",
        CaixaPlano.Condicionado => "Condicionado",
        CaixaPlano.ForaDoPlano => "Fora do plano",
        CaixaPlano.SemSituacao => "Sem situação",
        _ => caixa.ToString()
    };

    public static string ToBadgeClass(this CaixaPlano caixa) => caixa switch
    {
        CaixaPlano.Entra => "badge bg-success",
        CaixaPlano.Condicionado => "badge bg-warning text-dark",
        CaixaPlano.ForaDoPlano => "badge bg-secondary",
        _ => "badge bg-light text-dark"
    };

    public static string ToLabel(this EstadoAprovacaoProjeto estado) => estado switch
    {
        EstadoAprovacaoProjeto.Proposto => "Proposto",
        EstadoAprovacaoProjeto.Aceite => "Aceite",
        EstadoAprovacaoProjeto.Rejeitado => "Rejeitado",
        _ => estado.ToString()
    };

    public static string ToBadgeClass(this EstadoAprovacaoProjeto estado) => estado switch
    {
        EstadoAprovacaoProjeto.Aceite => "badge bg-success",
        EstadoAprovacaoProjeto.Rejeitado => "badge bg-danger",
        _ => "badge bg-info text-dark"
    };

    public static string ToLabel(this PrioridadeProjetoPlano prioridade) => prioridade switch
    {
        PrioridadeProjetoPlano.Alta => "Alta",
        PrioridadeProjetoPlano.Media => "Média",
        PrioridadeProjetoPlano.Baixa => "Baixa",
        _ => prioridade.ToString()
    };

    public static string ToLabel(this TipoAcaoFormacao tipo) => tipo switch
    {
        TipoAcaoFormacao.Formacao => "Formação",
        TipoAcaoFormacao.Feira => "Feira",
        _ => tipo.ToString()
    };

    // Valores do plano em CVE, sem casas decimais (como nas folhas Resumo e Painel).
    public static string ToCve(this decimal valor) => valor.ToString("N0", PtPT) + " CVE";

    public static string ToCve(this decimal? valor) => valor.HasValue ? valor.Value.ToCve() : "-";

    public static string ToDias(this decimal valor) => valor.ToString("0.#", PtPT);

    public static string ToPercent(this decimal? valor) => valor.HasValue ? valor.Value.ToString("P0", PtPT) : "-";
}
