using System.ComponentModel.DataAnnotations;

namespace ComparacaoPropostas.Models.Entities.Enums;

public enum StatusPedido
{
    [Display(Name = "Em Curso")]
    EmCurso,
    Finalizado,
    Cancelado
}
