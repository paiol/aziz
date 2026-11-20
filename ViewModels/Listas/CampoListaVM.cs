namespace ComparacaoPropostas.ViewModels.Listas;

// Um campo de escolha a partir de uma lista do plano, com opção "+ Adicionar novo…".
public class CampoListaVM
{
    public const string OpcaoNovo = ComparacaoPropostas.Services.ListasPlano.OpcaoNovo;

    public string Campo { get; set; } = "";
    public string Label { get; set; } = "";
    public string? Atual { get; set; }
    public List<string> Valores { get; set; } = new();
    public bool Multipla { get; set; }
}
