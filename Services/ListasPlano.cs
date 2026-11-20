using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;
using Microsoft.EntityFrameworkCore;

namespace ComparacaoPropostas.Services;

// Listas de escolha dos projetos do plano: valores iniciais (folha "Listas" do ficheiro SCOPE),
// que campo do projeto usa que lista, e como acrescentar valores novos.
public static class ListasPlano
{
    // Campo do ProjetoPlano → lista. Necessidade de aquisição é de escolha múltipla (valores separados por vírgula).
    public static readonly IReadOnlyDictionary<string, ListaPlano> Campos = new Dictionary<string, ListaPlano>
    {
        [nameof(ProjetoPlano.DesignacaoRubrica)] = ListaPlano.Rubrica,
        [nameof(ProjetoPlano.Tipo)] = ListaPlano.Tipo,
        [nameof(ProjetoPlano.Coordenacao)] = ListaPlano.Coordenacao,
        [nameof(ProjetoPlano.Ilha)] = ListaPlano.Ilha,
        [nameof(ProjetoPlano.Classificacao)] = ListaPlano.Classificacao,
        [nameof(ProjetoPlano.Capex)] = ListaPlano.Capex,
        [nameof(ProjetoPlano.OrigemIniciativa)] = ListaPlano.OrigemIniciativa,
        [nameof(ProjetoPlano.TrimestrePrevisto)] = ListaPlano.Trimestre,
        [nameof(ProjetoPlano.Zona)] = ListaPlano.Zona,
        [nameof(ProjetoPlano.ExecucaoProjeto)] = ListaPlano.Executante,
        [nameof(ProjetoPlano.FiscalizacaoCivil)] = ListaPlano.Executante,
        [nameof(ProjetoPlano.FiscalizacaoTracados)] = ListaPlano.Executante,
        [nameof(ProjetoPlano.ExecucaoMontagem)] = ListaPlano.Executante,
        [nameof(ProjetoPlano.FiscalizacaoMontagem)] = ListaPlano.Executante,
        [nameof(ProjetoPlano.PerfilEquipaMontagem)] = ListaPlano.PerfilMontagem,
        [nameof(ProjetoPlano.PrestadorExternoIlha)] = ListaPlano.SimNao,
        [nameof(ProjetoPlano.NecessidadeAquisicao)] = ListaPlano.NecessidadeAquisicao
    };

    public static bool Multipla(ListaPlano lista) => lista == ListaPlano.NecessidadeAquisicao;

    // Sufixo do campo de texto "Adicionar novo…" no formulário (ex.: Ilha__novo).
    public const string SufixoNovo = "__novo";

    // Valor da opção "+ Adicionar novo…" nas listas de escolha.
    public const string OpcaoNovo = "__novo__";

    public static readonly IReadOnlyDictionary<ListaPlano, string[]> ValoresIniciais = new Dictionary<ListaPlano, string[]>
    {
        [ListaPlano.Rubrica] = new[]
        {
            "CVT IMS & VoLTE new build (+SmartCare & PCRF) 5G", "Smartcare Roaming Care - POC", "Neutral Carrier Datacenter",
            "Projetos Data Center - Climatização DC2 e Corredor Acesso DC1", "Proj. Retificadores - subsituição em 6 sites e reforço 2 sites",
            "Proj. Segurança Energia - Bacia Retenção e Ext. Auto Fogo Sala A", "Proj. Baterias litio - Substitição em 3 sites",
            "Projetos Geradores 2026 - Novos 8 sites incluindo GlobalCVT", "Projeto Power Praia", "Projetos Geradores - Transitado 5 projetos",
            "Projeto Power Mindelo", "Hybrid Power - Novo 5 sites com recursos existentes", "Hybrid Power - Transitado 3 sites",
            "Licenças NetECO Power Room A e monitorização retificadores Eltek", "Hybrid Power - Novo 2 sites SL e BV", "BTS Móvel Eventos",
            "Ferramentas Fiscalização", "Proj. Edificio - Calheta S.Miguel e A.S.Filipe", "FTTH principais Cidades - novos  2026",
            "Projetos RAN - em curso & Eventos", "Projetos RAN - novos"
        },
        [ListaPlano.Tipo] = new[] { "ENR", "EDF", "RAN", "PRC" },
        [ListaPlano.Coordenacao] = new[] { "CRN", "CRS", "SSM", "CLI", "CRF", "NAC" },
        [ListaPlano.Ilha] = new[] { "SA", "SV", "SN", "SL", "BV", "MO", "ST-PR", "ST-INT", "FG", "BR", "NAC" },
        [ListaPlano.Classificacao] = new[]
        {
            "Proj. Novo Site", "Proj. Remotização", "Proj. Reengenharia", "Proj. Remanejamento", "Proj. Evento", "Proj. Upgrade", "Add Tech",
            "Eng./design", "Proj. Gerador", "Proj. Hybrid Power", "Proj. Baterias", "Proj. Modernização", "Proj. Retificador",
            "Proj. Beneficiação", "Proj. Edifício", "Proj. SAF", "Proj. Segurança", "Aquisição", "Processos", "Outros"
        },
        [ListaPlano.Capex] = new[] { "CAPEX 26", "COND_CAPEX 26", "CAPEX 27 _Transitado", "CAPEX 27 _Novo", "COND_CAPEX 27", "CAPEX 28" },
        [ListaPlano.OrigemIniciativa] = new[] { "CRN", "DOM", "DEI", "DLG" },
        [ListaPlano.Trimestre] = new[] { "T1", "T2", "T3", "T4", "Ano todo" },
        [ListaPlano.Zona] = new[] { "Urbano", "Rural" },
        [ListaPlano.Executante] = new[] { "DEI", "DOM", "Terceiro", "NA", "Chave na mão" },
        [ListaPlano.PerfilMontagem] = new[] { "Energia", "Equipa", "ST", "SV" },
        [ListaPlano.SimNao] = new[] { "Sim", "Não", "NA" },
        [ListaPlano.NecessidadeAquisicao] = new[]
        {
            "Serv. Civil-abrigo", "Serv. Civil-traçado", "Serv. Desmont.-gerador", "Serv. Desmont.-PV", "Serv. Mont.-gerador",
            "Serv. Mont.-PV", "Equip. Gerador", "Serv. Conf. Escape", "Serv. Fisc. Civil", "Serv. Fisc. Mont.", "NA"
        }
    };

    // Valores iniciais com Ids fixos, para o HasData (migração).
    public static IEnumerable<ValorLista> Semente()
    {
        var id = 1;
        foreach (var (lista, valores) in ValoresIniciais)
            for (var i = 0; i < valores.Length; i++)
                yield return new ValorLista { Id = id++, Lista = lista, Valor = valores[i], Ordem = (i + 1) * 10, Ativo = true };
    }

    // Separa os valores de um campo de escolha múltipla ("Serv. Civil-abrigo, Serv. Fisc. Civil").
    public static IEnumerable<string> Separar(string? texto)
        => (texto ?? "").Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

    // Valores usados num projeto, por lista (para garantir que existem nas listas).
    public static IEnumerable<(ListaPlano Lista, string Valor)> ValoresUsados(ProjetoPlano projeto)
    {
        foreach (var (campo, lista) in Campos)
        {
            var texto = typeof(ProjetoPlano).GetProperty(campo)!.GetValue(projeto) as string;
            var valores = Multipla(lista) ? Separar(texto) : string.IsNullOrWhiteSpace(texto) ? Enumerable.Empty<string>() : new[] { texto.Trim() };
            foreach (var v in valores) yield return (lista, v);
        }
    }

    // Acrescenta às listas os valores que os projetos usam e ainda não existem (ex.: depois de importar).
    // Devolve quantos valores foram acrescentados. Não grava: o chamador faz SaveChanges.
    public static int GarantirValores(AppDbContext db, IEnumerable<ProjetoPlano> projetos)
    {
        var existentes = db.ValoresLista.AsNoTracking().ToList()
            .Concat(db.ValoresLista.Local)
            .Select(v => (v.Lista, Chave: v.Valor.Trim().ToLowerInvariant()))
            .ToHashSet();
        var ordemMax = db.ValoresLista.AsNoTracking().GroupBy(v => v.Lista).Select(g => new { g.Key, Max = g.Max(v => v.Ordem) })
            .ToDictionary(x => x.Key, x => x.Max);

        var novos = 0;
        foreach (var (lista, valor) in projetos.SelectMany(ValoresUsados))
        {
            if (valor.Length > 200 || !existentes.Add((lista, valor.ToLowerInvariant()))) continue;
            var ordem = ordemMax.GetValueOrDefault(lista) + 10;
            ordemMax[lista] = ordem;
            db.ValoresLista.Add(new ValorLista { Lista = lista, Valor = valor, Ordem = ordem, Ativo = true });
            novos++;
        }
        return novos;
    }

    // Aplica ao projeto os valores escritos em "Adicionar novo…" e, nos campos múltiplos, junta as opções escolhidas.
    public static void AplicarFormulario(ProjetoPlano projeto, Microsoft.AspNetCore.Http.IFormCollection form)
    {
        foreach (var (campo, lista) in Campos)
        {
            var prop = typeof(ProjetoPlano).GetProperty(campo)!;
            var novo = form[campo + SufixoNovo].ToString().Trim();
            if (Multipla(lista))
            {
                var escolhidos = form[campo].Where(v => !string.IsNullOrWhiteSpace(v)).Select(v => v!.Trim()).ToList();
                escolhidos.AddRange(Separar(novo));
                var texto = string.Join(", ", escolhidos.Distinct(StringComparer.OrdinalIgnoreCase));
                prop.SetValue(projeto, texto.Length == 0 ? null : texto);
            }
            else if (novo.Length > 0)
            {
                prop.SetValue(projeto, novo);
            }
            else if (prop.GetValue(projeto) as string == OpcaoNovo)
            {
                // Escolheu "+ Adicionar novo…" mas não escreveu nada.
                prop.SetValue(projeto, null);
            }
        }
    }

    // Valores ativos de cada lista, para os formulários.
    public static Dictionary<ListaPlano, List<string>> CarregarAtivas(AppDbContext db)
        => db.ValoresLista.AsNoTracking()
            .Where(v => v.Ativo)
            .OrderBy(v => v.Ordem).ThenBy(v => v.Valor)
            .ToList()
            .GroupBy(v => v.Lista)
            .ToDictionary(g => g.Key, g => g.Select(v => v.Valor).ToList());

    // Renomeia um valor de lista nos campos do projeto que usam essa lista. Devolve true se mudou.
    public static bool Renomear(ProjetoPlano projeto, ListaPlano lista, string antigo, string novo)
    {
        var mudou = false;
        foreach (var (campo, l) in ListasPlano.Campos.Where(c => c.Value == lista))
        {
            var prop = typeof(ProjetoPlano).GetProperty(campo)!;
            var texto = prop.GetValue(projeto) as string;
            if (string.IsNullOrWhiteSpace(texto)) continue;

            if (ListasPlano.Multipla(l))
            {
                var partes = ListasPlano.Separar(texto).ToList();
                if (!partes.Any(x => x.Equals(antigo, StringComparison.OrdinalIgnoreCase))) continue;
                prop.SetValue(projeto, string.Join(", ", partes.Select(x => x.Equals(antigo, StringComparison.OrdinalIgnoreCase) ? novo : x)));
                mudou = true;
            }
            else if (texto.Trim().Equals(antigo, StringComparison.OrdinalIgnoreCase))
            {
                prop.SetValue(projeto, novo);
                mudou = true;
            }
        }
        return mudou;
    }
}
