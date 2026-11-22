using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Services.Stock;

public static class StockCalculos
{
    // Entradas somam, saídas subtraem; ajustes levam o sinal na própria quantidade.
    public static decimal Efeito(MovimentoStock m) => m.Tipo switch
    {
        TipoMovimentoStock.Entrada => m.Quantidade,
        TipoMovimentoStock.Saida => -m.Quantidade,
        _ => m.Quantidade
    };

    public static decimal Saldo(IEnumerable<MovimentoStock> movimentos) => movimentos.Sum(Efeito);

    public static decimal Saldo(IEnumerable<MovimentoStock> movimentos, AreaDepartamento area)
        => movimentos.Where(m => m.Area == area).Sum(Efeito);

    public static Dictionary<AreaDepartamento, decimal> SaldoPorArea(IEnumerable<MovimentoStock> movimentos)
        => movimentos.GroupBy(m => m.Area).ToDictionary(g => g.Key, g => g.Sum(Efeito));

    // Validação de uma saída: quantidade positiva, saldo suficiente na área e, se o material
    // tem números de série em armazém nessa área, um número de série por unidade.
    public static List<string> ValidarSaida(decimal quantidade, decimal saldoArea, int seriesEmArmazem, int seriesEscolhidas)
    {
        var erros = new List<string>();
        if (quantidade <= 0) erros.Add("A quantidade tem de ser maior que zero.");
        if (quantidade > saldoArea) erros.Add($"Só há {saldoArea:0.###} em stock nesta área.");
        if (seriesEmArmazem > 0 && seriesEscolhidas != quantidade)
            erros.Add($"Este material é seguido por número de série: escolha {quantidade:0.###} número(s) de série (escolheu {seriesEscolhidas}).");
        return erros;
    }
}
