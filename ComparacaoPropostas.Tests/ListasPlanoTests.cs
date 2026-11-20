using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Primitives;
using Xunit;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Services;

namespace ComparacaoPropostas.Tests;

public class ListasPlanoTests
{
    [Fact]
    public void Semente_IdsUnicosESemValoresRepetidosNaMesmaLista()
    {
        var semente = ListasPlano.Semente().ToList();

        Assert.Equal(semente.Count, semente.Select(v => v.Id).Distinct().Count());
        Assert.All(semente.GroupBy(v => v.Lista), g =>
            Assert.Equal(g.Count(), g.Select(v => v.Valor.ToLowerInvariant()).Distinct().Count()));
        Assert.Contains(semente, v => v.Lista == ListaPlano.Ilha && v.Valor == "ST-PR");
        Assert.Contains(semente, v => v.Lista == ListaPlano.Executante && v.Valor == "Chave na mão");
        // Todas as listas mapeadas para campos têm valores iniciais.
        Assert.All(ListasPlano.Campos.Values.Distinct(), l => Assert.Contains(semente, v => v.Lista == l));
    }

    [Fact]
    public void ValoresUsados_SeparaEscolhaMultipla()
    {
        var p = new ProjetoPlano
        {
            Nome = "X",
            Ilha = " SV ",
            ExecucaoProjeto = "DEI",
            FiscalizacaoCivil = "Terceiro",
            NecessidadeAquisicao = "Serv. Civil-abrigo, Serv. Fisc. Civil"
        };

        var usados = ListasPlano.ValoresUsados(p).ToList();

        Assert.Contains((ListaPlano.Ilha, "SV"), usados);
        Assert.Contains((ListaPlano.Executante, "DEI"), usados);
        Assert.Contains((ListaPlano.Executante, "Terceiro"), usados);
        Assert.Contains((ListaPlano.NecessidadeAquisicao, "Serv. Civil-abrigo"), usados);
        Assert.Contains((ListaPlano.NecessidadeAquisicao, "Serv. Fisc. Civil"), usados);
        Assert.Equal(5, usados.Count);
    }

    [Fact]
    public void AplicarFormulario_UsaValorNovoEJuntaEscolhaMultipla()
    {
        var p = new ProjetoPlano { Nome = "X", Ilha = ListasPlano.OpcaoNovo, Tipo = ListasPlano.OpcaoNovo, Zona = "Urbano" };
        var form = new FormCollection(new Dictionary<string, StringValues>
        {
            ["Ilha" + ListasPlano.SufixoNovo] = " Santa Luzia ",
            ["Tipo" + ListasPlano.SufixoNovo] = "",
            ["NecessidadeAquisicao"] = new StringValues(new[] { "Serv. Civil-abrigo", "Equip. Gerador" }),
            ["NecessidadeAquisicao" + ListasPlano.SufixoNovo] = "Serv. Vedação, Equip. Gerador"
        });

        ListasPlano.AplicarFormulario(p, form);

        Assert.Equal("Santa Luzia", p.Ilha);
        Assert.Null(p.Tipo);
        Assert.Equal("Urbano", p.Zona);
        Assert.Equal("Serv. Civil-abrigo, Equip. Gerador, Serv. Vedação", p.NecessidadeAquisicao);
    }

    [Fact]
    public void Renomear_AtualizaCamposDaListaIncluindoMultipla()
    {
        var p = new ProjetoPlano
        {
            Nome = "X",
            ExecucaoProjeto = "Terceiro",
            ExecucaoMontagem = "terceiro",
            FiscalizacaoCivil = "DOM",
            NecessidadeAquisicao = "Serv. Civil-abrigo, Serv. Fisc. Civil"
        };

        Assert.True(ListasPlano.Renomear(p, ListaPlano.Executante, "Terceiro", "Externo"));
        Assert.Equal("Externo", p.ExecucaoProjeto);
        Assert.Equal("Externo", p.ExecucaoMontagem);
        Assert.Equal("DOM", p.FiscalizacaoCivil);

        Assert.True(ListasPlano.Renomear(p, ListaPlano.NecessidadeAquisicao, "Serv. Fisc. Civil", "Serv. Fiscalização Civil"));
        Assert.Equal("Serv. Civil-abrigo, Serv. Fiscalização Civil", p.NecessidadeAquisicao);

        Assert.False(ListasPlano.Renomear(p, ListaPlano.Ilha, "SA", "Santo Antão"));
    }
}
