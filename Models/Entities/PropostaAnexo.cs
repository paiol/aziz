using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ComparacaoPropostas.Models.Entities;

public class PropostaAnexo
{
    public int Id { get; set; }

    public int PropostaId { get; set; }
    [ValidateNever]
    public Proposta Proposta { get; set; } = null!;

    [Required, Display(Name = "Nome do Ficheiro")]
    public string NomeArquivo { get; set; } = "";

    [Required]
    public string CaminhoArquivo { get; set; } = "";

    public DateTime DataUpload { get; set; } = DateTime.UtcNow;
}
