using System.ComponentModel.DataAnnotations;

namespace ComparacaoPropostas.ViewModels.AvaliacoesObra;

public class AvaliacaoObraFormVM
{
    public int PropostaEmpreiteiroId { get; set; }
    public string Empreiteiro { get; set; } = "";
    public int ProjetoObraId { get; set; }

    [Display(Name = "Avaliador")]
    public string? Avaliador { get; set; }

    public List<ItemAvaliacaoObraVM> Itens { get; set; } = new();
}

public class ItemAvaliacaoObraVM
{
    public int CriterioObraId { get; set; }
    public string CriterioNome { get; set; } = "";
    public decimal Peso { get; set; }

    [Range(1, 5, ErrorMessage = "A nota deve ser entre 1 e 5 estrelas.")]
    public int Nota { get; set; } = 3;

    public string? Comentario { get; set; }
}
