using System;
using System.Collections.Generic;
using Xunit;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Services.Airtable;
using S = ComparacaoPropostas.Services.Airtable.AirtableSchema;

namespace ComparacaoPropostas.Tests;

public class AirtableMapperTests
{
    private static Processo NovoProcesso() => new()
    {
        Id = 9,
        Nome = "Aquisição Gerador",
        NumeroProcesso = "PRC-009",
        Status = StatusProcesso.Concluido,
        TipoCompra = TipoCompra.Internacional,
        ValorAdjudicadoCVE = 2977155m,
        DataAdjudicacao = new DateTime(2026, 8, 18, 17, 48, 0, DateTimeKind.Utc),
        PedidoProposta = new PedidoProposta
        {
            TipoProposta = "Compra Material Geradores",
            Area = AreaDepartamento.DepInf,
            PessoaCriou = "Abdel",
            PrazoEntrega = new DateTime(2026, 8, 31)
        },
        PropostaVencedora = new Proposta { Fornecedor = "Delta" }
    };

    [Fact]
    public void Processo_MapeiaCamposEEstado()
    {
        var r = AirtableMapper.Processo(NovoProcesso(), "https://exemplo.cv/");

        Assert.Equal("PR9", r.Chave);
        Assert.Equal("Aquisição Gerador", r.Campos[S.Processos.Nome]);
        Assert.Equal("Decidido", r.Campos[S.Processos.Status]);
        Assert.Equal("2026-08-31", r.Campos[S.Processos.PrazoFinal]);
        Assert.Equal("DEP-INF", r.Campos[S.Processos.Area]);
        Assert.Equal("Internacional", r.Campos[S.Processos.TipoCompra]);
        Assert.Equal("Delta", r.Campos[S.Processos.FornecedorVencedor]);
        Assert.Equal(2977155m, r.Campos[S.Processos.ValorAdjudicadoCve]);
        Assert.Equal("2026-08-18", r.Campos[S.Processos.DataAdjudicacao]);
        Assert.Equal("https://exemplo.cv/Processos/Details/9", r.Campos[S.Processos.LinkApp]);
        Assert.Null(r.Campos[S.Processos.Descricao]); // limpa o campo no Airtable
    }

    [Theory]
    [InlineData(StatusProcesso.Criado, "Aberto")]
    [InlineData(StatusProcesso.Recebido, "Em Avaliação")]
    [InlineData(StatusProcesso.Concluido, "Decidido")]
    [InlineData(StatusProcesso.Cancelado, "Cancelado")]
    public void StatusProcesso_CorrespondeAsOpcoesDoAirtable(StatusProcesso status, string esperado)
        => Assert.Equal(esperado, AirtableMapper.StatusProcesso(status));

    [Theory]
    [InlineData(StatusProposta.Recebida, "Recebida")]
    [InlineData(StatusProposta.EmAnalise, "Em Avaliação")]
    [InlineData(StatusProposta.Aceite, "Aprovada")]
    [InlineData(StatusProposta.Rejeitada, "Reprovada")]
    public void StatusProposta_CorrespondeAsOpcoesDoAirtable(StatusProposta status, string esperado)
        => Assert.Equal(esperado, AirtableMapper.StatusProposta(status));

    [Theory]
    [InlineData("Tecnico", "Técnico")]
    [InlineData("Técnica", "Técnico")]
    [InlineData("financeira", "Financeiro")]
    [InlineData("Preço", "Financeiro")]
    [InlineData("Comercial", "Comercial")]
    [InlineData("Ambiental", "Ambiental")]
    [InlineData("  ", null)]
    public void CategoriaCriterio_Normaliza(string entrada, string? esperado)
        => Assert.Equal(esperado, AirtableMapper.CategoriaCriterio(entrada));

    [Fact]
    public void Criterio_PesoPassaDePercentagemParaFracao()
    {
        var r = AirtableMapper.Criterio(new Criterio { Id = 3, Nome = "Técnico", Categoria = "Tecnico", Peso = 90m });

        Assert.Equal("C3", r.Chave);
        Assert.Equal(0.9m, r.Campos[S.Criterios.Peso]);
        Assert.Equal("Técnico", r.Campos[S.Criterios.Categoria]);
    }

    [Fact]
    public void Proposta_EmEuros_ValorTotalVaiEmCve()
    {
        var pr = new Proposta
        {
            Id = 18, ProcessoId = 9, Fornecedor = "Delta", Moeda = "EUR", TaxaCambio = 110.265m,
            ValorTotal = 27000m, Status = StatusProposta.Aceite
        };

        var r = AirtableMapper.Proposta(pr, "recPROCESSO00001", null);

        Assert.Equal("P18", r.Chave);
        Assert.Equal(2977155.00m, r.Campos[S.Propostas.ValorTotal]);
        Assert.Equal(27000m, r.Campos[S.Propostas.ValorMoedaOriginal]);
        Assert.Equal("EUR", r.Campos[S.Propostas.Moeda]);
        Assert.Equal("Aprovada", r.Campos[S.Propostas.Status]);
        Assert.Equal(new[] { "recPROCESSO00001" }, r.Campos[S.Propostas.Processo]);
        Assert.Null(r.Campos[S.Propostas.LinkApp]);
    }

    [Fact]
    public void ItemProposta_PrecoConvertidoELigacoes()
    {
        var pr = new Proposta { Id = 18, Fornecedor = "Delta", Moeda = "EUR", TaxaCambio = 110.265m };
        var ip = new ItemProposta
        {
            Id = 55, PropostaId = 18, ItemMaterialId = 7, Quantidade = 2m, PrecoUnitario = 100m,
            ItemMaterial = new ItemMaterial { Id = 7, NomeItem = "Gerador 100 kVA" }
        };

        var r = AirtableMapper.ItemProposta(ip, pr, "recPROPOSTA00001", null);

        Assert.Equal("IP55", r.Chave);
        Assert.Equal("Delta - Gerador 100 kVA", r.Campos[S.ItensProposta.Nome]);
        Assert.Equal(11026.50m, r.Campos[S.ItensProposta.PrecoUnitario]);
        Assert.Equal(Array.Empty<string>(), r.Campos[S.ItensProposta.Item]);
    }

    [Fact]
    public void NotaFinal_Manual_MediaDosAvaliadores()
    {
        var c = new Criterio { Id = 1, Nome = "Técnico", Peso = 90m };
        var pr = new Proposta
        {
            Id = 18,
            Avaliacoes = new List<Avaliacao>
            {
                new() { CriterioId = 1, Nota = 5, Avaliador = new Avaliador { Nome = "Ana" }, Comentario = "Cumpre tudo" },
                new() { CriterioId = 1, Nota = 4, Avaliador = new Avaliador { Nome = "Rui" } },
                new() { CriterioId = 2, Nota = 1 }
            }
        };

        var final = AirtableMapper.NotaFinal(pr, c);

        Assert.NotNull(final);
        Assert.Equal(4.5m, final!.Value.Nota);
        Assert.Contains("Ana: 5 — Cumpre tudo", final.Value.Comentario);
        Assert.Contains("Rui: 4", final.Value.Comentario);
    }

    [Fact]
    public void NotaFinal_Automatico_UsaMemoriaDeCalculo()
    {
        var c = new Criterio { Id = 2, Nome = "Preço", Peso = 10m, TipoAutomatico = TipoCriterioAutomatico.Preco };
        var pr = new Proposta
        {
            Id = 18,
            MemoriaCalculo = new List<MemoriaCalculoAvaliacao>
            {
                new() { CriterioId = 2, Nota = 5m, Justificativa = "Menor preço" }
            }
        };

        var final = AirtableMapper.NotaFinal(pr, c);

        Assert.Equal(5m, final!.Value.Nota);
        Assert.Equal("Cálculo automático: Menor preço", final.Value.Comentario);
    }

    [Fact]
    public void NotaFinal_SemNotas_DevolveNull()
        => Assert.Null(AirtableMapper.NotaFinal(new Proposta { Id = 1 }, new Criterio { Id = 1 }));

    [Fact]
    public void InsertPendentes_GeraParametrosNaOrdemCerta()
    {
        var (sql, parametros) = AirtableSyncInterceptor.InsertPendentes(new[] { ("Processo", 9), ("Proposta", 18) });

        Assert.EndsWith("VALUES ({1}, {2}, {0}, 0, {0}), ({3}, {4}, {0}, 0, {0})", sql);
        Assert.Equal(5, parametros.Length);
        Assert.IsType<DateTime>(parametros[0]);
        Assert.Equal("Processo", parametros[1]);
        Assert.Equal(9, parametros[2]);
        Assert.Equal("Proposta", parametros[3]);
        Assert.Equal(18, parametros[4]);
    }
}
