using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ComparacaoPropostas.Models.Entities;

public class MemoriaCalculoAvaliacao
{
    public int Id { get; set; }

    public int PropostaId { get; set; }
    [ValidateNever]
    public Proposta Proposta { get; set; } = null!;

    public int CriterioId { get; set; }
    [ValidateNever]
    public Criterio Criterio { get; set; } = null!;

    public decimal Nota { get; set; }

    public string Justificativa { get; set; } = "";

    public DateTime CalculadoEm { get; set; } = DateTime.UtcNow;
}
