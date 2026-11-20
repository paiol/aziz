using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Services;

public class LinhaResumoRubrica
{
    public string Rubrica { get; set; } = "";
    public int NumAtivos { get; set; }
    public decimal ValorTransitar { get; set; }
    public decimal ValorNovo { get; set; }
    public decimal TotalOrcamento { get; set; }
    public decimal TotalAnoAnterior { get; set; }
    public decimal Delta => TotalOrcamento - TotalAnoAnterior;
    public int NumCondicionados { get; set; }
}

public class LinhaResumoArea
{
    public AreaDepartamento Area { get; set; }
    public int NumProjetos { get; set; }
    public int NumPropostos { get; set; }
    public int NumAceites { get; set; }
    public int NumRejeitados { get; set; }
    // TOTAL ORC. dos projetos que entram no plano e ainda não foram rejeitados (proposta da área).
    public decimal OrcamentoProposto { get; set; }
    // TOTAL ORC. dos projetos aceites (Capex aprovado da área).
    public decimal OrcamentoAceite { get; set; }
}

public class LinhaResumoCaixa
{
    public CaixaPlano Caixa { get; set; }
    public int NumProjetos { get; set; }
    public decimal TotalOrcamento { get; set; }
    public decimal Terceirizacao { get; set; }
    public decimal EsforcoArquitetura { get; set; }
    public decimal EsforcoEnergia { get; set; }
}

public class CenarioFormacao
{
    public string Nome { get; set; } = "";
    public decimal Custo { get; set; }
    public int Participacoes { get; set; }
}

public class ResumoPlano
{
    public int NumAtivos { get; set; }
    public int NumAtivosSemValor { get; set; }
    public int NumCondicionados { get; set; }
    public decimal ValorTransitar { get; set; }
    public decimal ValorNovo { get; set; }
    public decimal TotalOrcamento { get; set; }
    public decimal TotalAnoAnterior { get; set; }
    public decimal Delta => TotalOrcamento - TotalAnoAnterior;
    public decimal ValorCondicionado { get; set; }
    public decimal TransferidoDom { get; set; }
    public int NumTransferidoDom { get; set; }

    public decimal EsforcoArquitetura { get; set; }
    public decimal EsforcoEnergia { get; set; }
    public decimal? UsoCapacidadeArquitetura { get; set; }
    public decimal? UsoCapacidadeEnergia { get; set; }
    public decimal[] CargaMensalArquitetura { get; set; } = new decimal[12];
    public decimal[] CargaMensalEnergia { get; set; } = new decimal[12];

    public List<LinhaResumoRubrica> PorRubrica { get; set; } = new();
    public List<LinhaResumoCaixa> PorCaixa { get; set; } = new();
    public List<LinhaResumoArea> PorArea { get; set; } = new();
    public int NumPropostos { get; set; }
    public int NumAceites { get; set; }
    public int NumRejeitados { get; set; }
    public decimal OrcamentoAceite { get; set; }
    public List<CenarioFormacao> CenariosFormacao { get; set; } = new();
}

// Execução de um projeto Capex pelas compras associadas (pedidos → processos).
public class ExecucaoProjetoCapex
{
    public decimal Orcamento { get; set; }
    public decimal Adjudicado { get; set; }
    // Orçamento estimado dos pedidos ainda sem adjudicação (compras em curso).
    public decimal EmCurso { get; set; }
    public int NumCompras { get; set; }
    public int NumObras { get; set; }
    public decimal Saldo => Orcamento - Adjudicado;
    public decimal SaldoAposEmCurso => Orcamento - Adjudicado - EmCurso;
    public decimal? PercentagemExecutada => Orcamento > 0 ? Adjudicado / Orcamento : null;
    public bool Ultrapassado => Adjudicado > Orcamento;
}

// Regras de cálculo do plano, replicadas das fórmulas do ficheiro SCOPE_INF
// (folhas Scope, Resumo, Painel e Formação e feiras).
public static class PlanoCalculos
{
    // ---- Execução do Capex pelas compras e obras -------------------------------------------------

    private static bool PedidoAtivo(PedidoProposta p)
        => p.Status != StatusPedido.Cancelado && p.Processo?.Status != StatusProcesso.Cancelado;

    private static decimal? AdjudicadoCve(PedidoProposta p) => PedidoAtivo(p) ? p.Processo?.ValorAdjudicadoCVE : null;

    // Requer ProjetoPlano.Pedidos (com Processo) e ProjetoPlano.Obras carregados.
    // Compras: adjudicado = valor adjudicado (CVE) do processo; em curso = orçamento estimado do pedido.
    // Obras: adjudicado = valor adjudicado ao empreiteiro; em curso = valor estimado da obra.
    public static ExecucaoProjetoCapex CalcularExecucao(ProjetoPlano projeto, int? excluirProcessoId = null, int? excluirObraId = null)
    {
        var pedidos = projeto.Pedidos.Where(PedidoAtivo).Where(p => excluirProcessoId == null || p.Processo?.Id != excluirProcessoId).ToList();
        var obras = projeto.Obras.Where(o => excluirObraId == null || o.Id != excluirObraId).ToList();
        return new ExecucaoProjetoCapex
        {
            Orcamento = projeto.TotalOrcamento,
            Adjudicado = pedidos.Sum(p => AdjudicadoCve(p) ?? 0m) + obras.Sum(o => o.ValorAdjudicado ?? 0m),
            EmCurso = pedidos.Where(p => AdjudicadoCve(p) == null).Sum(p => p.OrcamentoEstimado ?? 0m)
                      + obras.Where(o => o.ValorAdjudicado == null).Sum(o => o.ValorEstimado ?? 0m),
            NumCompras = pedidos.Count,
            NumObras = obras.Count
        };
    }

    // Validação da adjudicação: o novo valor, somado ao já adjudicado nas outras compras e
    // obras do mesmo projeto, não pode passar o TOTAL ORC. sem justificação.
    public static bool AdjudicacaoUltrapassaOrcamento(ProjetoPlano projeto, int processoId, decimal valorCve)
    {
        var execucao = CalcularExecucao(projeto, excluirProcessoId: processoId);
        return execucao.Adjudicado + valorCve > execucao.Orcamento;
    }

    public static bool AdjudicacaoObraUltrapassaOrcamento(ProjetoPlano projeto, int obraId, decimal valorCve)
    {
        var execucao = CalcularExecucao(projeto, excluirObraId: obraId);
        return execucao.Adjudicado + valorCve > execucao.Orcamento;
    }

    // Projeto Capex que é (ou inclui) obra de construção civil — abrigos, edifícios, bases,
    // vedações… Serve para sugerir a ligação ao módulo Obras; qualquer projeto aceite pode ter obras.
    private static readonly string[] PalavrasObra = { "abrigo", "civil", "edif", "obra", "constru", "vedação", "fundação", "base pv", "remodela", "beneficiação" };

    public static bool EhObra(ProjetoPlano p)
    {
        if (p.Obras.Count > 0 || (p.CustoConstrucoes ?? 0m) > 0m) return true;
        var texto = string.Join(" ", p.Nome, p.Classificacao, p.NecessidadeAquisicao, p.DesignacaoRubrica, p.Descricao).ToLowerInvariant();
        return PalavrasObra.Any(texto.Contains);
    }


    // ---- Transição para o plano do ano seguinte ------------------------------------------

    // Sugestão inicial para a revisão do ano seguinte (o utilizador pode mudar cada linha):
    // rejeitados e fora do plano não passam; totalmente adjudicados saem como executados;
    // condicionados continuam condicionados; com adjudicação parcial transitam parcialmente.
    public static SituacaoProjetoPlano? DecisaoTransicaoSugerida(ProjetoPlano anterior)
    {
        if (anterior.EstadoAprovacao == EstadoAprovacaoProjeto.Rejeitado || anterior.Caixa == CaixaPlano.ForaDoPlano) return null;
        var execucao = CalcularExecucao(anterior);
        if (execucao.Orcamento > 0m && execucao.Adjudicado >= execucao.Orcamento) return SituacaoProjetoPlano.ExecutadoRetirar;
        if (anterior.Caixa == CaixaPlano.Condicionado) return SituacaoProjetoPlano.Condicionado;
        return execucao.Adjudicado > 0m ? SituacaoProjetoPlano.TransitadoParcial : SituacaoProjetoPlano.Transitado;
    }

    // Valor que fica por executar: o orçamento do ano menos o que já foi adjudicado.
    public static decimal ValorTransitarSugerido(ProjetoPlano anterior)
        => Math.Max(0m, anterior.TotalOrcamento - CalcularExecucao(anterior).Adjudicado);

    // Cria a linha do plano seguinte a partir do projeto do ano anterior, como na folha Scope:
    // o TOTAL do ano anterior passa a referência, o adjudicado passa a "adjudicado ano anterior"
    // e o valor por executar passa a "valor a transitar".
    public static ProjetoPlano TransitarProjeto(ProjetoPlano anterior, SituacaoProjetoPlano situacao, decimal? valorTransitar)
    {
        var execucao = CalcularExecucao(anterior);
        var caixa = CaixaDoPlano(situacao);
        var entra = caixa != CaixaPlano.ForaDoPlano;
        return new ProjetoPlano
        {
            ProjetoOrigemId = anterior.Id,
            Area = anterior.Area,
            // O que transita mantém-se aceite; condicionado volta a proposta; o que sai fica rejeitado.
            EstadoAprovacao = caixa switch
            {
                CaixaPlano.Entra => EstadoAprovacaoProjeto.Aceite,
                CaixaPlano.ForaDoPlano => EstadoAprovacaoProjeto.Rejeitado,
                _ => EstadoAprovacaoProjeto.Proposto
            },
            DataDecisao = caixa is CaixaPlano.Entra or CaixaPlano.ForaDoPlano ? DateTime.UtcNow : null,
            MotivoDecisao = caixa switch
            {
                CaixaPlano.Entra => "Transitado do plano anterior",
                CaixaPlano.ForaDoPlano => "Não entra no plano (transição do ano anterior)",
                _ => null
            },
            Nome = anterior.Nome,
            CapexAnoMenos2 = anterior.CodCapexAnoAnterior,
            CodCapexAnoAnterior = anterior.CodCapex,
            SeguimentoOrcamento = anterior.SeguimentoOrcamento,
            DesignacaoRubrica = anterior.DesignacaoRubrica,
            OrigemIniciativa = anterior.OrigemIniciativa,
            Tipo = anterior.Tipo,
            Coordenacao = anterior.Coordenacao,
            Ilha = anterior.Ilha,
            Classificacao = anterior.Classificacao,
            Descricao = anterior.Descricao,
            Situacao = situacao,
            Prioridade = anterior.Prioridade,
            ExecucaoAnoAnterior = execucao.PercentagemExecutada is decimal pct ? Math.Min(1m, Math.Round(pct, 4)) : null,
            Zona = anterior.Zona,
            ExecucaoProjeto = anterior.ExecucaoProjeto,
            FiscalizacaoCivil = anterior.FiscalizacaoCivil,
            FiscalizacaoTracados = anterior.FiscalizacaoTracados,
            ExecucaoMontagem = anterior.ExecucaoMontagem,
            PerfilEquipaMontagem = anterior.PerfilEquipaMontagem,
            FiscalizacaoMontagem = anterior.FiscalizacaoMontagem,
            PrestadorExternoIlha = anterior.PrestadorExternoIlha,
            NecessidadeAquisicao = anterior.NecessidadeAquisicao,
            EntidadesExecutantes = anterior.EntidadesExecutantes,
            AdjudicadoAnoAnterior = execucao.Adjudicado,
            AdjudicadoAnosAnteriores = (anterior.AdjudicadoAnoAnterior ?? 0m) + (anterior.AdjudicadoAnosAnteriores ?? 0m),
            TotalOrcamentoAnoAnterior = anterior.TotalOrcamento,
            ValorTransitar = entra ? (valorTransitar ?? ValorTransitarSugerido(anterior)) : 0m,
            ValorNovo = 0m,
            EsforcoArquiteturaDias = anterior.EsforcoArquiteturaDias,
            EsforcoEnergiaDias = anterior.EsforcoEnergiaDias
        };
    }

    // ---- Regras da folha ---------------------------------------------------------------
    public static readonly string[] NomesMeses = { "Jan", "Fev", "Mar", "Abr", "Mai", "Jun", "Jul", "Ago", "Set", "Out", "Nov", "Dez" };

    // Coluna GI: Transitado / Transitado parcial / Novo entram; Condicionado fica à parte;
    // Executado, Cancelado e Exploração - DOM ficam fora do plano.
    public static CaixaPlano CaixaDoPlano(SituacaoProjetoPlano? situacao) => situacao switch
    {
        SituacaoProjetoPlano.Transitado or SituacaoProjetoPlano.TransitadoParcial or SituacaoProjetoPlano.Novo => CaixaPlano.Entra,
        SituacaoProjetoPlano.Condicionado => CaixaPlano.Condicionado,
        null => CaixaPlano.SemSituacao,
        _ => CaixaPlano.ForaDoPlano
    };

    // Coluna AJ: TOTAL ORC. = Valor a transitar + Valor novo, exceto para projetos fora do plano.
    public static decimal TotalOrcamento(ProjetoPlano p)
        => CaixaDoPlano(p.Situacao) == CaixaPlano.ForaDoPlano ? 0m : (p.ValorTransitar ?? 0m) + (p.ValorNovo ?? 0m);

    // Coluna AR: soma dos custos por natureza (a terceirização é "dos quais", não soma).
    public static decimal TotalCustos(ProjetoPlano p)
        => (p.CustoAquisicaoSite ?? 0m) + (p.CustoEquipamento ?? 0m) + (p.CustoConstrucoes ?? 0m)
         + (p.CustoServicos ?? 0m) + (p.CustoDeslocacoes ?? 0m);

    // Folha Formação e feiras, coluna O: Nº × (Inscrição + Passagem + Dias × (Estadia + Ajuda)).
    public static decimal TotalAcaoFormacao(AcaoFormacaoPlano a)
    {
        if (a.NumeroPessoas is not int n) return 0m;
        return n * ((a.InscricaoPorPessoa ?? 0m) + (a.PassagemPorPessoa ?? 0m)
                    + (a.Dias ?? 0m) * ((a.EstadiaDia ?? 0m) + (a.AjudaCustoDia ?? 0m)));
    }

    // Colunas GT/GU: sem Início assume janeiro, sem Fim assume dezembro; Fim nunca antes do Início.
    public static (int Inicio, int Fim) MesesAtivos(ProjetoPlano p)
    {
        var inicio = Math.Clamp(p.MesInicio ?? 1, 1, 12);
        var fim = Math.Max(inicio, Math.Clamp(p.MesFim ?? 12, 1, 12));
        return (inicio, fim);
    }

    // Painel, secção 2: o esforço de cada projeto que entra reparte-se por igual pelos meses ativos.
    public static decimal[] CargaMensal(IEnumerable<ProjetoPlano> projetos, Func<ProjetoPlano, decimal?> esforco)
    {
        var carga = new decimal[12];
        foreach (var p in projetos.Where(p => p.Caixa == CaixaPlano.Entra))
        {
            var dias = esforco(p) ?? 0m;
            if (dias == 0m) continue;
            var (inicio, fim) = MesesAtivos(p);
            var porMes = dias / (fim - inicio + 1);
            for (var m = inicio; m <= fim; m++) carga[m - 1] += porMes;
        }
        return carga;
    }

    public static ResumoPlano CalcularResumo(Plano plano)
    {
        // Os projetos rejeitados ficam fora de todos os totais do plano.
        var todos = plano.Projetos.ToList();
        var projetos = todos.Where(p => p.EstadoAprovacao != EstadoAprovacaoProjeto.Rejeitado).ToList();
        var ativos = projetos.Where(p => p.Caixa == CaixaPlano.Entra).ToList();
        var condicionados = projetos.Where(p => p.Caixa == CaixaPlano.Condicionado).ToList();
        var dom = projetos.Where(p => p.Situacao == SituacaoProjetoPlano.ExploracaoDom).ToList();

        var resumo = new ResumoPlano
        {
            NumAtivos = ativos.Count,
            NumAtivosSemValor = ativos.Count(p => p.TotalOrcamento == 0m),
            NumCondicionados = condicionados.Count,
            ValorTransitar = ativos.Sum(p => p.ValorTransitar ?? 0m),
            ValorNovo = ativos.Sum(p => p.ValorNovo ?? 0m),
            TotalOrcamento = ativos.Sum(p => p.TotalOrcamento),
            TotalAnoAnterior = ativos.Sum(p => p.TotalOrcamentoAnoAnterior ?? 0m),
            ValorCondicionado = ativos.Concat(condicionados).Sum(p => p.ValorCondicionado ?? 0m),
            TransferidoDom = dom.Sum(p => (p.ValorTransitar ?? 0m) + (p.ValorNovo ?? 0m)),
            NumTransferidoDom = dom.Count,
            EsforcoArquitetura = ativos.Sum(p => p.EsforcoArquiteturaDias ?? 0m),
            EsforcoEnergia = ativos.Sum(p => p.EsforcoEnergiaDias ?? 0m),
            CargaMensalArquitetura = CargaMensal(projetos, p => p.EsforcoArquiteturaDias),
            CargaMensalEnergia = CargaMensal(projetos, p => p.EsforcoEnergiaDias)
        };

        resumo.UsoCapacidadeArquitetura = Uso(resumo.EsforcoArquitetura, plano.CapacidadeArquiteturaDias);
        resumo.UsoCapacidadeEnergia = Uso(resumo.EsforcoEnergia, plano.CapacidadeEnergiaDias);

        resumo.PorRubrica = projetos
            .Where(p => p.Caixa is CaixaPlano.Entra or CaixaPlano.Condicionado)
            .GroupBy(p => string.IsNullOrWhiteSpace(p.DesignacaoRubrica) ? "(sem rubrica)" : p.DesignacaoRubrica.Trim())
            .Select(g =>
            {
                var at = g.Where(p => p.Caixa == CaixaPlano.Entra).ToList();
                return new LinhaResumoRubrica
                {
                    Rubrica = g.Key,
                    NumAtivos = at.Count,
                    ValorTransitar = at.Sum(p => p.ValorTransitar ?? 0m),
                    ValorNovo = at.Sum(p => p.ValorNovo ?? 0m),
                    TotalOrcamento = at.Sum(p => p.TotalOrcamento),
                    TotalAnoAnterior = at.Sum(p => p.TotalOrcamentoAnoAnterior ?? 0m),
                    NumCondicionados = g.Count(p => p.Caixa == CaixaPlano.Condicionado)
                };
            })
            .OrderByDescending(r => r.TotalOrcamento)
            .ThenBy(r => r.Rubrica)
            .ToList();

        resumo.PorCaixa = Enum.GetValues<CaixaPlano>()
            .Select(c =>
            {
                var g = projetos.Where(p => p.Caixa == c).ToList();
                return new LinhaResumoCaixa
                {
                    Caixa = c,
                    NumProjetos = g.Count,
                    TotalOrcamento = g.Sum(p => p.TotalOrcamento),
                    Terceirizacao = g.Sum(p => p.CustoTerceirizacao ?? 0m),
                    EsforcoArquitetura = g.Sum(p => p.EsforcoArquiteturaDias ?? 0m),
                    EsforcoEnergia = g.Sum(p => p.EsforcoEnergiaDias ?? 0m)
                };
            })
            .ToList();

        bool Aceite(ProjetoPlano p) => p.EstadoAprovacao == EstadoAprovacaoProjeto.Aceite && p.Caixa != CaixaPlano.ForaDoPlano;
        resumo.NumPropostos = todos.Count(p => p.EstadoAprovacao == EstadoAprovacaoProjeto.Proposto);
        resumo.NumAceites = todos.Count(Aceite);
        resumo.NumRejeitados = todos.Count(p => p.EstadoAprovacao == EstadoAprovacaoProjeto.Rejeitado);
        resumo.OrcamentoAceite = todos.Where(Aceite).Sum(p => p.TotalOrcamento);
        resumo.PorArea = todos
            .GroupBy(p => p.Area)
            .OrderBy(g => g.Key)
            .Select(g => new LinhaResumoArea
            {
                Area = g.Key,
                NumProjetos = g.Count(),
                NumPropostos = g.Count(p => p.EstadoAprovacao == EstadoAprovacaoProjeto.Proposto),
                NumAceites = g.Count(Aceite),
                NumRejeitados = g.Count(p => p.EstadoAprovacao == EstadoAprovacaoProjeto.Rejeitado),
                OrcamentoProposto = g.Where(p => p.Caixa == CaixaPlano.Entra && p.EstadoAprovacao != EstadoAprovacaoProjeto.Rejeitado).Sum(p => p.TotalOrcamento),
                OrcamentoAceite = g.Where(Aceite).Sum(p => p.TotalOrcamento)
            })
            .ToList();

        resumo.CenariosFormacao = CalcularCenariosFormacao(plano.AcoesFormacao, plano.Ano);
        return resumo;
    }

    // Cenários da folha Formação e feiras — só contam as ações do ano do plano (sem ano = ano do plano).
    public static List<CenarioFormacao> CalcularCenariosFormacao(IEnumerable<AcaoFormacaoPlano> acoes, int ano)
    {
        var doAno = acoes.Where(a => (a.Ano ?? ano) == ano).ToList();
        CenarioFormacao Cenario(string nome, Func<AcaoFormacaoPlano, bool> filtro)
        {
            var sel = doAno.Where(filtro).ToList();
            return new CenarioFormacao { Nome = nome, Custo = sel.Sum(a => a.Total), Participacoes = sel.Sum(a => a.NumeroPessoas ?? 0) };
        }

        return new List<CenarioFormacao>
        {
            Cenario("Cenário mínimo (prioridade 1)", a => a.Prioridade == 1),
            Cenario("Cenário alargado (prioridades 1 + 2)", a => a.Prioridade <= 2),
            Cenario("Todas as opções (1 + 2 + 3)", _ => true),
            Cenario("Selecionado para o plano (No plano? = Sim)", a => a.NoPlano)
        };
    }

    private static decimal? Uso(decimal esforco, decimal? capacidade)
        => capacidade is decimal c && c > 0 ? esforco / c : null;
}
