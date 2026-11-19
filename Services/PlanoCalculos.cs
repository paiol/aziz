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
    public List<CenarioFormacao> CenariosFormacao { get; set; } = new();
}

// Regras de cálculo do plano, replicadas das fórmulas do ficheiro SCOPE_INF
// (folhas Scope, Resumo, Painel e Formação e feiras).
public static class PlanoCalculos
{
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
        var projetos = plano.Projetos.ToList();
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
