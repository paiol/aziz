using System.Globalization;
using System.Text.RegularExpressions;
using ClosedXML.Excel;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Services;

public class PlanoImportado
{
    public int? Ano { get; set; }
    public string? Designacao { get; set; }
    public decimal? CapacidadeArquiteturaDias { get; set; }
    public decimal? CapacidadeEnergiaDias { get; set; }
    public List<ProjetoPlano> Projetos { get; set; } = new();
    public List<AcaoFormacaoPlano> AcoesFormacao { get; set; } = new();
    public List<string> Avisos { get; set; } = new();
}

public interface IPlanoExcelService
{
    PlanoImportado LerPlanoExcel(Stream ficheiro);
}

// Lê o ficheiro SCOPE_INF (folhas "Scope DEP-INF AAAA", "Formação e feiras AAAA" e "Parâmetros plano").
// As colunas são encontradas pelo nome do cabeçalho — os anos no cabeçalho ("Situação 2027",
// "Cod Capex 2026"…) são relativos ao ano do plano, para o mesmo importador servir em anos seguintes.
// Das células com fórmula usa-se o último valor calculado pelo Excel.
public class PlanoExcelService : IPlanoExcelService
{
    private static readonly CultureInfo PtPT = new("pt-PT");

    public PlanoImportado LerPlanoExcel(Stream ficheiro)
    {
        var resultado = new PlanoImportado();
        using var workbook = new XLWorkbook(ficheiro);

        var scope = workbook.Worksheets.FirstOrDefault(w => Normalizar(w.Name).StartsWith("scope"));
        if (scope == null)
        {
            resultado.Avisos.Add("Não foi encontrada a folha \"Scope\" no ficheiro.");
        }
        else
        {
            LerScope(scope, resultado);
        }

        var formacao = workbook.Worksheets.FirstOrDefault(w => Normalizar(w.Name).StartsWith("formação"));
        if (formacao != null) LerFormacao(formacao, resultado);

        var parametros = workbook.Worksheets.FirstOrDefault(w => Normalizar(w.Name) == "parâmetros plano");
        if (parametros != null) LerCapacidade(parametros, resultado);

        var resumo = workbook.Worksheets.FirstOrDefault(w => Normalizar(w.Name).StartsWith("resumo"));
        var titulo = resumo?.Cell(1, 1).GetString().Trim();
        if (!string.IsNullOrWhiteSpace(titulo))
        {
            resultado.Designacao = Regex.Replace(titulo, @"\s*[—–-]\s*Resumo\s*$", "", RegexOptions.IgnoreCase).Trim();
        }
        if (string.IsNullOrWhiteSpace(resultado.Designacao) && resultado.Ano.HasValue)
        {
            resultado.Designacao = $"Plano {resultado.Ano} DEP-INF";
        }

        return resultado;
    }

    private static void LerScope(IXLWorksheet sheet, PlanoImportado resultado)
    {
        var cabecalho = sheet.Row(1);
        var ultimaColuna = sheet.LastColumnUsed()?.ColumnNumber() ?? 0;

        var cabecalhos = new List<(int Coluna, string Texto)>();
        for (var c = 1; c <= ultimaColuna; c++)
        {
            var texto = Normalizar(cabecalho.Cell(c).GetString());
            if (texto.Length > 0) cabecalhos.Add((c, texto));
        }

        // Ano do plano: o do cabeçalho "Situação AAAA", ou o do nome da folha.
        var ano = cabecalhos.Select(h => Regex.Match(h.Texto, @"^situação (\d{4})$")).FirstOrDefault(m => m.Success)?.Groups[1].Value
                  ?? Regex.Match(sheet.Name, @"(\d{4})").Groups[1].Value;
        if (int.TryParse(ano, out var anoPlano)) resultado.Ano = anoPlano;

        // Troca os anos por marcadores relativos: {a} = ano do plano, {a-1}, {a-2}.
        var relativos = cabecalhos
            .Select(h => (h.Coluna, Texto: resultado.Ano is int a ? AnosRelativos(h.Texto, a) : h.Texto))
            .ToList();

        int? Col(string chave)
        {
            var exata = relativos.FirstOrDefault(h => h.Texto == chave);
            if (exata.Texto != null) return exata.Coluna;
            var prefixo = relativos.FirstOrDefault(h => h.Texto.StartsWith(chave));
            return prefixo.Texto != null ? prefixo.Coluna : null;
        }

        var colProjeto = Col("projeto");
        if (colProjeto == null)
        {
            resultado.Avisos.Add("A folha Scope não tem a coluna \"Projeto\".");
            return;
        }

        var cols = new
        {
            CapexAnoMenos2 = Col("capex {a-2}"),
            CodCapexAnoAnterior = Col("cod capex {a-1}"),
            CodCapex = Col("cod capex {a}"),
            Seguimento = Col("seguimento orçamento"),
            Rubrica = Col("designação da rubrica"),
            Origem = Col("origem iniciativa"),
            Tipo = Col("tipo"),
            Coord = Col("coord."),
            Ilha = Col("ilha"),
            Classificacao = Col("classificação projeto"),
            Descricao = Col("descrição"),
            Observacoes = Col("observações"),
            Capex = Col("capex"),
            Situacao = Col("situação {a}"),
            Prioridade = Col("prioridade"),
            Trimestre = Col("trimestre previsto"),
            Execucao = Col("execução {a-1}"),
            Zona = Col("zona"),
            ExecProjeto = Col("execução · projeto"),
            FiscCivil = Col("fiscalização · civil"),
            FiscTracados = Col("fiscalização · traçados"),
            ExecMontagem = Col("execução · montagem"),
            Perfil = Col("perfil / origem"),
            FiscMontagem = Col("fiscalização · montagem"),
            Prestador = Col("prestador externo"),
            Necessidade = Col("necessidade aquisição"),
            Entidades = Col("entidades executantes"),
            PropostasSemAdj = Col("propostas sem adjudicação"),
            AdjAnterior = Col("adjudicado {a-1}"),
            AdjAnteriores = Col("adjudicado anos anteriores"),
            Transitar = Col("valor a transitar"),
            Novo = Col("valor novo"),
            Condicionado = Col("valor condicionado"),
            TotalRef = Col("total orc. {a-1}"),
            AquisicaoSite = Col("aquisição site"),
            Equipamento = Col("equipamento"),
            Construcoes = Col("construções"),
            Servicos = Col("serviços"),
            Deslocacoes = Col("deslocações da equipa"),
            Terceirizacao = Col("dos quais terceirização"),
            Condicao = Col("condição / motivo"),
            Inicio = Col("início (mês)"),
            Fim = Col("fim (mês)"),
            EsforcoArq = Col("esforço {a} · arquitetura"),
            EsforcoEne = Col("esforço {a} · energia")
        };

        var ultimaLinha = sheet.LastRowUsed()?.RowNumber() ?? 1;
        for (var r = 2; r <= ultimaLinha; r++)
        {
            var row = sheet.Row(r);
            var nome = Texto(row, colProjeto);
            if (string.IsNullOrWhiteSpace(nome)) continue;

            var situacaoTexto = Texto(row, cols.Situacao);
            var situacao = ParseSituacao(situacaoTexto);
            if (situacao == null && !string.IsNullOrWhiteSpace(situacaoTexto))
                resultado.Avisos.Add($"Linha {r} (\"{nome}\"): situação \"{situacaoTexto}\" não reconhecida — ficou sem situação.");

            resultado.Projetos.Add(new ProjetoPlano
            {
                Nome = nome,
                CapexAnoMenos2 = Texto(row, cols.CapexAnoMenos2),
                CodCapexAnoAnterior = Texto(row, cols.CodCapexAnoAnterior),
                CodCapex = Texto(row, cols.CodCapex),
                SeguimentoOrcamento = Texto(row, cols.Seguimento),
                DesignacaoRubrica = Texto(row, cols.Rubrica),
                OrigemIniciativa = Texto(row, cols.Origem),
                Tipo = Texto(row, cols.Tipo),
                Coordenacao = Texto(row, cols.Coord),
                Ilha = Texto(row, cols.Ilha),
                Classificacao = Texto(row, cols.Classificacao),
                Descricao = Texto(row, cols.Descricao),
                Observacoes = Texto(row, cols.Observacoes),
                Capex = Texto(row, cols.Capex),
                Situacao = situacao,
                Prioridade = ParsePrioridade(Texto(row, cols.Prioridade)),
                TrimestrePrevisto = Texto(row, cols.Trimestre),
                ExecucaoAnoAnterior = Numero(row, cols.Execucao),
                Zona = Texto(row, cols.Zona),
                ExecucaoProjeto = Texto(row, cols.ExecProjeto),
                FiscalizacaoCivil = Texto(row, cols.FiscCivil),
                FiscalizacaoTracados = Texto(row, cols.FiscTracados),
                ExecucaoMontagem = Texto(row, cols.ExecMontagem),
                PerfilEquipaMontagem = Texto(row, cols.Perfil),
                FiscalizacaoMontagem = Texto(row, cols.FiscMontagem),
                PrestadorExternoIlha = Texto(row, cols.Prestador),
                NecessidadeAquisicao = Texto(row, cols.Necessidade),
                EntidadesExecutantes = Texto(row, cols.Entidades),
                PropostasSemAdjudicacao = Numero(row, cols.PropostasSemAdj),
                AdjudicadoAnoAnterior = Numero(row, cols.AdjAnterior),
                AdjudicadoAnosAnteriores = Numero(row, cols.AdjAnteriores),
                ValorTransitar = Numero(row, cols.Transitar),
                ValorNovo = Numero(row, cols.Novo),
                ValorCondicionado = Numero(row, cols.Condicionado),
                TotalOrcamentoAnoAnterior = Numero(row, cols.TotalRef),
                CustoAquisicaoSite = Numero(row, cols.AquisicaoSite),
                CustoEquipamento = Numero(row, cols.Equipamento),
                CustoConstrucoes = Numero(row, cols.Construcoes),
                CustoServicos = Numero(row, cols.Servicos),
                CustoDeslocacoes = Numero(row, cols.Deslocacoes),
                CustoTerceirizacao = Numero(row, cols.Terceirizacao),
                CondicaoMotivo = Texto(row, cols.Condicao),
                MesInicio = ParseMes(Texto(row, cols.Inicio)),
                MesFim = ParseMes(Texto(row, cols.Fim)),
                EsforcoArquiteturaDias = Numero(row, cols.EsforcoArq),
                EsforcoEnergiaDias = Numero(row, cols.EsforcoEne)
            });
        }
    }

    private static void LerFormacao(IXLWorksheet sheet, PlanoImportado resultado)
    {
        var cabecalho = sheet.RowsUsed().FirstOrDefault(r =>
            r.CellsUsed().Any(c => Normalizar(c.GetString()) == "ação") &&
            r.CellsUsed().Any(c => Normalizar(c.GetString()) == "prioridade"));
        if (cabecalho == null) return;

        int? Col(string chave) => cabecalho.CellsUsed()
            .Where(c => Normalizar(c.GetString()).StartsWith(chave))
            .Select(c => (int?)c.Address.ColumnNumber)
            .FirstOrDefault();

        var colAcao = Col("ação")!;
        var cols = new
        {
            Prioridade = Col("prioridade"),
            Tipo = Col("tipo"),
            Datas = Col("datas"),
            Local = Col("local"),
            Formato = Col("formato"),
            Ligacao = Col("ligação"),
            Pessoas = Col("nº pessoas"),
            Dias = Col("dias"),
            Inscricao = Col("inscrição"),
            Passagem = Col("passagem"),
            Estadia = Col("estadia"),
            Ajuda = Col("ajuda"),
            NoPlano = Col("no plano"),
            Participantes = Col("participantes"),
            Fonte = Col("fonte"),
            Observacoes = Col("observações"),
            Ano = Col("ano")
        };

        var ultimaLinha = sheet.LastRowUsed()?.RowNumber() ?? 0;
        for (var r = cabecalho.RowNumber() + 1; r <= ultimaLinha; r++)
        {
            var row = sheet.Row(r);
            var acao = Texto(row, colAcao);
            if (string.IsNullOrWhiteSpace(acao)) continue;

            var prioridade = (int?)Numero(row, cols.Prioridade) ?? 3;
            resultado.AcoesFormacao.Add(new AcaoFormacaoPlano
            {
                Acao = acao,
                Prioridade = Math.Clamp(prioridade, 1, 3),
                Tipo = Normalizar(Texto(row, cols.Tipo) ?? "") == "feira" ? TipoAcaoFormacao.Feira : TipoAcaoFormacao.Formacao,
                Datas = Texto(row, cols.Datas),
                Local = Texto(row, cols.Local),
                Formato = Texto(row, cols.Formato),
                LigacaoProjetos = Texto(row, cols.Ligacao),
                NumeroPessoas = (int?)Numero(row, cols.Pessoas),
                Dias = Numero(row, cols.Dias),
                InscricaoPorPessoa = Numero(row, cols.Inscricao),
                PassagemPorPessoa = Numero(row, cols.Passagem),
                EstadiaDia = Numero(row, cols.Estadia),
                AjudaCustoDia = Numero(row, cols.Ajuda),
                NoPlano = Normalizar(Texto(row, cols.NoPlano) ?? "") == "sim",
                Participantes = Texto(row, cols.Participantes),
                Fonte = Texto(row, cols.Fonte),
                Observacoes = Texto(row, cols.Observacoes),
                Ano = (int?)Numero(row, cols.Ano)
            });
        }
    }

    private static void LerCapacidade(IXLWorksheet sheet, PlanoImportado resultado)
    {
        var linha = sheet.RowsUsed().FirstOrDefault(r => Normalizar(r.Cell(1).GetString()).StartsWith("dias disponíveis para projetos"));
        if (linha == null) return;
        resultado.CapacidadeArquiteturaDias = Arredondar(Numero(linha, 2));
        resultado.CapacidadeEnergiaDias = Arredondar(Numero(linha, 3));
    }

    public static string Normalizar(string? texto)
        => Regex.Replace(texto ?? "", @"\s+", " ").Trim().ToLowerInvariant();

    public static string AnosRelativos(string texto, int ano)
        => Regex.Replace(texto, @"\b(\d{4})\b", m =>
        {
            var y = int.Parse(m.Value);
            return y == ano ? "{a}" : y >= ano - 3 && y < ano ? $"{{a-{ano - y}}}" : y > ano && y <= ano + 3 ? $"{{a+{y - ano}}}" : m.Value;
        });

    public static SituacaoProjetoPlano? ParseSituacao(string? texto)
    {
        var t = Normalizar(texto).Replace('–', '-').Replace('—', '-');
        return t switch
        {
            "transitado" => SituacaoProjetoPlano.Transitado,
            "transitado parcial" => SituacaoProjetoPlano.TransitadoParcial,
            "novo" => SituacaoProjetoPlano.Novo,
            "condicionado" => SituacaoProjetoPlano.Condicionado,
            "executado - retirar" or "executado" => SituacaoProjetoPlano.ExecutadoRetirar,
            "cancelado" => SituacaoProjetoPlano.Cancelado,
            "exploração - dom" => SituacaoProjetoPlano.ExploracaoDom,
            _ => null
        };
    }

    public static PrioridadeProjetoPlano? ParsePrioridade(string? texto) => Normalizar(texto) switch
    {
        "alta" => PrioridadeProjetoPlano.Alta,
        "média" or "media" => PrioridadeProjetoPlano.Media,
        "baixa" => PrioridadeProjetoPlano.Baixa,
        _ => null
    };

    public static int? ParseMes(string? texto)
    {
        var t = Normalizar(texto);
        if (t.Length == 0) return null;
        if (int.TryParse(t, out var n) && n is >= 1 and <= 12) return n;
        var i = Array.FindIndex(PlanoCalculos.NomesMeses, m => t.StartsWith(m.ToLowerInvariant()));
        return i >= 0 ? i + 1 : null;
    }

    private static IXLCell Celula(IXLRow row, int coluna) => row.Cell(coluna);

    private static XLCellValue Valor(IXLCell cell) => cell.HasFormula ? cell.CachedValue : cell.Value;

    private static string? Texto(IXLRow row, int? coluna)
    {
        if (coluna is not int c) return null;
        var v = Valor(Celula(row, c));
        var s = v.IsBlank ? "" : v.IsNumber ? v.GetNumber().ToString(CultureInfo.InvariantCulture) : v.ToString().Trim();
        return string.IsNullOrWhiteSpace(s) ? null : s;
    }

    private static decimal? Numero(IXLRow row, int? coluna)
    {
        if (coluna is not int c) return null;
        var v = Valor(Celula(row, c));
        if (v.IsNumber) return (decimal)v.GetNumber();
        if (v.IsText)
        {
            var s = v.GetText().Trim();
            if (decimal.TryParse(s, NumberStyles.Number, PtPT, out var d)) return d;
            if (decimal.TryParse(s, NumberStyles.Number, CultureInfo.InvariantCulture, out d)) return d;
        }
        return null;
    }

    private static decimal? Arredondar(decimal? valor) => valor is decimal v ? Math.Round(v, 2) : null;
}
