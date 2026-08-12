using ClosedXML.Excel;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Services;

public class PropostaExcelService : IPropostaExcelService
{
    private const string ColItem = "Item";
    private const string ColUnidade = "Unidade";
    private const string ColQtdSolicitada = "Quantidade Solicitada";
    private const string ColQtdFornecida = "Quantidade Fornecida";
    private const string ColPreco = "Preço Unitário";
    private const string ColObservacao = "Observação";

    private readonly AppDbContext _db;

    public PropostaExcelService(AppDbContext db)
    {
        _db = db;
    }

    public byte[] GerarPedidoExcel(Proposta proposta)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Pedido de Proposta");

        sheet.Cell(1, 1).Value = $"Pedido de Proposta — {proposta.Processo.Nome}";
        sheet.Range(1, 1, 1, 6).Merge();
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;

        sheet.Cell(2, 1).Value = "Fornecedor:";
        sheet.Cell(2, 2).Value = proposta.Fornecedor;
        sheet.Cell(2, 1).Style.Font.Bold = true;

        var headerRow = 4;
        var headers = new[] { ColItem, ColUnidade, ColQtdSolicitada, ColQtdFornecida, ColPreco, ColObservacao };
        for (var c = 0; c < headers.Length; c++)
        {
            var cell = sheet.Cell(headerRow, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0x21, 0x25, 0x29);
            cell.Style.Font.FontColor = XLColor.White;
        }

        var row = headerRow + 1;
        foreach (var item in proposta.ItensProposta.OrderBy(i => i.ItemMaterial.NomeItem))
        {
            sheet.Cell(row, 1).Value = item.ItemMaterial.NomeItem;
            sheet.Cell(row, 2).Value = item.ItemMaterial.Unidade;
            sheet.Cell(row, 3).Value = item.QuantidadeSolicitada ?? item.Quantidade;
            sheet.Cell(row, 3).Style.Fill.BackgroundColor = XLColor.FromArgb(0xE9, 0xEC, 0xEF);
            sheet.Cell(row, 3).Style.Protection.Locked = true;
            // Columns 4-6 (Quantidade Fornecida, Preço Unitário, Observação) left blank for the supplier to fill in.
            row++;
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public ImportExcelResultado ImportarPropostaExcel(Proposta proposta, Stream ficheiro)
    {
        var resultado = new ImportExcelResultado();

        using var workbook = new XLWorkbook(ficheiro);
        var sheet = workbook.Worksheets.First();

        var headerRow = sheet.RowsUsed().FirstOrDefault(r => r.Cells().Any(c => c.GetString().Trim() == ColItem));
        if (headerRow == null) return resultado;

        var colunas = new Dictionary<string, int>();
        foreach (var cell in headerRow.CellsUsed())
        {
            var texto = cell.GetString().Trim();
            if (!string.IsNullOrEmpty(texto)) colunas[texto] = cell.Address.ColumnNumber;
        }

        if (!colunas.ContainsKey(ColItem)) return resultado;

        // GroupBy+First (not ToDictionary) because a Proposta can legitimately have more than
        // one ItemProposta row for the same ItemMaterial; name-based matching just takes the first.
        var itensPorNome = proposta.ItensProposta
            .GroupBy(i => i.ItemMaterial.NomeItem.Trim(), StringComparer.OrdinalIgnoreCase)
            .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

        foreach (var dataRow in sheet.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber()))
        {
            var nomeItem = colunas.TryGetValue(ColItem, out var colItem) ? dataRow.Cell(colItem).GetString().Trim() : "";
            if (string.IsNullOrWhiteSpace(nomeItem)) continue;

            var qtdFornecida = LerDecimal(dataRow, colunas, ColQtdFornecida);
            var preco = LerDecimal(dataRow, colunas, ColPreco);
            var observacao = colunas.TryGetValue(ColObservacao, out var colObs) ? dataRow.Cell(colObs).GetString().Trim() : null;

            if (itensPorNome.TryGetValue(nomeItem, out var existente))
            {
                existente.Quantidade = qtdFornecida ?? existente.Quantidade;
                existente.PrecoUnitario = preco ?? existente.PrecoUnitario;
                existente.Observacao = string.IsNullOrWhiteSpace(observacao) ? existente.Observacao : observacao;
                existente.Incluido = qtdFornecida is > 0 || preco is > 0;
                resultado.Atualizados++;
                continue;
            }

            var itemCatalogo = _db.ItensMaterial.FirstOrDefault(im => im.NomeItem == nomeItem);
            if (itemCatalogo == null)
            {
                resultado.NaoReconhecidos.Add(nomeItem);
                continue;
            }

            _db.ItensProposta.Add(new ItemProposta
            {
                PropostaId = proposta.Id,
                ItemMaterialId = itemCatalogo.Id,
                Quantidade = qtdFornecida ?? 0,
                PrecoUnitario = preco ?? 0,
                Observacao = observacao,
                Incluido = qtdFornecida is > 0 || preco is > 0
            });
            resultado.Criados++;
        }

        _db.SaveChanges();
        return resultado;
    }

    private static decimal? LerDecimal(IXLRow row, Dictionary<string, int> colunas, string nomeColuna)
    {
        if (!colunas.TryGetValue(nomeColuna, out var col)) return null;
        var cell = row.Cell(col);
        return cell.TryGetValue(out double valor) ? (decimal)valor : null;
    }
}
