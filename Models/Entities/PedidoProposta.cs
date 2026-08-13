using System.ComponentModel.DataAnnotations;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Models.Entities;

public class PedidoProposta
{
    public int Id { get; set; }

    [Required, Display(Name = "Tipo de Proposta")]
    public string TipoProposta { get; set; } = "";

    [Display(Name = "Área")]
    public AreaDepartamento Area { get; set; }

    [Display(Name = "Estado")]
    public StatusPedido Status { get; set; } = StatusPedido.EmCurso;

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    [Required, Display(Name = "Pessoa Criou")]
    public string PessoaCriou { get; set; } = "";

    [Display(Name = "Prazo de Entrega")]
    [DataType(DataType.Date)]
    public DateTime? PrazoEntrega { get; set; }

    [Display(Name = "Orçamento Estimado")]
    public decimal? OrcamentoEstimado { get; set; }

    // Set once a Processo picks this Pedido as its 1-1 origin; null while still Pendente.
    public Processo? Processo { get; set; }

    // The items being requested from the supplier — belong to the Pedido (the request
    // itself), not to the Processo, so they exist independently of a Processo being created.
    public ICollection<ItemPedido> ItensPedido { get; set; } = new List<ItemPedido>();
}
