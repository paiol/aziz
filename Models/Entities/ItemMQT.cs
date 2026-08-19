using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace ComparacaoPropostas.Models.Entities;

public class ItemMQT
{
    public int Id { get; set; }

    public int ProjetoObraId { get; set; }
    [ValidateNever]
    public ProjetoObra ProjetoObra { get; set; } = null!;

    [Display(Name = "Código")]
    public string? CodigoIndexacao { get; set; }

    [Required, Display(Name = "Descrição")]
    public string Descricao { get; set; } = "";

    [Display(Name = "Unidade")]
    public string? Unidade { get; set; }

    [Display(Name = "Quantidade")]
    public decimal Quantidade { get; set; }

    // Marcado quando o item não veio do Mapa de Quantidades original, mas foi adicionado
    // porque um empreiteiro incluiu um item com nome diferente na resposta e o utilizador
    // confirmou que é mesmo um item novo/não previsto (em vez de nomenclatura diferente
    // de um item já existente no MQT).
    [Display(Name = "Não Previsto no MQT")]
    public bool NaoPrevisto { get; set; }

    public ICollection<ItemPropostaEmpreiteiro> ItensProposta { get; set; } = new List<ItemPropostaEmpreiteiro>();
}
