using System.ComponentModel.DataAnnotations;

namespace ComparacaoPropostas.Models.Entities;

public class Avaliador
{
    public int Id { get; set; }

    [Required, Display(Name = "Nome do Avaliador")]
    public string Nome { get; set; } = "";

    [Display(Name = "Perfil / Cargo")]
    public string? Perfil { get; set; }

    [EmailAddress, Display(Name = "E-mail")]
    public string? Email { get; set; }

    [Display(Name = "Ativo")]
    public bool Ativo { get; set; } = true;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<Avaliacao> Avaliacoes { get; set; } = new List<Avaliacao>();
}
