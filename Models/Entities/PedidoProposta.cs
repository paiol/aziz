using System.ComponentModel.DataAnnotations;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Models.Entities;

public class PedidoProposta
{
    public int Id { get; set; }

    [Required, Display(Name = "Tipo de Proposta")]
    public string TipoProposta { get; set; } = "";

    [Required, Display(Name = "Área")]
    public string Area { get; set; } = "";

    [Display(Name = "Estado")]
    public StatusPedido Status { get; set; } = StatusPedido.Pendente;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    [Required, Display(Name = "Pessoa Criou")]
    public string PessoaCriou { get; set; } = "";

    // Set once a Processo picks this Pedido as its 1-1 origin; null while still Pendente.
    public Processo? Processo { get; set; }
}
