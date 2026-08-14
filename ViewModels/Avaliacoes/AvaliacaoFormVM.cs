using System.ComponentModel.DataAnnotations;
using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.ViewModels.Avaliacoes;

public class AvaliacaoFormVM
{
    public int PropostaId { get; set; }
    public string PropostaFornecedor { get; set; } = "";
    public int ProcessoId { get; set; }

    [Display(Name = "Avaliador")]
    public int? AvaliadorId { get; set; }

    // Campos para criar um novo avaliador rapidamente
    [Display(Name = "Nome do Novo Avaliador")]
    public string? NomeNovoAvaliador { get; set; }

    [Display(Name = "Perfil / Cargo")]
    public string? PerfilNovoAvaliador { get; set; }

    [Display(Name = "E-mail")]
    public string? EmailNovoAvaliador { get; set; }

    public List<Avaliador> AvaliadoresDisponiveis { get; set; } = new();
    public List<ItemAvaliacaoVM> Itens { get; set; } = new();
    public List<HistoricoAvaliacaoVM> OutrasAvaliacoes { get; set; } = new();
}

public class ItemAvaliacaoVM
{
    public int CriterioId { get; set; }
    public string CriterioNome { get; set; } = "";
    public decimal Peso { get; set; }

    [Range(1, 5, ErrorMessage = "A nota deve ser entre 1 e 5 estrelas.")]
    public int Nota { get; set; } = 3; // Padrão 3 estrelas

    public string? Comentario { get; set; }
}

public class HistoricoAvaliacaoVM
{
    public string AvaliadorNome { get; set; } = "";
    public string? Perfil { get; set; }
    public DateTime AvaliadoEm { get; set; }
    public decimal PontuacaoPonderada { get; set; }
    public Dictionary<string, int> NotasPorCriterio { get; set; } = new();
}
