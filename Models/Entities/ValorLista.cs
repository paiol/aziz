using System.ComponentModel.DataAnnotations;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Models.Entities;

// Um valor de uma lista de escolha do plano (Ilha, Tipo, Coordenação, Rubrica…).
// Os projetos guardam o texto do valor; a lista serve para escolher e manter os valores coerentes.
public class ValorLista
{
    public int Id { get; set; }

    [Display(Name = "Lista")]
    public ListaPlano Lista { get; set; }

    [Required, StringLength(200), Display(Name = "Valor")]
    public string Valor { get; set; } = "";

    [Display(Name = "Ordem")]
    public int Ordem { get; set; }

    // Valores inativos deixam de aparecer para escolha, mas os projetos que já os usam mantêm-nos.
    [Display(Name = "Ativo")]
    public bool Ativo { get; set; } = true;
}
