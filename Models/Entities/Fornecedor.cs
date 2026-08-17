using System.ComponentModel.DataAnnotations;

namespace ComparacaoPropostas.Models.Entities;

public class Fornecedor
{
    public int Id { get; set; }

    [Required, Display(Name = "Nome do Fornecedor")]
    public string Nome { get; set; } = "";

    [Display(Name = "Tipo")]
    public string? Tipo { get; set; }

    [Display(Name = "Contribuinte / NIF")]
    public string? Contribuinte { get; set; }

    [Display(Name = "Contacto")]
    public string? Contacto { get; set; }

    [EmailAddress, Display(Name = "E-mail")]
    public string? Email { get; set; }

    [Display(Name = "Ativo")]
    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;
}
