using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Services;

public class LinhaMqtImportada
{
    public string? CodigoIndexacao { get; set; }
    public string Descricao { get; set; } = "";
    public string? Unidade { get; set; }
    public decimal Quantidade { get; set; }
}

public class LinhaExcelObraImportada
{
    public string NomeItem { get; set; } = "";
    public decimal? QuantidadeFornecida { get; set; }
    public decimal? PrecoUnitario { get; set; }
}

public interface IMqtExcelService
{
    List<LinhaMqtImportada> LerMqtExcel(Stream ficheiro);
    byte[] GerarModeloExcel(string projetoNome, string empreiteiro, IEnumerable<ItemMQT> itens);
    List<LinhaExcelObraImportada> LerRespostaExcel(Stream ficheiro);
}
