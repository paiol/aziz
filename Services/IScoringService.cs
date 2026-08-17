using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.ViewModels.Comparacao;

namespace ComparacaoPropostas.Services;

public interface IScoringService
{
    decimal ObterNotaMediaCriterio(Proposta proposta, int criterioId);
    decimal CalcularNotaMedia(Proposta proposta);
    decimal CalcularPontuacaoPonderada(Proposta proposta);
    decimal CalcularPontuacaoPonderada(Proposta proposta, IEnumerable<Criterio> criterios);
    decimal? MenorValorOfertado(Processo processo);
    ComparacaoViewModel BuildComparacao(int processoId);
    ComparacaoItensViewModel BuildComparacaoItens(int processoId);
    void ClonarItensPedidoParaProposta(Proposta proposta, IEnumerable<ItemPedido> itensPedido);
    void AtualizarAvaliacaoAutomatica(Processo processo);
}
