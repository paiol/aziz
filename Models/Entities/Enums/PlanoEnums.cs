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

public enum PrioridadeProjetoPlano
{
    Alta,
    [Display(Name = "Média")]
    Media,
    Baixa
}

public enum TipoAcaoFormacao
{
    [Display(Name = "Formação")]
    Formacao,
    Feira
}
