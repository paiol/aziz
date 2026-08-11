using System.ComponentModel.DataAnnotations;

namespace ComparacaoPropostas.Models.Entities.Enums;

public enum StatusProposta
{
    Recebida,
    [Display(Name = "Em Análise")]
    EmAnalise,
    Aceite,
    Rejeitada
}
