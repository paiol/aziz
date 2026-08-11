using System.ComponentModel.DataAnnotations;

namespace ComparacaoPropostas.Models.Entities.Enums;

public enum StatusProcesso
{
    Aberto,
    [Display(Name = "Em Avaliação")]
    EmAvaliacao,
    Decidido,
    Cancelado
}
