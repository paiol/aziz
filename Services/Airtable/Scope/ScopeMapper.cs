using ComparacaoPropostas.Helper;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Services.Airtable.Scope;

/// <summary>
/// Converte um projeto aceite do plano nos campos da tabela SCOPE do Airtable
/// (mesmos nomes de campos da tabela real "SCOPE_V0.1 - Plano 2027").
/// Só funções puras, para se poder testar sem Airtable nem base de dados.
/// </summary>
public static class ScopeMapper
{
    public static string Chave(ProjetoPlano p) => $"PP{p.Id}";

    // Só vão para o SCOPE os projetos aceites que não estão fora do plano.
    public static bool DeveEstarNoScope(ProjetoPlano p)
        => p.EstadoAprovacao == EstadoAprovacaoProjeto.Aceite && p.Caixa != CaixaPlano.ForaDoPlano;

    // Opções que existem no Airtable — valores fora destas listas não são enviados,
    // para não criar opções novas por engano (o envio usa typecast).
    private static readonly HashSet<string> Coordenacoes = new() { "CRN", "CRS", "SSM", "CLI", "CRF", "NAC" };
    private static readonly HashSet<string> Ilhas = new() { "SA", "SV", "SN", "SL", "BV", "MA", "ST-PR", "ST-INT", "FG", "BR", "NAC" };
    private static readonly HashSet<string> Donos = new() { "DEI", "DLG", "DOM", "CVWC", "GCA" };

    public static Dictionary<string, object?> Campos(ProjetoPlano p, int anoPlano)
    {
        var historico = string.Join(" · ", new[]
        {
            $"Plataforma DEI · Plano {anoPlano}",
            p.Situacao?.ToLabel(),
            string.IsNullOrWhiteSpace(p.DesignacaoRubrica) ? null : $"Rubrica: {p.DesignacaoRubrica}"
        }.Where(s => !string.IsNullOrWhiteSpace(s)));

        return new Dictionary<string, object?>
        {
            ["🟠Nome Projeto Original"] = p.Nome,
            ["🟠Enquadramento CAPEX"] = Enquadramento(p.Situacao, anoPlano),
            ["Área DEP"] = new[] { p.Area.ToLabel() },
            ["🟠Coord"] = SoSeExistir(p.Coordenacao, Coordenacoes),
            ["🟠Ilha"] = SoSeExistir(Ilha(p.Ilha), Ilhas),
            ["Dono Proj."] = SoSeExistir(p.OrigemIniciativa, Donos),
            ["🟠Orientação Prioridade (DEP)"] = Prioridade(p.Prioridade),
            ["🟠Descrição âmbito"] = p.Descricao,
            ["🟠Histórico Proj."] = historico,
            ["Orçamento transitado CVE"] = Valor(p.ValorTransitar),
            [$"🟠Orçamento Projeto CVE Plano {anoPlano}"] = Valor(p.TotalOrcamento)
        };
    }

    // Transitado / Transitado parcial → "Transitado"; Novo → "Novo <ano>"; o resto fica vazio.
    public static string? Enquadramento(SituacaoProjetoPlano? situacao, int anoPlano) => situacao switch
    {
        SituacaoProjetoPlano.Transitado or SituacaoProjetoPlano.TransitadoParcial => "Transitado",
        SituacaoProjetoPlano.Novo => $"Novo {anoPlano}",
        _ => null
    };

    // Alta → P1, Média → P2, Baixa → P3, sem prioridade → tbd (P0 só se define no Airtable).
    public static string Prioridade(PrioridadeProjetoPlano? prioridade) => prioridade switch
    {
        PrioridadeProjetoPlano.Alta => "P1",
        PrioridadeProjetoPlano.Media => "P2",
        PrioridadeProjetoPlano.Baixa => "P3",
        _ => "tbd"
    };

    // A app usa "MO" para o Maio (como o ficheiro SCOPE); o Airtable usa "MA".
    public static string? Ilha(string? ilha) => ilha?.Trim().ToUpperInvariant() switch
    {
        null or "" => null,
        "MO" => "MA",
        var i => i
    };

    private static string? SoSeExistir(string? valor, HashSet<string> opcoes)
    {
        var v = valor?.Trim();
        return !string.IsNullOrEmpty(v) && opcoes.Contains(v) ? v : null;
    }

    private static decimal? Valor(decimal? v) => v is decimal d ? Math.Round(d, 0) : null;
}
