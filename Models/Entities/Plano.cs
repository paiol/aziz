using System.ComponentModel.DataAnnotations;

namespace ComparacaoPropostas.Models.Entities;

// Plano anual de um departamento (ex.: "Plano 2027 DEP-INF"), com base no ficheiro SCOPE_INF.
public class Plano
{
    public int Id { get; set; }

    [Required, Range(2000, 2100), Display(Name = "Ano")]
    public int Ano { get; set; } = DateTime.Today.Year + 1;

    [Required, Display(Name = "Designação")]
    public string Designacao { get; set; } = "";

    [Display(Name = "Departamento")]
    public string? Departamento { get; set; } = "DEI";

    [Display(Name = "Descrição")]
    public string? Descricao { get; set; }

    // Dias-pessoa disponíveis para projetos por ano (folha "Parâmetros plano", linha 12).
    [Display(Name = "Capacidade Arquitetura (dias/ano)")]
    public decimal? CapacidadeArquiteturaDias { get; set; }

    [Display(Name = "Capacidade Energia (dias/ano)")]
    public decimal? CapacidadeEnergiaDias { get; set; }

    [Display(Name = "Criado por")]
    public string? CriadoPor { get; set; }

    public DateTime CriadoEm { get; set; } = DateTime.UtcNow;

    [Display(Name = "Estado")]
    public Enums.EstadoPlano Estado { get; set; } = Enums.EstadoPlano.EmPreparacao;

    [Display(Name = "Concluído em")]
    public DateTime? ConcluidoEm { get; set; }

    public ICollection<ProjetoPlano> Projetos { get; set; } = new List<ProjetoPlano>();
    public ICollection<AcaoFormacaoPlano> AcoesFormacao { get; set; } = new List<AcaoFormacaoPlano>();
}
