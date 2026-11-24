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
    public string? AreaTecnica { get; set; }
    public string? Armazem { get; set; }
    public List<ArmazemStock> Armazens { get; set; } = new();
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

    // Armazém de onde sai (vazio = sem armazém; o saldo validado é o da área).
    [StringLength(20), Display(Name = "Armazém")]
    public string? Armazem { get; set; }

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

public class PainelStockVM
{
    public AreaDepartamento? Area { get; set; }

    // Totais (quantidades somadas só têm sentido por material; no painel geral mostram-se nº de movimentos e valores).
    public int NumMateriaisComStock { get; set; }
    public int NumEntradas { get; set; }
    public int NumSaidas { get; set; }
    public decimal ValorEntradas { get; set; }
    public decimal ValorSaidas { get; set; }
    public decimal ValorStock { get; set; }
    public int MateriaisSemPreco { get; set; }

    public List<ComparacaoPropostas.Services.Stock.AlarmeStock> Alarmes { get; set; } = new();

    // Material escolhido no seletor.
    public List<MaterialStock> Materiais { get; set; } = new();
    public MaterialStock? Material { get; set; }
    public decimal QtdEntradas { get; set; }
    public decimal QtdSaidas { get; set; }
    public decimal QtdAjustes { get; set; }
    public decimal QtdStock { get; set; }

    public List<(string Area, decimal Valor, int Materiais)> PorArea { get; set; } = new();
    public List<(string Mes, decimal Entradas, decimal Saidas)> ValorPorMes { get; set; } = new();
    public List<(int? ProjetoId, string Projeto, int Saidas, decimal Valor)> ConsumoPorProjeto { get; set; } = new();
    public List<MovimentoStock> UltimosMovimentos { get; set; } = new();
}
