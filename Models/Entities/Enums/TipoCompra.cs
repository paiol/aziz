using System.ComponentModel.DataAnnotations;

namespace ComparacaoPropostas.Models.Entities.Enums;

public enum TipoCompra
{
    [Display(Name = "Nacional")]
    Nacional,
    [Display(Name = "Internacional")]
    Internacional
}
