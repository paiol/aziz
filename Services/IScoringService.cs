using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.ViewModels.Comparacao;

namespace ComparacaoPropostas.Services;

public interface IScoringService
{
    decimal CalcularNotaPonderada(Avaliacao avaliacao);
    decimal CalcularNotaMedia(Proposta proposta);
    decimal CalcularPontuacaoPonderada(Proposta proposta);
    decimal? MenorValorOfertado(Processo processo);
    ComparacaoViewModel BuildComparacao(int processoId);
    ComparacaoItensViewModel BuildComparacaoItens(int processoId);
}
