using System.Globalization;
using System.Text;
using ComparacaoPropostas.Helper;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;
using S = ComparacaoPropostas.Services.Airtable.AirtableSchema;

namespace ComparacaoPropostas.Services.Airtable;

/// <summary>Um registo a enviar ao Airtable: a chave ("ID App") e os valores por ID de campo.</summary>
public sealed record RegistoAirtable(string Chave, Dictionary<string, object?> Campos);

/// <summary>
/// Converte as entidades da app nos campos do Airtable. Só funções puras (sem BD nem HTTP),
/// para se poder testar o mapeamento isoladamente.
///
/// Convenções:
///  - Valores monetários vão sempre em CVE (os campos do Airtable estão em CVE); propostas em EUR
///    são convertidas com a taxa da própria proposta, como faz a app.
///  - Peso: na app é 0–100, no Airtable é campo percentagem (0–1).
///  - Avaliações: a app guarda uma nota por avaliador; o Airtable soma (Nota × Peso) de todas as
///    avaliações da proposta. Para a Pontuação Ponderada bater certo com a da app, envia-se UMA
///    avaliação por Proposta × Critério com a nota final da app (média dos avaliadores, ou a nota
///    automática para critérios de preço/prazo/garantia/técnico automático).
///  - Valores nulos limpam o campo no Airtable (a app é a fonte de verdade).
/// </summary>
public static class AirtableMapper
{
    public static string ChaveProcesso(int id) => $"PR{id}";
    public static string ChaveCriterio(int id) => $"C{id}";
    public static string ChaveProposta(int id) => $"P{id}";
    public static string ChaveItemProposta(int id) => $"IP{id}";
    public static string ChaveAvaliacao(int propostaId, int criterioId) => $"A{propostaId}-{criterioId}";
    public static string ChaveItemMaterial(int id) => $"IM{id}";

    public static RegistoAirtable Processo(Processo p, string? appBaseUrl)
    {
        var pedido = p.PedidoProposta;
        return new RegistoAirtable(ChaveProcesso(p.Id), new Dictionary<string, object?>
        {
            [S.Processos.Nome] = p.Nome,
            [S.Processos.Descricao] = Vazio(p.Descricao),
            [S.Processos.Status] = StatusProcesso(p.Status),
            [S.Processos.PrazoFinal] = Data(pedido?.PrazoEntrega),
            [S.Processos.OrcamentoEstimado] = pedido?.OrcamentoEstimado,
            [S.Processos.NumeroProcesso] = Vazio(p.NumeroProcesso),
            [S.Processos.Area] = pedido == null ? null : Area(pedido.Area),
            [S.Processos.TipoCompra] = p.TipoCompra == Models.Entities.Enums.TipoCompra.Internacional ? "Internacional" : "Nacional",
            [S.Processos.FornecedorVencedor] = Vazio(p.PropostaVencedora?.Fornecedor),
            [S.Processos.ValorAdjudicadoCve] = p.ValorAdjudicadoCVE,
            [S.Processos.DataAdjudicacao] = Data(p.DataAdjudicacao),
            [S.Processos.LinkApp] = Link(appBaseUrl, $"/Processos/Details/{p.Id}"),
        });
    }

    public static RegistoAirtable Criterio(Criterio c)
        => new(ChaveCriterio(c.Id), new Dictionary<string, object?>
        {
            [S.Criterios.Nome] = c.Nome,
            [S.Criterios.Categoria] = CategoriaCriterio(c.Categoria),
            [S.Criterios.Peso] = Math.Round(c.Peso / 100m, 4),
            [S.Criterios.Descricao] = Vazio(c.Descricao),
        });

    public static RegistoAirtable CriterioProcesso(Criterio c, string processoNome, string? processoRec, string? criterioRec)
        => new(ChaveCriterio(c.Id), new Dictionary<string, object?>
        {
            [S.CriteriosProcesso.Nome] = $"{processoNome} — {c.Nome}",
            [S.CriteriosProcesso.Processo] = Ligacao(processoRec),
            [S.CriteriosProcesso.Criterio] = Ligacao(criterioRec),
        });

    public static RegistoAirtable Proposta(Proposta pr, string? processoRec, string? appBaseUrl)
        => new(ChaveProposta(pr.Id), new Dictionary<string, object?>
        {
            [S.Propostas.Fornecedor] = pr.Fornecedor,
            [S.Propostas.ValorTotal] = pr.ValorTotalCVE,
            [S.Propostas.PrazoEntregaDias] = pr.PrazoEntregaDias,
            [S.Propostas.Validade] = Data(pr.ValidadeProposta),
            [S.Propostas.Status] = StatusProposta(pr.Status),
            [S.Propostas.Observacoes] = Vazio(pr.Observacoes),
            [S.Propostas.Processo] = Ligacao(processoRec),
            [S.Propostas.Moeda] = (pr.Moeda ?? MoedaHelper.MoedaCve).Trim().ToUpperInvariant(),
            [S.Propostas.ValorMoedaOriginal] = pr.ValorTotal,
            [S.Propostas.Garantia] = Vazio(pr.Garantia),
            [S.Propostas.LinkApp] = Link(appBaseUrl, $"/Processos/Details/{pr.ProcessoId}"),
        });

    public static RegistoAirtable ItemProposta(ItemProposta ip, Proposta pr, string? propostaRec, string? itemMaterialRec)
        => new(ChaveItemProposta(ip.Id), new Dictionary<string, object?>
        {
            [S.ItensProposta.Nome] = $"{pr.Fornecedor} - {ip.NomeExibicao}",
            [S.ItensProposta.Incluido] = ip.Incluido,
            [S.ItensProposta.Quantidade] = ip.Quantidade,
            [S.ItensProposta.PrecoUnitario] = MoedaHelper.CalcularTotalCve(ip.PrecoUnitario, pr.Moeda, pr.TaxaCambio),
            [S.ItensProposta.Observacao] = Vazio(ip.Observacao),
            [S.ItensProposta.Proposta] = Ligacao(propostaRec),
            [S.ItensProposta.Item] = Ligacao(itemMaterialRec),
        });

    public static RegistoAirtable ItemMaterial(ItemMaterial im)
        => new(ChaveItemMaterial(im.Id), new Dictionary<string, object?>
        {
            [S.ItensMaterial.Nome] = im.NomeItem,
            [S.ItensMaterial.Categoria] = Vazio(im.Categoria),
            [S.ItensMaterial.Unidade] = Vazio(im.Unidade),
            [S.ItensMaterial.Descricao] = Vazio(im.Descricao),
        });

    /// <summary>
    /// Nota final da proposta num critério, tal como a app a usa no ranking (ScoringService):
    /// critério automático → nota da memória de cálculo; manual → média dos avaliadores.
    /// Devolve null se ainda não houver nota (nesse caso não se envia avaliação).
    /// </summary>
    public static (decimal Nota, string? Comentario)? NotaFinal(Proposta pr, Criterio c)
    {
        if (c.TipoAutomatico != TipoCriterioAutomatico.Nenhum)
        {
            var memoria = pr.MemoriaCalculo.FirstOrDefault(m => m.CriterioId == c.Id);
            if (memoria == null) return null;
            return (Math.Round(memoria.Nota, 2, MidpointRounding.AwayFromZero),
                    Vazio($"Cálculo automático: {memoria.Justificativa}".TrimEnd(' ', ':')));
        }

        var avaliacoes = pr.Avaliacoes.Where(a => a.CriterioId == c.Id).ToList();
        if (avaliacoes.Count == 0) return null;

        var media = Math.Round((decimal)avaliacoes.Average(a => a.Nota), 2, MidpointRounding.AwayFromZero);
        var comentarios = avaliacoes
            .Select(a =>
            {
                var quem = a.Avaliador?.Nome ?? "Avaliador";
                return string.IsNullOrWhiteSpace(a.Comentario) ? $"{quem}: {a.Nota}" : $"{quem}: {a.Nota} — {a.Comentario!.Trim()}";
            });
        return (media, string.Join("\n", comentarios));
    }

    public static RegistoAirtable Avaliacao(Proposta pr, Criterio c, decimal nota, string? comentario,
        string? propostaRec, string? criterioRec, string? criterioProcessoRec)
        => new(ChaveAvaliacao(pr.Id, c.Id), new Dictionary<string, object?>
        {
            [S.Avaliacoes.Nome] = $"{pr.Fornecedor} - {c.Nome}",
            [S.Avaliacoes.Proposta] = Ligacao(propostaRec),
            [S.Avaliacoes.Criterio] = Ligacao(criterioRec),
            [S.Avaliacoes.CriterioProcesso] = Ligacao(criterioProcessoRec),
            [S.Avaliacoes.Nota] = nota,
            [S.Avaliacoes.Comentario] = comentario,
        });

    // ---- valores de escolha (singleSelect) ----

    public static string StatusProcesso(StatusProcesso s) => s switch
    {
        Models.Entities.Enums.StatusProcesso.Criado => "Aberto",
        Models.Entities.Enums.StatusProcesso.Recebido => "Em Avaliação",
        Models.Entities.Enums.StatusProcesso.Concluido => "Decidido",
        Models.Entities.Enums.StatusProcesso.Cancelado => "Cancelado",
        _ => "Aberto"
    };

    public static string StatusProposta(StatusProposta s) => s switch
    {
        Models.Entities.Enums.StatusProposta.Recebida => "Recebida",
        Models.Entities.Enums.StatusProposta.EmAnalise => "Em Avaliação",
        Models.Entities.Enums.StatusProposta.Aceite => "Aprovada",
        Models.Entities.Enums.StatusProposta.Rejeitada => "Reprovada",
        _ => "Recebida"
    };

    public static string Area(AreaDepartamento a) => a switch
    {
        AreaDepartamento.DepInf => "DEP-INF",
        AreaDepartamento.DepPrcs => "DEP-PRCS",
        AreaDepartamento.DepPrm => "DEP-PRM",
        AreaDepartamento.DepSaf => "DEP-SAF",
        _ => a.ToString()
    };

    /// <summary>
    /// Na app a categoria é texto livre ("Técnica", "Tecnico", "Financeira"...). Converte para as
    /// opções do Airtable (Técnico / Financeiro / Comercial); qualquer outro texto passa tal como está.
    /// </summary>
    public static string? CategoriaCriterio(string? categoria)
    {
        if (string.IsNullOrWhiteSpace(categoria)) return null;
        var n = SemAcentos(categoria.Trim()).ToLowerInvariant();
        if (n.StartsWith("tecn")) return "Técnico";
        if (n.StartsWith("financ") || n.StartsWith("preco") || n.StartsWith("custo")) return "Financeiro";
        if (n.StartsWith("comerc")) return "Comercial";
        return categoria.Trim();
    }

    // ---- utilitários ----

    private static string[] Ligacao(string? recordId)
        => string.IsNullOrEmpty(recordId) ? Array.Empty<string>() : new[] { recordId };

    private static string? Data(DateTime? d) => d?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);

    private static string? Vazio(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string? Link(string? baseUrl, string caminho)
        => string.IsNullOrWhiteSpace(baseUrl) ? null : baseUrl.TrimEnd('/') + caminho;

    private static string SemAcentos(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s.Normalize(NormalizationForm.FormD))
            if (CharUnicodeInfo.GetUnicodeCategory(ch) != UnicodeCategory.NonSpacingMark)
                sb.Append(ch);
        return sb.ToString().Normalize(NormalizationForm.FormC);
    }
}
