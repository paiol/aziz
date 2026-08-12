using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Services;

public class LinhaExcelImportada
{
    public string NomeItem { get; set; } = "";
    public decimal? QuantidadeFornecida { get; set; }
    public decimal? PrecoUnitario { get; set; }
    public string? Observacao { get; set; }
}

public class LinhaCatalogoImportada
{
    public string NomeItem { get; set; } = "";
    public string? Categoria { get; set; }
    public string? Unidade { get; set; }
}

public interface IPropostaExcelService
{
    byte[] GerarPedidoExcel(string processoNome, string fornecedor, IEnumerable<ItemPedido> itens);
    List<LinhaExcelImportada> LerRespostaExcel(Stream ficheiro);
    List<LinhaCatalogoImportada> LerCatalogoExcel(Stream ficheiro);
}
