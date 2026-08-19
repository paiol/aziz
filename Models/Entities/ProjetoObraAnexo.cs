using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Models.Entities;

public class ProjetoObraAnexo
{
    public int Id { get; set; }

    public int ProjetoObraId { get; set; }
    [ValidateNever]
    public ProjetoObra ProjetoObra { get; set; } = null!;

    [Required, Display(Name = "Nome do Ficheiro")]
    public string NomeArquivo { get; set; } = "";

    [Required]
    public string CaminhoArquivo { get; set; } = "";

    [Display(Name = "Tipo de Documento")]
    public TipoDocumentoObra TipoDocumento { get; set; } = TipoDocumentoObra.DocDiversos;

    public DateTime DataUpload { get; set; } = DateTime.UtcNow;
}
