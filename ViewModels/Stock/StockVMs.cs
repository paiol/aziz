using System.ComponentModel.DataAnnotations;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.ViewModels.Stock;

public class LinhaSaldoVM
{
    public MaterialStock Material { get; set; } = null!;
    public Dictionary<AreaDepartamento, decimal> PorArea { get; set; } = new();
    public decimal Total => PorArea.Values.Sum();
    public bool AbaixoMinimo => Material.StockMinimo is decimal m && Total < m;
    public int SeriesEmArmazem { get; set; }
}

public class StockIndexVM
{
    public AreaDepartamento? Area { get; set; }
    public string? Pesquisa { get; set; }
    public bool SoComStock { get; set; } = true;
    public List<LinhaSaldoVM> Linhas { get; set; } = new();
    public int NumEntradas { get; set; }
    public int NumSaidas { get; set; }
    public List<(string Mes, int Entradas, int Saidas)> MovimentosPorMes { get; set; } = new();
    public List<(AreaDepartamento Area, int Materiais)> MateriaisPorArea { get; set; } = new();
}

public class LinhaEntradaManualVM
{
    public string? Codigo { get; set; }
    public string? Descricao { get; set; }
    public string? Unidade { get; set; }
    public decimal? Quantidade { get; set; }
    // Números de série separados por espaço, vírgula ou mudança de linha (opcional).
    public string? Series { get; set; }
}

public class NovaEntradaVM
{
    public EntradaStock Entrada { get; set; } = new();
    // Processo de compra da plataforma (dá o pedido, o projeto Capex e o fornecedor).
    public int? ProcessoId { get; set; }
    public List<LinhaEntradaManualVM> Linhas { get; set; } = Enumerable.Range(0, 3).Select(_ => new LinhaEntradaManualVM()).ToList();
}

public class NovaSaidaVM
{
    [Required, Display(Name = "Material")]
    public int? MaterialStockId { get; set; }

    [Display(Name = "Área")]
    public AreaDepartamento Area { get; set; } = AreaDepartamento.DepInf;

    [Display(Name = "Quantidade")]
    public decimal Quantidade { get; set; } = 1;

    [Display(Name = "Data")]
    [DataType(DataType.Date)]
    public DateTime Data { get; set; } = DateTime.Today;

    [Display(Name = "Projeto Capex")]
    public int? ProjetoPlanoId { get; set; }

    [StringLength(150), Display(Name = "Site")]
    public string? Site { get; set; }

    [StringLength(150), Display(Name = "Destino / requisitante")]
    public string? Destino { get; set; }

    [StringLength(100), Display(Name = "Responsável")]
    public string? Responsavel { get; set; }

    [Display(Name = "Observações")]
    public string? Observacoes { get; set; }

    public List<int> SeriesEscolhidas { get; set; } = new();
}
