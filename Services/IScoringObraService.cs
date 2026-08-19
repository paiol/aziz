using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.ViewModels.ComparacaoObra;

namespace ComparacaoPropostas.Services;

public interface IScoringObraService
{
    decimal CalcularPontuacaoPonderada(PropostaEmpreiteiro proposta, IEnumerable<CriterioObra> criterios);
    ComparacaoObraViewModel BuildComparacao(int projetoObraId);
    void ClonarItensMQTParaProposta(PropostaEmpreiteiro proposta, IEnumerable<ItemMQT> itensMQT);
}
