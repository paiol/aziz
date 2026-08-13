using ClosedXML.Excel;
using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Services;

public class PropostaExcelService : IPropostaExcelService
{
    private const string ColItem = "Item";
    private const string ColCategoria = "Categoria";
    private const string ColUnidade = "Unidade";
    private const string ColQtdSolicitada = "Quantidade Solicitada";
    private const string ColQtdFornecida = "Quantidade Fornecida";
    private const string ColPreco = "Preço Unitário";
    private const string ColPrecoTotal = "Preço Total";
    private const string ColObservacao = "Observação";

    public byte[] GerarPedidoExcel(string processoNome, string fornecedor, IEnumerable<ItemPedido> itens)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Pedido de Proposta");

        var headers = new[] { ColItem, ColUnidade, ColQtdSolicitada, ColQtdFornecida, ColPreco, ColPrecoTotal, ColObservacao };

        sheet.Cell(1, 1).Value = $"Pedido de Proposta — {processoNome}";
        sheet.Range(1, 1, 1, headers.Length).Merge();
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;

        sheet.Cell(2, 1).Value = "Fornecedor:";
        sheet.Cell(2, 2).Value = fornecedor;
        sheet.Cell(2, 1).Style.Font.Bold = true;

        var headerRow = 4;
        for (var c = 0; c < headers.Length; c++)
        {
            var cell = sheet.Cell(headerRow, c + 1);
            cell.Value = headers[c];
            cell.Style.Font.Bold = true;
            cell.Style.Fill.BackgroundColor = XLColor.FromArgb(0x21, 0x25, 0x29);
            cell.Style.Font.FontColor = XLColor.White;
        }

        var row = headerRow + 1;
        var grupos = itens
            .GroupBy(i => string.IsNullOrWhiteSpace(i.ItemMaterial.Categoria) ? "Sem categoria" : i.ItemMaterial.Categoria!)
            .OrderBy(g => g.Key);

        foreach (var grupo in grupos)
        {
            var categoriaCell = sheet.Cell(row, 1);
            categoriaCell.Value = grupo.Key;
            sheet.Range(row, 1, row, headers.Length).Merge();
            categoriaCell.Style.Font.Bold = true;
            categoriaCell.Style.Fill.BackgroundColor = XLColor.FromArgb(0xE9, 0xEC, 0xEF);
            row++;

            foreach (var item in grupo.OrderBy(i => i.ItemMaterial.NomeItem))
            {
                sheet.Cell(row, 1).Value = item.ItemMaterial.NomeItem;
                sheet.Cell(row, 2).Value = item.ItemMaterial.Unidade;
                sheet.Cell(row, 3).Value = item.QuantidadeSolicitada;
                sheet.Cell(row, 3).Style.Fill.BackgroundColor = XLColor.FromArgb(0xE9, 0xEC, 0xEF);
                sheet.Cell(row, 3).Style.Protection.Locked = true;
                // Columns 4-5 (Quantidade Fornecida, Preço Unitário) left blank for the supplier to fill in.
                // Column 6 (Preço Total) calculates itself from those two once the supplier fills them in.
                sheet.Cell(row, 6).FormulaA1 = $"=IF(OR(D{row}=\"\",E{row}=\"\"),\"\",D{row}*E{row})";
                sheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
                // Column 7 (Observação) left blank for the supplier.
                row++;
            }
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public List<LinhaExcelImportada> LerRespostaExcel(Stream ficheiro)
    {
        var linhas = new List<LinhaExcelImportada>();

        using var workbook = new XLWorkbook(ficheiro);
        var sheet = workbook.Worksheets.First();

        var headerRow = EncontrarLinhaCabecalho(sheet, ColItem);
        if (headerRow == null) return linhas;

        var colunas = MapearColunas(headerRow);
        if (!colunas.ContainsKey(ColItem)) return linhas;

        foreach (var dataRow in sheet.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber()))
        {
            var nomeItem = colunas.TryGetValue(ColItem, out var colItem) ? dataRow.Cell(colItem).GetString().Trim() : "";
            if (string.IsNullOrWhiteSpace(nomeItem)) continue;

            // Skip the merged "Categoria" section rows GerarPedidoExcel writes between groups —
            // real item rows always carry a Unidade, those section rows never do.
            if (colunas.TryGetValue(ColUnidade, out var colUnidadeCheck) && string.IsNullOrWhiteSpace(dataRow.Cell(colUnidadeCheck).GetString()))
                continue;

            linhas.Add(new LinhaExcelImportada
            {
                NomeItem = nomeItem,
                QuantidadeFornecida = LerDecimal(dataRow, colunas, ColQtdFornecida),
                PrecoUnitario = LerDecimal(dataRow, colunas, ColPreco),
                Observacao = colunas.TryGetValue(ColObservacao, out var colObs) ? dataRow.Cell(colObs).GetString().Trim() : null
            });
        }

        return linhas;
    }

    public List<LinhaCatalogoImportada> LerCatalogoExcel(Stream ficheiro)
    {
        var linhas = new List<LinhaCatalogoImportada>();

        using var workbook = new XLWorkbook(ficheiro);
        // "Necessidade total" is the consolidated item list in the purchase templates this
        // was built against; fall back to the first sheet for any other layout.
        var sheet = workbook.Worksheets.FirstOrDefault(s => s.Name.Trim().Equals("Necessidade total", StringComparison.OrdinalIgnoreCase))
                    ?? workbook.Worksheets.First();

        var headerRow = EncontrarLinhaCabecalho(sheet, ColItem);
        if (headerRow == null) return linhas;

        var colunas = MapearColunas(headerRow);
        if (!colunas.ContainsKey(ColItem)) return linhas;

        var vistos = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var dataRow in sheet.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber()))
        {
            var nomeItem = dataRow.Cell(colunas[ColItem]).GetString().Trim();
            if (string.IsNullOrWhiteSpace(nomeItem)) continue;
            if (!vistos.Add(nomeItem)) continue; // dedupe repeated rows within the same file

            linhas.Add(new LinhaCatalogoImportada
            {
                NomeItem = nomeItem,
                Categoria = colunas.TryGetValue(ColCategoria, out var colCat) ? dataRow.Cell(colCat).GetString().Trim() : null,
                Unidade = colunas.TryGetValue(ColUnidade, out var colUn) ? dataRow.Cell(colUn).GetString().Trim() : null
            });
        }

        return linhas;
    }

    private static IXLRow? EncontrarLinhaCabecalho(IXLWorksheet sheet, string colunaChave)
        => sheet.RowsUsed().FirstOrDefault(r => r.Cells().Any(c => c.GetString().Trim().Equals(colunaChave, StringComparison.OrdinalIgnoreCase)));

    private static Dictionary<string, int> MapearColunas(IXLRow headerRow)
    {
        var colunas = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        foreach (var cell in headerRow.CellsUsed())
        {
            var texto = cell.GetString().Trim();
            if (!string.IsNullOrEmpty(texto)) colunas[texto] = cell.Address.ColumnNumber;
        }
        return colunas;
    }

    private static decimal? LerDecimal(IXLRow row, Dictionary<string, int> colunas, string nomeColuna)
    {
        if (!colunas.TryGetValue(nomeColuna, out var col)) return null;
        var cell = row.Cell(col);
        return cell.TryGetValue(out double valor) ? (decimal)valor : null;
    }
}
