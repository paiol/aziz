using System.Globalization;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Helper;

public static class MoedaHelper
{
    public const decimal TaxaEurCvePadrao = 110.265m;
    public const string MoedaCve = "CVE";
    public const string MoedaEur = "EUR";

    public static readonly string[] MoedasSuportadas = { MoedaCve, MoedaEur };

    public static string MoedaParaTipoCompra(TipoCompra tipoCompra)
        => tipoCompra == TipoCompra.Internacional ? MoedaEur : MoedaCve;

    public static decimal ConverterEurParaCve(decimal valorEur, decimal taxa)
    {
        if (taxa <= 0) taxa = TaxaEurCvePadrao;
        return Math.Round(valorEur * taxa, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal ConverterCveParaEur(decimal valorCve, decimal taxa)
    {
        if (taxa <= 0) taxa = TaxaEurCvePadrao;
        return Math.Round(valorCve / taxa, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal CalcularTotalCve(decimal valorTotal, string? moeda, decimal taxa)
    {
        var m = (moeda ?? MoedaCve).Trim().ToUpperInvariant();
        if (m == MoedaEur)
        {
            return ConverterEurParaCve(valorTotal, taxa);
        }
        return Math.Round(valorTotal, 2, MidpointRounding.AwayFromZero);
    }

    public static decimal? CalcularTotalEur(decimal valorTotal, string? moeda, decimal taxa)
    {
        var m = (moeda ?? MoedaCve).Trim().ToUpperInvariant();
        if (m == MoedaEur)
        {
            return Math.Round(valorTotal, 2, MidpointRounding.AwayFromZero);
        }
        if (taxa > 0)
        {
            return ConverterCveParaEur(valorTotal, taxa);
        }
        return null;
    }

    public static string FormatarValor(decimal valor, string? moeda)
    {
        var m = (moeda ?? MoedaCve).Trim().ToUpperInvariant();
        var ptPT = new CultureInfo("pt-PT");
        if (m == MoedaEur)
        {
            return valor.ToString("N2", ptPT) + " €";
        }
        return valor.ToString("N2", ptPT) + " CVE";
    }
}
