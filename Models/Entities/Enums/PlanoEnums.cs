using System.ComponentModel.DataAnnotations;

namespace ComparacaoPropostas.Models.Entities.Enums;

// Valores da coluna "Situação 2027" da folha Scope (lista "Situação" da folha Listas).
public enum SituacaoProjetoPlano
{
    Transitado,
    [Display(Name = "Transitado parcial")]
    TransitadoParcial,
    Novo,
    Condicionado,
    [Display(Name = "Executado – retirar")]
    ExecutadoRetirar,
    Cancelado,
    [Display(Name = "Exploração - DOM")]
    ExploracaoDom
}

// "Caixa do plano": derivada automaticamente da Situação (coluna GI da folha Scope).
public enum CaixaPlano
{
    Entra,
    Condicionado,
    [Display(Name = "Fora do plano")]
    ForaDoPlano,
    [Display(Name = "Sem situação")]
    SemSituacao
}

// Decisão sobre o projeto proposto: só os Aceites formam o Capex e podem ter compras.
public enum EstadoAprovacaoProjeto
{
    Proposto,
    Aceite,
    Rejeitado
}

public enum PrioridadeProjetoPlano
{
    Alta,
    [Display(Name = "Média")]
    Media,
    Baixa
}

// Listas de escolha dos projetos do plano (equivalente à folha "Listas" do ficheiro SCOPE).
public enum ListaPlano
{
    [Display(Name = "Designação da rubrica")]
    Rubrica,
    Tipo,
    [Display(Name = "Coordenação")]
    Coordenacao,
    Ilha,
    [Display(Name = "Classificação do projeto")]
    Classificacao,
    [Display(Name = "CAPEX")]
    Capex,
    [Display(Name = "Origem da iniciativa")]
    OrigemIniciativa,
    [Display(Name = "Trimestre previsto")]
    Trimestre,
    Zona,
    [Display(Name = "Execução / fiscalização")]
    Executante,
    [Display(Name = "Perfil / origem da equipa de montagem")]
    PerfilMontagem,
    [Display(Name = "Sim / Não")]
    SimNao,
    [Display(Name = "Necessidade de aquisição")]
    NecessidadeAquisicao
}

public enum TipoAcaoFormacao
{
    [Display(Name = "Formação")]
    Formacao,
    Feira
}
