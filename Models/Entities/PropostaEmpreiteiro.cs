using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Models.Entities;

public class PropostaEmpreiteiro
{
    public int Id { get; set; }

    public int ProjetoObraId { get; set; }
    [ValidateNever]
    public ProjetoObra ProjetoObra { get; set; } = null!;

    [Required, Display(Name = "Empreiteiro")]
    public string Empreiteiro { get; set; } = "";

    [Display(Name = "Prazo de Entrega (dias)")]
    public int? PrazoEntregaDias { get; set; }

    [Display(Name = "Validade da Proposta")]
    [DataType(DataType.Date)]
    public DateTime? ValidadeProposta { get; set; }

    [Display(Name = "Estado")]
    public StatusPropostaEmpreiteiro Status { get; set; } = StatusPropostaEmpreiteiro.Recebida;

    [Display(Name = "Observações")]
    public string? Observacoes { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    public ICollection<ItemPropostaEmpreiteiro> ItensProposta { get; set; } = new List<ItemPropostaEmpreiteiro>();
    public ICollection<AvaliacaoObra> Avaliacoes { get; set; } = new List<AvaliacaoObra>();

    [NotMapped]
    public decimal Subtotal => ItensProposta.Where(i => i.Incluido).Sum(i => i.Subtotal);
}
