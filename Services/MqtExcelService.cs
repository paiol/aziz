using ClosedXML.Excel;
using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Services;

public class MqtExcelService : IMqtExcelService
{
    private const string ColCodigo = "Código";
    private const string ColDescricao = "Descrição";
    private const string ColUnidade = "Unidade";
    private const string ColQtd = "Quantidade";
    private const string ColQtdFornecida = "Quantidade Fornecida";
    private const string ColPreco = "Preço Unitário";
    private const string ColPrecoTotal = "Preço Total";

    // Um Mapa de Quantidades real vem de clientes/consultores, com cabeçalhos que variam
    // (Código/Cód./Item Nº, Descrição/Designação/Descrição do Trabalho, Un./Unid., Qtd/Quant.).
    // Cada coluna aceita várias variantes de nome, tentadas por ordem.
    private static readonly string[] SinonimosCodigo = { "Código", "Cod.", "Cód.", "Código de Indexação", "Item Nº", "Item N.º", "Nº" };
    private static readonly string[] SinonimosDescricao = { "Descrição", "Designação", "Descrição do Trabalho", "Atividade", "Item", "Descrição dos Trabalhos" };
    private static readonly string[] SinonimosUnidade = { "Unidade", "Un.", "Unid.", "Un" };
    private static readonly string[] SinonimosQuantidade = { "Quantidade", "Qtd", "Qtd.", "Quant.", "Quant" };

    public List<LinhaMqtImportada> LerMqtExcel(Stream ficheiro)
    {
        var linhas = new List<LinhaMqtImportada>();

        using var workbook = new XLWorkbook(ficheiro);
        var sheet = workbook.Worksheets.First();

        var headerRow = EncontrarLinhaCabecalho(sheet, SinonimosDescricao);
        if (headerRow == null) return linhas;

        var colDescricao = EncontrarColuna(headerRow, SinonimosDescricao);
        if (colDescricao == null) return linhas;

        var colCodigo = EncontrarColuna(headerRow, SinonimosCodigo);
        var colUnidade = EncontrarColuna(headerRow, SinonimosUnidade);
        var colQtd = EncontrarColuna(headerRow, SinonimosQuantidade);

        foreach (var dataRow in sheet.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber()))
        {
            var descricao = dataRow.Cell(colDescricao.Value).GetString().Trim();
            if (string.IsNullOrWhiteSpace(descricao)) continue;

            linhas.Add(new LinhaMqtImportada
            {
                CodigoIndexacao = colCodigo.HasValue ? dataRow.Cell(colCodigo.Value).GetString().Trim() : null,
                Descricao = descricao,
                Unidade = colUnidade.HasValue ? dataRow.Cell(colUnidade.Value).GetString().Trim() : null,
                Quantidade = colQtd.HasValue && dataRow.Cell(colQtd.Value).TryGetValue(out double q) ? (decimal)q : 0m
            });
        }

        return linhas;
    }

    public byte[] GerarModeloExcel(string projetoNome, string empreiteiro, IEnumerable<ItemMQT> itens)
    {
        using var workbook = new XLWorkbook();
        var sheet = workbook.Worksheets.Add("Mapa de Quantidades");

        var headers = new[] { ColCodigo, ColDescricao, ColUnidade, ColQtd, ColQtdFornecida, ColPreco, ColPrecoTotal };

        sheet.Cell(1, 1).Value = $"Mapa de Quantidades — {projetoNome}";
        sheet.Range(1, 1, 1, headers.Length).Merge();
        sheet.Cell(1, 1).Style.Font.Bold = true;
        sheet.Cell(1, 1).Style.Font.FontSize = 14;

        sheet.Cell(2, 1).Value = "Empreiteiro:";
        sheet.Cell(2, 2).Value = empreiteiro;
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
        foreach (var item in itens.OrderBy(i => i.CodigoIndexacao).ThenBy(i => i.Descricao))
        {
            sheet.Cell(row, 1).Value = item.CodigoIndexacao;
            sheet.Cell(row, 2).Value = item.Descricao;
            sheet.Cell(row, 3).Value = item.Unidade;
            sheet.Cell(row, 4).Value = item.Quantidade;
            sheet.Cell(row, 4).Style.Fill.BackgroundColor = XLColor.FromArgb(0xE9, 0xEC, 0xEF);
            sheet.Cell(row, 4).Style.Protection.Locked = true;
            // Colunas 5-6 (Quantidade Fornecida, Preço Unitário) em branco para o empreiteiro preencher.
            sheet.Cell(row, 7).FormulaA1 = $"=IF(OR(E{row}=\"\",F{row}=\"\"),\"\",E{row}*F{row})";
            sheet.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
            row++;
        }

        sheet.Columns().AdjustToContents();

        using var stream = new MemoryStream();
        workbook.SaveAs(stream);
        return stream.ToArray();
    }

    public List<LinhaExcelObraImportada> LerRespostaExcel(Stream ficheiro)
    {
        var linhas = new List<LinhaExcelObraImportada>();

        using var workbook = new XLWorkbook(ficheiro);
        var sheet = workbook.Worksheets.First();

        var headerRow = EncontrarLinhaCabecalho(sheet, SinonimosDescricao);
        if (headerRow == null) return linhas;

        var colDescricao = EncontrarColuna(headerRow, SinonimosDescricao);
        if (colDescricao == null) return linhas;

        var colQtdFornecida = EncontrarColuna(headerRow, new[] { ColQtdFornecida });
        var colPreco = EncontrarColuna(headerRow, new[] { ColPreco });

        foreach (var dataRow in sheet.RowsUsed().Where(r => r.RowNumber() > headerRow.RowNumber()))
        {
            var descricao = dataRow.Cell(colDescricao.Value).GetString().Trim();
            if (string.IsNullOrWhiteSpace(descricao)) continue;

            linhas.Add(new LinhaExcelObraImportada
            {
                NomeItem = descricao,
                QuantidadeFornecida = colQtdFornecida.HasValue && dataRow.Cell(colQtdFornecida.Value).TryGetValue(out double q) ? (decimal)q : null,
                PrecoUnitario = colPreco.HasValue && dataRow.Cell(colPreco.Value).TryGetValue(out double p) ? (decimal)p : null
            });
        }

        return linhas;
    }

    private static IXLRow? EncontrarLinhaCabecalho(IXLWorksheet sheet, string[] sinonimosChave)
        => sheet.RowsUsed().FirstOrDefault(r => r.Cells().Any(c => sinonimosChave.Any(s => c.GetString().Trim().Equals(s, StringComparison.OrdinalIgnoreCase))));

    private static int? EncontrarColuna(IXLRow headerRow, string[] sinonimos)
    {
        foreach (var cell in headerRow.CellsUsed())
        {
            var texto = cell.GetString().Trim();
            if (sinonimos.Any(s => texto.Equals(s, StringComparison.OrdinalIgnoreCase)))
                return cell.Address.ColumnNumber;
        }
        return null;
    }
}
