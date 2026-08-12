using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Services;

public class ImportExcelResultado
{
    public int Atualizados { get; set; }
    public int Criados { get; set; }
    public List<string> NaoReconhecidos { get; set; } = new();
}

public interface IPropostaExcelService
{
    byte[] GerarPedidoExcel(Proposta proposta);
    ImportExcelResultado ImportarPropostaExcel(Proposta proposta, Stream ficheiro);
}
