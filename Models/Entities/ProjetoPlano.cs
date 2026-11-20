using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Services;

namespace ComparacaoPropostas.Models.Entities;

// Uma linha da folha "Scope DEP-INF" do plano. O motor de cálculo detalhado da folha
// (preços unitários, deslocações, serviços externos — colunas AU a GH) fica fora desta fase:
// aqui guardam-se só os totais por natureza de custo, editáveis à mão ou importados do Excel.
public class ProjetoPlano
{
    public int Id { get; set; }

    public int PlanoId { get; set; }
    [ValidateNever]
    public Plano Plano { get; set; } = null!;

    // Projeto do plano do ano anterior de onde este transitou (null = proposto de raiz).
    public int? ProjetoOrigemId { get; set; }
    [ValidateNever]
    public ProjetoPlano? ProjetoOrigem { get; set; }

    // Compras (pedidos de proposta → processos) que executam este projeto.
    [ValidateNever]
    public ICollection<PedidoProposta> Pedidos { get; set; } = new List<PedidoProposta>();

    // Área que propõe o projeto (o plano é feito por áreas).
    [Display(Name = "Área")]
    public AreaDepartamento Area { get; set; } = AreaDepartamento.DepInf;

    // Aceite = entra no Capex do ano e pode ter compras associadas.
    [Display(Name = "Aprovação")]
    public EstadoAprovacaoProjeto EstadoAprovacao { get; set; } = EstadoAprovacaoProjeto.Proposto;

    [Display(Name = "Motivo da decisão")]
    public string? MotivoDecisao { get; set; }

    [Display(Name = "Data da decisão")]
    public DateTime? DataDecisao { get; set; }

    // Identificação
    [Display(Name = "Capex ano -2")]
    public string? CapexAnoMenos2 { get; set; }

    [Display(Name = "Cód. Capex ano anterior")]
    public string? CodCapexAnoAnterior { get; set; }

    [Display(Name = "Cód. Capex")]
    public string? CodCapex { get; set; }

    [Display(Name = "Seguimento Orçamento")]
    public string? SeguimentoOrcamento { get; set; }

    [Display(Name = "Designação da rubrica")]
    public string? DesignacaoRubrica { get; set; }

    [Display(Name = "Origem iniciativa")]
    public string? OrigemIniciativa { get; set; }

    [Display(Name = "Tipo")]
    public string? Tipo { get; set; }

    [Display(Name = "Coord.")]
    public string? Coordenacao { get; set; }

    [Display(Name = "Ilha")]
    public string? Ilha { get; set; }

    [Display(Name = "Classificação Projeto")]
    public string? Classificacao { get; set; }

    [Required, Display(Name = "Projeto")]
    public string Nome { get; set; } = "";

    [Display(Name = "Descrição")]
    public string? Descricao { get; set; }

    [Display(Name = "Observações / justificação")]
    public string? Observacoes { get; set; }

    [Display(Name = "CAPEX")]
    public string? Capex { get; set; }

    [Display(Name = "Situação")]
    public SituacaoProjetoPlano? Situacao { get; set; }

    [Display(Name = "Prioridade")]
    public PrioridadeProjetoPlano? Prioridade { get; set; }

    [Display(Name = "Trimestre previsto")]
    public string? TrimestrePrevisto { get; set; }

    // Fração 0–1 (a folha mostra em %).
    [Display(Name = "Execução ano anterior (%)")]
    public decimal? ExecucaoAnoAnterior { get; set; }

    [Display(Name = "Zona")]
    public string? Zona { get; set; }

    // Quem executa / fiscaliza (DEI, DOM, Terceiro, NA, Chave na mão…)
    [Display(Name = "Execução · Projeto e levantamento")]
    public string? ExecucaoProjeto { get; set; }

    [Display(Name = "Fiscalização · civil")]
    public string? FiscalizacaoCivil { get; set; }

    [Display(Name = "Fiscalização · traçados")]
    public string? FiscalizacaoTracados { get; set; }

    [Display(Name = "Execução · Montagem equipamentos")]
    public string? ExecucaoMontagem { get; set; }

    [Display(Name = "Perfil / origem da equipa de montagem")]
    public string? PerfilEquipaMontagem { get; set; }

    [Display(Name = "Fiscalização · montagem equipamentos")]
    public string? FiscalizacaoMontagem { get; set; }

    [Display(Name = "Prestador externo da ilha?")]
    public string? PrestadorExternoIlha { get; set; }

    [Display(Name = "Necessidade aquisição")]
    public string? NecessidadeAquisicao { get; set; }

    [Display(Name = "Entidades executantes")]
    public string? EntidadesExecutantes { get; set; }

    // Valores (CVE)
    [Display(Name = "Propostas sem adjudicação")]
    public decimal? PropostasSemAdjudicacao { get; set; }

    [Display(Name = "Adjudicado ano anterior")]
    public decimal? AdjudicadoAnoAnterior { get; set; }

    [Display(Name = "Adjudicado anos anteriores")]
    public decimal? AdjudicadoAnosAnteriores { get; set; }

    [Display(Name = "Valor a transitar")]
    public decimal? ValorTransitar { get; set; }

    [Display(Name = "Valor novo (estimativa)")]
    public decimal? ValorNovo { get; set; }

    [Display(Name = "Valor condicionado")]
    public decimal? ValorCondicionado { get; set; }

    [Display(Name = "TOTAL ORC. ano anterior (ref.)")]
    public decimal? TotalOrcamentoAnoAnterior { get; set; }

    // Custos por natureza
    [Display(Name = "Aquisição site")]
    public decimal? CustoAquisicaoSite { get; set; }

    [Display(Name = "Equipamento")]
    public decimal? CustoEquipamento { get; set; }

    [Display(Name = "Construções")]
    public decimal? CustoConstrucoes { get; set; }

    [Display(Name = "Serviços")]
    public decimal? CustoServicos { get; set; }

    [Display(Name = "Deslocações da equipa")]
    public decimal? CustoDeslocacoes { get; set; }

    [Display(Name = "dos quais terceirização")]
    public decimal? CustoTerceirizacao { get; set; }

    // Calendário e esforço
    [Display(Name = "Condição / motivo")]
    public string? CondicaoMotivo { get; set; }

    [Range(1, 12), Display(Name = "Início (mês)")]
    public int? MesInicio { get; set; }

    [Range(1, 12), Display(Name = "Fim (mês)")]
    public int? MesFim { get; set; }

    [Display(Name = "Esforço Arquitetura (dias)")]
    public decimal? EsforcoArquiteturaDias { get; set; }

    [Display(Name = "Esforço Energia (dias)")]
    public decimal? EsforcoEnergiaDias { get; set; }

    [NotMapped]
    public CaixaPlano Caixa => PlanoCalculos.CaixaDoPlano(Situacao);

    [NotMapped]
    public decimal TotalOrcamento => PlanoCalculos.TotalOrcamento(this);

    [NotMapped]
    public decimal TotalCustos => PlanoCalculos.TotalCustos(this);

    [NotMapped]
    public decimal DeltaAnoAnterior => TotalOrcamento - (TotalOrcamentoAnoAnterior ?? 0m);
}
