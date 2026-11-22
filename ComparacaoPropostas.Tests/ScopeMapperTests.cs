using Xunit;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Services.Airtable.Scope;

namespace ComparacaoPropostas.Tests;

public class ScopeMapperTests
{
    private static ProjetoPlano Projeto() => new()
    {
        Id = 42,
        Nome = "Substituição Gerador Paul",
        Area = AreaDepartamento.DepInf,
        EstadoAprovacao = EstadoAprovacaoProjeto.Aceite,
        Situacao = SituacaoProjetoPlano.TransitadoParcial,
        Prioridade = PrioridadeProjetoPlano.Media,
        Coordenacao = "CRN",
        Ilha = "MO",
        OrigemIniciativa = "DOM",
        DesignacaoRubrica = "Projetos Geradores",
        Descricao = "Instalação de gerador 22kVA",
        ValorTransitar = 184944.4m,
        ValorNovo = 89760m
    };

    [Fact]
    public void Campos_UsamOsNomesDaTabelaScope()
    {
        var c = ScopeMapper.Campos(Projeto(), 2027);

        Assert.Equal("PP42", ScopeMapper.Chave(Projeto()));
        Assert.Equal("Substituição Gerador Paul", c["🟠Nome Projeto Original"]);
        Assert.Equal("Transitado", c["🟠Enquadramento CAPEX"]);
        Assert.Equal(new[] { "DEP-INF" }, c["Área DEP"]);
        Assert.Equal("CRN", c["🟠Coord"]);
        Assert.Equal("MA", c["🟠Ilha"]);
        Assert.Equal("DOM", c["Dono Proj."]);
        Assert.Equal("P2", c["🟠Orientação Prioridade (DEP)"]);
        Assert.Equal("Instalação de gerador 22kVA", c["🟠Descrição âmbito"]);
        Assert.Equal("Plataforma DEI · Plano 2027 · Transitado parcial · Rubrica: Projetos Geradores", c["🟠Histórico Proj."]);
        Assert.Equal(184944m, c["Orçamento transitado CVE"]);
        Assert.Equal(274704m, c["🟠Orçamento Projeto CVE Plano 2027"]);
    }

    [Fact]
    public void Campos_ValoresForaDasOpcoesDoAirtableFicamVazios()
    {
        var p = Projeto();
        p.OrigemIniciativa = "CRN";      // não é opção de "Dono Proj."
        p.Coordenacao = "XYZ";
        p.Ilha = "Santa Luzia";
        p.Prioridade = null;
        p.Situacao = SituacaoProjetoPlano.Novo;

        var c = ScopeMapper.Campos(p, 2028);

        Assert.Null(c["Dono Proj."]);
        Assert.Null(c["🟠Coord"]);
        Assert.Null(c["🟠Ilha"]);
        Assert.Equal("tbd", c["🟠Orientação Prioridade (DEP)"]);
        Assert.Equal("Novo 2028", c["🟠Enquadramento CAPEX"]);
        Assert.True(c.ContainsKey("🟠Orçamento Projeto CVE Plano 2028"));
    }

    [Theory]
    [InlineData(EstadoAprovacaoProjeto.Aceite, SituacaoProjetoPlano.Novo, true)]
    [InlineData(EstadoAprovacaoProjeto.Aceite, SituacaoProjetoPlano.Cancelado, false)]
    [InlineData(EstadoAprovacaoProjeto.Proposto, SituacaoProjetoPlano.Novo, false)]
    [InlineData(EstadoAprovacaoProjeto.Rejeitado, SituacaoProjetoPlano.Transitado, false)]
    public void DeveEstarNoScope_SoAceitesNoPlano(EstadoAprovacaoProjeto estado, SituacaoProjetoPlano situacao, bool esperado)
    {
        var p = new ProjetoPlano { Nome = "X", EstadoAprovacao = estado, Situacao = situacao };
        Assert.Equal(esperado, ScopeMapper.DeveEstarNoScope(p));
    }
}
