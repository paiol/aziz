using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Models.Entities;

// ---- Gestão de Stock: materiais comprados pela DEI e guardados em armazém, por área ----------

public enum TipoEntradaStock
{
    [Display(Name = "Packing List")]
    PackingList,
    [Display(Name = "Compra DEI")]
    CompraDei,
    Manual
}

public enum TipoMovimentoStock
{
    Entrada,
    [Display(Name = "Saída")]
    Saida,
    Ajuste
}

public enum EstadoNumeroSerie
{
    [Display(Name = "Em armazém")]
    EmArmazem,
    [Display(Name = "Saiu")]
    Saiu
}

// Material do catálogo de stock, identificado pelo código do fornecedor (Part Number na Huawei).
// O saldo não se guarda: calcula-se pelos movimentos, por área.
public class MaterialStock
{
    public int Id { get; set; }

    [Required, StringLength(60), Display(Name = "Código")]
    public string Codigo { get; set; } = "";

    [StringLength(100), Display(Name = "Modelo")]
    public string? Modelo { get; set; }

    [Required, Display(Name = "Descrição")]
    public string Descricao { get; set; } = "";

    [StringLength(20), Display(Name = "Unidade")]
    public string? Unidade { get; set; }

    [StringLength(100), Display(Name = "Fabricante")]
    public string? Fabricante { get; set; }

    // Custo unitário em CVE (para os valores do stock). Opcional.
    [Display(Name = "Preço unitário (CVE)")]
    public decimal? PrecoUnitario { get; set; }

    // Dias sem saídas a partir dos quais o stock é considerado parado (vazio = regra geral, 180 dias).
    [Display(Name = "Alarme de stock parado (dias)")]
    public int? DiasParado { get; set; }

    // Aviso quando o saldo (total de todas as áreas) fica abaixo deste valor.
    [Display(Name = "Stock mínimo")]
    public decimal? StockMinimo { get; set; }

    public ICollection<MovimentoStock> Movimentos { get; set; } = new List<MovimentoStock>();
    public ICollection<NumeroSerieStock> NumerosSerie { get; set; } = new List<NumeroSerieStock>();
}

// Uma receção de material: um Packing List da Huawei, uma compra da DEI ou uma entrada manual.
public class EntradaStock
{
    public int Id { get; set; }

    [Display(Name = "Tipo")]
    public TipoEntradaStock Tipo { get; set; } = TipoEntradaStock.Manual;

    [Display(Name = "Área")]
    public AreaDepartamento Area { get; set; } = AreaDepartamento.DepInf;

    [Required, StringLength(60), Display(Name = "Nº documento")]
    public string Numero { get; set; } = "";

    [Display(Name = "Data")]
    [DataType(DataType.Date)]
    public DateTime Data { get; set; } = DateTime.Today;

    [StringLength(150), Display(Name = "Fornecedor")]
    public string? Fornecedor { get; set; }

    [StringLength(60), Display(Name = "Contrato")]
    public string? Contrato { get; set; }

    // Contrato-quadro anual com a Huawei (ex.: "1+1_2026").
    [StringLength(30), Display(Name = "Contrato 1+1")]
    public string? Contrato1Mais1 { get; set; }

    [StringLength(60), Display(Name = "Fatura")]
    public string? Fatura { get; set; }

    [StringLength(60), Display(Name = "P/O")]
    public string? PurchaseOrder { get; set; }

    [StringLength(200), Display(Name = "Projeto do fornecedor")]
    public string? ProjetoFornecedor { get; set; }

    [Display(Name = "Projeto Capex")]
    public int? ProjetoPlanoId { get; set; }
    [ValidateNever]
    public ProjetoPlano? ProjetoPlano { get; set; }

    [Display(Name = "Pedido de compra")]
    public int? PedidoPropostaId { get; set; }
    [ValidateNever]
    public PedidoProposta? PedidoProposta { get; set; }

    [StringLength(100), Display(Name = "Armazém / localização")]
    public string? Localizacao { get; set; }

    [Display(Name = "Observações")]
    public string? Observacoes { get; set; }

    [StringLength(200)]
    public string? NomeFicheiro { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    [ValidateNever]
    public ICollection<MovimentoStock> Movimentos { get; set; } = new List<MovimentoStock>();
}

// Movimento de stock. A quantidade é sempre positiva; o Tipo diz se soma (Entrada/Ajuste +) ou subtrai (Saída).
// Ajustes negativos usam Tipo = Ajuste com Quantidade negativa.
public class MovimentoStock
{
    public int Id { get; set; }

    public int MaterialStockId { get; set; }
    [ValidateNever]
    public MaterialStock Material { get; set; } = null!;

    [Display(Name = "Área")]
    public AreaDepartamento Area { get; set; }

    [Display(Name = "Tipo")]
    public TipoMovimentoStock Tipo { get; set; }

    [Display(Name = "Quantidade")]
    public decimal Quantidade { get; set; }

    [Display(Name = "Data")]
    [DataType(DataType.Date)]
    public DateTime Data { get; set; } = DateTime.Today;

    public int? EntradaStockId { get; set; }
    [ValidateNever]
    public EntradaStock? Entrada { get; set; }

    [Display(Name = "Projeto Capex")]
    public int? ProjetoPlanoId { get; set; }
    [ValidateNever]
    public ProjetoPlano? ProjetoPlano { get; set; }

    // Inventário que originou este movimento (ajuste de contagem).
    public int? InventarioStockId { get; set; }
    [ValidateNever]
    public InventarioStock? Inventario { get; set; }

    [StringLength(30), Display(Name = "Caixa")]
    public string? Caixa { get; set; }

    [StringLength(60), Display(Name = "Embalagem")]
    public string? Embalagem { get; set; }

    [StringLength(150), Display(Name = "Site")]
    public string? Site { get; set; }

    [StringLength(150), Display(Name = "Destino / requisitante")]
    public string? Destino { get; set; }

    [StringLength(100), Display(Name = "Responsável")]
    public string? Responsavel { get; set; }

    [Display(Name = "Observações")]
    public string? Observacoes { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}

// Equipamento seguido individualmente pelo número de série.
public class NumeroSerieStock
{
    public int Id { get; set; }

    public int MaterialStockId { get; set; }
    [ValidateNever]
    public MaterialStock Material { get; set; } = null!;

    [Required, StringLength(80), Display(Name = "Nº de série")]
    public string Serie { get; set; } = "";

    [Display(Name = "Área")]
    public AreaDepartamento Area { get; set; }

    [Display(Name = "Estado")]
    public EstadoNumeroSerie Estado { get; set; } = EstadoNumeroSerie.EmArmazem;

    [StringLength(30)]
    public string? Caixa { get; set; }

    public int? EntradaStockId { get; set; }
    [ValidateNever]
    public EntradaStock? Entrada { get; set; }

    // Movimento de saída em que o equipamento saiu do armazém.
    public int? MovimentoSaidaId { get; set; }
    [ValidateNever]
    public MovimentoStock? MovimentoSaida { get; set; }
}

public enum EstadoInventario
{
    Aberto,
    Fechado
}

// Inventário (contagem física) de um armazém numa área. Ao fechar, o stock da área passa a ser o
// contado: a diferença para o saldo da plataforma fica registada como movimento de Ajuste.
public class InventarioStock
{
    public int Id { get; set; }

    [Display(Name = "Área")]
    public AreaDepartamento Area { get; set; } = AreaDepartamento.DepInf;

    [Display(Name = "Data")]
    [DataType(DataType.Date)]
    public DateTime Data { get; set; } = DateTime.Today;

    [StringLength(100), Display(Name = "Armazém / localização")]
    public string? Localizacao { get; set; }

    [StringLength(200), Display(Name = "Descrição")]
    public string? Descricao { get; set; }

    [StringLength(100), Display(Name = "Responsável")]
    public string? Responsavel { get; set; }

    [Display(Name = "Estado")]
    public EstadoInventario Estado { get; set; } = EstadoInventario.Aberto;

    [StringLength(200)]
    public string? NomeFicheiro { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
    public DateTime? FechadoEm { get; set; }

    [ValidateNever]
    public ICollection<LinhaInventario> Linhas { get; set; } = new List<LinhaInventario>();
}

public class LinhaInventario
{
    public int Id { get; set; }

    public int InventarioStockId { get; set; }
    [ValidateNever]
    public InventarioStock Inventario { get; set; } = null!;

    // Preenchido quando o código já existe no catálogo (ou ao fechar, quando o material é criado).
    public int? MaterialStockId { get; set; }
    [ValidateNever]
    public MaterialStock? Material { get; set; }

    [Required, StringLength(60), Display(Name = "Código")]
    public string Codigo { get; set; } = "";

    [Display(Name = "Descrição")]
    public string? Descricao { get; set; }

    [StringLength(20), Display(Name = "Unidade")]
    public string? Unidade { get; set; }

    // Vazio = ainda não contado (a linha é ignorada ao fechar).
    [Display(Name = "Contado")]
    public decimal? QuantidadeContada { get; set; }

    // Saldo da plataforma no momento em que o inventário foi fechado.
    [Display(Name = "Sistema")]
    public decimal? QuantidadeSistema { get; set; }

    // Números de série contados (separados por espaço, vírgula ou mudança de linha).
    [Display(Name = "Nºs de série")]
    public string? Series { get; set; }

    [Display(Name = "Observações")]
    public string? Observacoes { get; set; }
}
