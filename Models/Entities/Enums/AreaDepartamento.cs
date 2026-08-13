using System.ComponentModel.DataAnnotations;

namespace ComparacaoPropostas.Models.Entities.Enums;

public enum AreaDepartamento
{
    [Display(Name = "DEP-INF")]
    DepInf,
    [Display(Name = "DEP-PRCS")]
    DepPrcs,
    [Display(Name = "DEP-PRM")]
    DepPrm,
    [Display(Name = "DEP-SAF")]
    DepSaf
}
