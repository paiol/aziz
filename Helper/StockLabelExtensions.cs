using System.Globalization;
using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Helper;

public static class StockLabelExtensions
{
    private static readonly CultureInfo PtPT = new("pt-PT");

    public static string ToLabel(this TipoEntradaStock tipo) => tipo switch
    {
        TipoEntradaStock.PackingList => "Packing List",
        TipoEntradaStock.CompraDei => "Compra DEI",
        TipoEntradaStock.Manual => "Manual",
        _ => tipo.ToString()
    };

    public static string ToLabel(this TipoMovimentoStock tipo) => tipo switch
    {
        TipoMovimentoStock.Entrada => "Entrada",
        TipoMovimentoStock.Saida => "Saída",
        TipoMovimentoStock.Ajuste => "Ajuste",
        _ => tipo.ToString()
    };

    public static string ToBadgeClass(this TipoMovimentoStock tipo) => tipo switch
    {
        TipoMovimentoStock.Entrada => "badge bg-success",
        TipoMovimentoStock.Saida => "badge bg-warning text-dark",
        _ => "badge bg-secondary"
    };

    public static string ToLabel(this EstadoNumeroSerie estado) => estado == EstadoNumeroSerie.EmArmazem ? "Em armazém" : "Saiu";

    public static string ToLabel(this OrigemNomeMaterial origem) => origem switch
    {
        OrigemNomeMaterial.Inventario => "Inventário do armazém",
        OrigemNomeMaterial.PackingList => "Packing List",
        OrigemNomeMaterial.Entrada => "Entrada / compra DEI",
        _ => origem.ToString()
    };

    public static string ToLabel(this EstadoDivergenciaNome estado) => estado switch
    {
        EstadoDivergenciaNome.Pendente => "Por decidir",
        EstadoDivergenciaNome.MantidoCatalogo => "Mantido o nome do catálogo",
        EstadoDivergenciaNome.NomeAtualizado => "Catálogo atualizado",
        _ => estado.ToString()
    };

    // Quantidades de stock: sem casas decimais desnecessárias (metros podem ter decimais).
    public static string ToQtd(this decimal valor) => valor.ToString("#,##0.###", PtPT);
}
