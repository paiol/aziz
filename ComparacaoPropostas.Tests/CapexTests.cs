using System.Linq;
using Xunit;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Services;

namespace ComparacaoPropostas.Tests;

public class CapexTests
{
    private static PedidoProposta Compra(int processoId, decimal? adjudicadoCve, decimal? estimado = null,
        StatusProcesso status = StatusProcesso.Concluido, StatusPedido statusPedido = StatusPedido.Finalizado)
        => new()
        {
            Status = statusPedido,
            OrcamentoEstimado = estimado,
            Processo = new Processo { Id = processoId, Status = status, ValorAdjudicadoCVE = adjudicadoCve }
        };

    private static ProjetoPlano ProjetoAceite(decimal transitar, decimal novo) => new()
    {
        Id = 10,
        Nome = "Gerador Paul",
        Area = AreaDepartamento.DepInf,
        Situacao = SituacaoProjetoPlano.Novo,
        EstadoAprovacao = EstadoAprovacaoProjeto.Aceite,
        ValorTransitar = transitar,
        ValorNovo = novo
    };

    [Fact]
    public void CalcularExecucao_SomaAdjudicadoEEmCurso_IgnoraCancelados()
    {
        var p = ProjetoAceite(0m, 1000m);
        p.Pedidos.Add(Compra(1, 300m));
        p.Pedidos.Add(Compra(2, null, estimado: 200m, status: StatusProcesso.Recebido, statusPedido: StatusPedido.EmCurso));
        p.Pedidos.Add(new PedidoProposta { Status = StatusPedido.EmCurso, OrcamentoEstimado = 50m }); // ainda sem processo
        p.Pedidos.Add(Compra(3, 999m, status: StatusProcesso.Cancelado));

        var e = PlanoCalculos.CalcularExecucao(p);

        Assert.Equal(1000m, e.Orcamento);
        Assert.Equal(300m, e.Adjudicado);
        Assert.Equal(250m, e.EmCurso);
        Assert.Equal(700m, e.Saldo);
        Assert.Equal(450m, e.SaldoAposEmCurso);
        Assert.Equal(0.3m, e.PercentagemExecutada);
        Assert.Equal(3, e.NumCompras);
        Assert.False(e.Ultrapassado);
    }

    [Fact]
    public void AdjudicacaoUltrapassaOrcamento_ContaOutrasComprasMasNaoAPropria()
    {
        var p = ProjetoAceite(0m, 1000m);
        p.Pedidos.Add(Compra(1, 600m));
        p.Pedidos.Add(Compra(2, 350m)); // processo que está a ser (re)adjudicado

        Assert.False(PlanoCalculos.AdjudicacaoUltrapassaOrcamento(p, processoId: 2, valorCve: 400m));
        Assert.True(PlanoCalculos.AdjudicacaoUltrapassaOrcamento(p, processoId: 2, valorCve: 401m));
    }

    [Fact]
    public void AdjudicacaoUltrapassaOrcamento_ProjetoForaDoPlanoTemOrcamentoZero()
    {
        var p = ProjetoAceite(0m, 1000m);
        p.Situacao = SituacaoProjetoPlano.Cancelado;
        Assert.True(PlanoCalculos.AdjudicacaoUltrapassaOrcamento(p, processoId: 1, valorCve: 1m));
    }

    [Fact]
    public void TransitarProjeto_CopiaDadosEPassaValoresParaAnoAnterior()
    {
        var anterior = ProjetoAceite(200m, 800m);
        anterior.Area = AreaDepartamento.DepPrm;
        anterior.CodCapex = "CVT27_01";
        anterior.CodCapexAnoAnterior = "CVT26_01";
        anterior.AdjudicadoAnoAnterior = 50m;
        anterior.AdjudicadoAnosAnteriores = 25m;
        anterior.Pedidos.Add(Compra(1, 400m));

        Assert.Equal(600m, PlanoCalculos.ValorTransitarSugerido(anterior));
        Assert.Equal(SituacaoProjetoPlano.TransitadoParcial, PlanoCalculos.DecisaoTransicaoSugerida(anterior));

        var novo = PlanoCalculos.TransitarProjeto(anterior, SituacaoProjetoPlano.TransitadoParcial, valorTransitar: null);

        Assert.Equal(10, novo.ProjetoOrigemId);
        Assert.Equal(AreaDepartamento.DepPrm, novo.Area);
        Assert.Equal(EstadoAprovacaoProjeto.Aceite, novo.EstadoAprovacao);
        Assert.Equal("CVT27_01", novo.CodCapexAnoAnterior);
        Assert.Equal("CVT26_01", novo.CapexAnoMenos2);
        Assert.Null(novo.CodCapex);
        Assert.Equal(1000m, novo.TotalOrcamentoAnoAnterior);
        Assert.Equal(400m, novo.AdjudicadoAnoAnterior);
        Assert.Equal(75m, novo.AdjudicadoAnosAnteriores);
        Assert.Equal(0.4m, novo.ExecucaoAnoAnterior);
        Assert.Equal(600m, novo.ValorTransitar);
        Assert.Equal(0m, novo.ValorNovo);
        Assert.Equal(600m, novo.TotalOrcamento);
        Assert.Empty(novo.Pedidos);
    }

    [Theory]
    [InlineData(SituacaoProjetoPlano.Transitado, EstadoAprovacaoProjeto.Aceite)]
    [InlineData(SituacaoProjetoPlano.Condicionado, EstadoAprovacaoProjeto.Proposto)]
    [InlineData(SituacaoProjetoPlano.ExecutadoRetirar, EstadoAprovacaoProjeto.Rejeitado)]
    public void TransitarProjeto_DefineAprovacaoPelaDecisao(SituacaoProjetoPlano decisao, EstadoAprovacaoProjeto esperado)
    {
        var novo = PlanoCalculos.TransitarProjeto(ProjetoAceite(0m, 100m), decisao, valorTransitar: 30m);
        Assert.Equal(esperado, novo.EstadoAprovacao);
        Assert.Equal(decisao == SituacaoProjetoPlano.ExecutadoRetirar ? 0m : 30m, novo.ValorTransitar);
    }

    [Fact]
    public void DecisaoTransicaoSugerida_RegrasBase()
    {
        var rejeitado = ProjetoAceite(0m, 100m);
        rejeitado.EstadoAprovacao = EstadoAprovacaoProjeto.Rejeitado;
        Assert.Null(PlanoCalculos.DecisaoTransicaoSugerida(rejeitado));

        var executado = ProjetoAceite(0m, 100m);
        executado.Pedidos.Add(Compra(1, 100m));
        Assert.Equal(SituacaoProjetoPlano.ExecutadoRetirar, PlanoCalculos.DecisaoTransicaoSugerida(executado));

        var condicionado = ProjetoAceite(0m, 100m);
        condicionado.Situacao = SituacaoProjetoPlano.Condicionado;
        Assert.Equal(SituacaoProjetoPlano.Condicionado, PlanoCalculos.DecisaoTransicaoSugerida(condicionado));

        Assert.Equal(SituacaoProjetoPlano.Transitado, PlanoCalculos.DecisaoTransicaoSugerida(ProjetoAceite(0m, 100m)));
    }

    [Fact]
    public void CalcularResumo_PorAreaEAprovacao_RejeitadosNaoContam()
    {
        var plano = new Plano
        {
            Ano = 2027,
            Projetos =
            {
                new ProjetoPlano { Area = AreaDepartamento.DepInf, Situacao = SituacaoProjetoPlano.Novo, ValorNovo = 100m, EstadoAprovacao = EstadoAprovacaoProjeto.Aceite },
                new ProjetoPlano { Area = AreaDepartamento.DepInf, Situacao = SituacaoProjetoPlano.Novo, ValorNovo = 50m },
                new ProjetoPlano { Area = AreaDepartamento.DepPrm, Situacao = SituacaoProjetoPlano.Novo, ValorNovo = 70m, EstadoAprovacao = EstadoAprovacaoProjeto.Rejeitado }
            }
        };

        var r = PlanoCalculos.CalcularResumo(plano);

        Assert.Equal(150m, r.TotalOrcamento);
        Assert.Equal(2, r.NumAtivos);
        Assert.Equal(1, r.NumAceites);
        Assert.Equal(1, r.NumPropostos);
        Assert.Equal(1, r.NumRejeitados);
        Assert.Equal(100m, r.OrcamentoAceite);

        var inf = r.PorArea.Single(a => a.Area == AreaDepartamento.DepInf);
        Assert.Equal(150m, inf.OrcamentoProposto);
        Assert.Equal(100m, inf.OrcamentoAceite);
        var prm = r.PorArea.Single(a => a.Area == AreaDepartamento.DepPrm);
        Assert.Equal(0m, prm.OrcamentoProposto);
        Assert.Equal(1, prm.NumRejeitados);
    }
}
