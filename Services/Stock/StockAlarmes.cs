using ComparacaoPropostas.Helper;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Services.Stock;

public enum TipoAlarmeStock
{
    SaldoNegativo,
    Esgotado,
    AbaixoMinimo,
    Parado,
    NomeDiferente
}

public enum SeveridadeAlarme
{
    Critico,
    Aviso,
    Info
}

public record AlarmeStock(TipoAlarmeStock Tipo, SeveridadeAlarme Severidade, MaterialStock Material, AreaDepartamento? Area, decimal Saldo, string Mensagem,
    DivergenciaNomeMaterial? Divergencia = null);

// Alarmes do stock, calculados a partir dos movimentos (nada fica guardado):
//  - Saldo negativo numa área (saiu mais do que entrou: erro de registo)          → crítico
//  - Esgotado: o material tem stock mínimo definido e o saldo total chegou a 0     → crítico
//  - Abaixo do mínimo: saldo total abaixo do stock mínimo                          → aviso
//  - Parado: há stock mas não sai nada há mais de N dias (por omissão 180)         → info
//  - Nome diferente: o mesmo código chegou com outro nome (armazém / Packing List)   → aviso
//    (vem dos alertas DivergenciaNomeMaterial por decidir; incluir m.DivergenciasNome para os ver)
public static class StockAlarmes
{
    public const int DiasParadoPadrao = 180;

    public static List<AlarmeStock> Calcular(IEnumerable<MaterialStock> materiais, DateTime hoje, AreaDepartamento? area = null)
    {
        var alarmes = new List<AlarmeStock>();
        foreach (var m in materiais)
        {
            foreach (var d in m.DivergenciasNome.Where(d => d.Estado == EstadoDivergenciaNome.Pendente))
                alarmes.Add(new AlarmeStock(TipoAlarmeStock.NomeDiferente, SeveridadeAlarme.Aviso, m, null, StockCalculos.Saldo(m.Movimentos),
                    $"{d.Origem.ToLabel()}{(d.Referencia != null ? $" ({d.Referencia})" : "")} chama-lhe \"{d.NomeRecebido}\".", d));

            var movs = m.Movimentos.Where(x => area == null || x.Area == area).ToList();
            if (movs.Count == 0) continue;

            foreach (var (a, saldoArea) in StockCalculos.SaldoPorArea(movs).Where(x => x.Value < 0))
                alarmes.Add(new AlarmeStock(TipoAlarmeStock.SaldoNegativo, SeveridadeAlarme.Critico, m, a, saldoArea,
                    $"Saldo negativo em {a.ToLabel()} ({saldoArea:0.###}): há saídas sem a entrada correspondente."));

            var saldo = StockCalculos.Saldo(movs);
            if (m.StockMinimo is decimal minimo && minimo > 0)
            {
                if (saldo <= 0)
                    alarmes.Add(new AlarmeStock(TipoAlarmeStock.Esgotado, SeveridadeAlarme.Critico, m, area, saldo,
                        $"Esgotado (mínimo {minimo:0.###})."));
                else if (saldo < minimo)
                    alarmes.Add(new AlarmeStock(TipoAlarmeStock.AbaixoMinimo, SeveridadeAlarme.Aviso, m, area, saldo,
                        $"Abaixo do mínimo: {saldo:0.###} de {minimo:0.###}."));
            }

            if (saldo > 0)
            {
                var dias = m.DiasParado ?? DiasParadoPadrao;
                // Última saída; sem saídas conta desde a primeira entrada.
                var ultimaSaida = movs.Where(x => x.Tipo == TipoMovimentoStock.Saida).Select(x => (DateTime?)x.Data).Max();
                var desde = ultimaSaida ?? movs.Min(x => x.Data);
                var parado = (hoje.Date - desde.Date).Days;
                if (parado > dias)
                    alarmes.Add(new AlarmeStock(TipoAlarmeStock.Parado, SeveridadeAlarme.Info, m, area, saldo,
                        ultimaSaida == null ? $"Sem saídas há {parado} dias (desde a entrada)." : $"Sem saídas há {parado} dias."));
            }
        }
        return alarmes.OrderBy(a => a.Severidade).ThenBy(a => a.Material.Descricao).ToList();
    }

    public static string ToLabel(this TipoAlarmeStock t) => t switch
    {
        TipoAlarmeStock.SaldoNegativo => "Saldo negativo",
        TipoAlarmeStock.Esgotado => "Esgotado",
        TipoAlarmeStock.AbaixoMinimo => "Abaixo do mínimo",
        TipoAlarmeStock.Parado => "Stock parado",
        TipoAlarmeStock.NomeDiferente => "Nome diferente",
        _ => t.ToString()
    };

    public static string ToBadgeClass(this SeveridadeAlarme s) => s switch
    {
        SeveridadeAlarme.Critico => "badge bg-danger",
        SeveridadeAlarme.Aviso => "badge bg-warning text-dark",
        _ => "badge bg-info text-dark"
    };

    private static string ToLabel(this AreaDepartamento a) => ComparacaoPropostas.Helper.StatusBadgeExtensions.ToLabel(a);
}
