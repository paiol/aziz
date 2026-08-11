using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Data;

public static class SeedData
{
    public static void Ensure(AppDbContext db)
    {
        if (db.Processos.Any()) return;

        const string dominio = "Equipamentos e Materiais";

        var criterios = new List<CriterioAvaliacao>
        {
            new() { Nome = "Preço", Categoria = "Financeira", Dominio = dominio },
            new() { Nome = "Prazo de Entrega", Categoria = "Financeira", Dominio = dominio },
            new() { Nome = "Qualidade Técnica da Solução", Categoria = "Técnica", Dominio = dominio },
            new() { Nome = "Garantia e Suporte", Categoria = "Técnica", Dominio = dominio },
        };
        db.CriteriosAvaliacao.AddRange(criterios);

        var item = new ItemMaterial { NomeItem = "Quadro Elétrico de Distribuição", Categoria = "Equipamentos", Unidade = "un" };
        db.ItensMaterial.Add(item);

        var processo = new Processo
        {
            Nome = "Quadros Elétricos - Exemplo",
            Descricao = "Processo de demonstração criado automaticamente.",
            Status = StatusProcesso.EmAvaliacao,
            TipoProcesso = dominio,
            OrcamentoEstimado = 18000000m,
            CriadoPor = "Sistema"
        };
        db.Processos.Add(processo);

        var pesos = new decimal[] { 40, 20, 25, 15 };
        var processoCriterios = criterios.Select((c, i) => new ProcessoCriterio
        {
            Processo = processo,
            CriterioAvaliacao = c,
            Peso = pesos[i]
        }).ToList();
        db.ProcessosCriterio.AddRange(processoCriterios);

        var fornecedores = new[] { "Inovagera Tecnologia e Sistemas de Energia, Lda.", "Resul - Componentes de Energia, S.A." };
        var valores = new[] { 17979097.49m, 18105623.27m };
        var prazos = new[] { 30, 35 };
        var notasPorFornecedor = new[]
        {
            new decimal[] { 8, 9, 8, 7 },
            new decimal[] { 7, 8, 9, 8 },
        };

        for (var i = 0; i < fornecedores.Length; i++)
        {
            var proposta = new Proposta
            {
                Processo = processo,
                Fornecedor = fornecedores[i],
                ValorTotal = valores[i],
                PrazoEntregaDias = prazos[i],
                Status = StatusProposta.Recebida
            };
            db.Propostas.Add(proposta);

            for (var c = 0; c < processoCriterios.Count; c++)
            {
                db.Avaliacoes.Add(new Avaliacao
                {
                    Proposta = proposta,
                    ProcessoCriterio = processoCriterios[c],
                    Nota = notasPorFornecedor[i][c]
                });
            }

            db.ItensProposta.Add(new ItemProposta
            {
                Proposta = proposta,
                ItemMaterial = item,
                Incluido = true,
                Quantidade = 2,
                PrecoUnitario = valores[i] / 2
            });
        }

        db.SaveChanges();
    }
}
