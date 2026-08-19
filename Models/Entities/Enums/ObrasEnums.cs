using System.ComponentModel.DataAnnotations;

namespace ComparacaoPropostas.Models.Entities.Enums;

public enum TipoProjetoObra
{
    [Display(Name = "Edificação")]
    Edificacao,
    Infraestrutura,
    Especiais
}

public enum StatusProjetoObra
{
    [Display(Name = "Em Concurso")]
    EmConcurso,
    [Display(Name = "Em Execução")]
    EmExecucao,
    [Display(Name = "Concluído")]
    Concluido
}

public enum TipoDocumentoObra
{
    Desenhos,
    [Display(Name = "Caderno de Encargos")]
    CadernoEncargos,
    [Display(Name = "Mapa de Quantidades")]
    MapaQuantidades,
    Fotografias,
    [Display(Name = "Doc. Diversos")]
    DocDiversos
}

public enum StatusPropostaEmpreiteiro
{
    Recebida,
    [Display(Name = "Em Análise")]
    EmAnalise,
    Aceite,
    Rejeitada
}
