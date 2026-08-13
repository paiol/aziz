using System.ComponentModel.DataAnnotations;

namespace ComparacaoPropostas.Models.Entities.Enums;

public enum StatusProcesso
{
    Criado,
    Recebido,
    [Display(Name = "Concluído")]
    Concluido,
    Cancelado
}
