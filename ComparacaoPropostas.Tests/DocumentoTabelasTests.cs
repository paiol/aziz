using System.IO;
using System.Linq;
using ComparacaoPropostas.Services.Stock;
using DocumentFormat.OpenXml;
using DocumentFormat.OpenXml.Packaging;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using Xunit;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ComparacaoPropostas.Tests;

// Packing List / inventário em Word e PDF: o documento passa a tabela e é lido como o Excel.
public class DocumentoTabelasTests
{
    private static readonly string[] Cabecalho = { "PART NUMBER", "DESCRIPTION", "QTY", "UOM", "HW SERIAL NO." };
    private static readonly string[][] Linhas =
    {
        new[] { "02312TCX", "Rectifier Module, R4850, 1U, 3000W", "2", "PCS", "2102312TCX001 2102312TCX002" },
        new[] { "25030429", "Power Cable,450V/750V,16mm^2,Yellow/Green", "80", "M", "" },
        new[] { "01076667", "Energy Storage Module,ESM-48150B1, 48V, 150Ah", "1", "PCS", "LS2639203787" }
    };

    private static MemoryStream Word()
    {
        var ms = new MemoryStream();
        using (var doc = WordprocessingDocument.Create(ms, WordprocessingDocumentType.Document, true))
        {
            var main = doc.AddMainDocumentPart();
            var corpo = new W.Body();
            W.Paragraph P(string t) => new(new W.Run(new W.Text(t)));
            corpo.Append(P("PACKING LIST"), P("P/L No. PL-TESTE-001"), P("Contract No. 0001322602230A"));
            var tabela = new W.Table();
            foreach (var l in new[] { Cabecalho }.Concat(Linhas))
                tabela.Append(new W.TableRow(l.Select(c => new W.TableCell(P(c)))));
            corpo.Append(tabela);
            main.Document = new W.Document(corpo);
        }
        ms.Position = 0;
        return ms;
    }

    private static MemoryStream Pdf()
    {
        QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;
        var bytes = Document.Create(c => c.Page(p =>
        {
            p.Size(PageSizes.A4.Landscape());
            p.Margin(30);
            p.DefaultTextStyle(t => t.FontSize(8));
            p.Content().Column(col =>
            {
                col.Item().Text("PACKING LIST");
                col.Item().Text("P/L No. PL-TESTE-001");
                col.Item().Text("Contract No. 0001322602230A");
                col.Item().PaddingTop(10).Table(t =>
                {
                    t.ColumnsDefinition(cd => { cd.ConstantColumn(80); cd.RelativeColumn(3); cd.ConstantColumn(40); cd.ConstantColumn(40); cd.RelativeColumn(2); });
                    foreach (var h in Cabecalho) t.Cell().Padding(2).Text(h).Bold();
                    foreach (var l in Linhas)
                        for (var i = 0; i < l.Length; i++)
                        {
                            // A quantidade alinhada à direita (como no Excel): não fica por baixo do texto "QTY".
                            var cel = t.Cell().Padding(2);
                            if (i == 2) cel.AlignRight().Text(l[i]); else cel.Text(l[i]).DisableFontFeature(QuestPDF.Helpers.FontFeatures.StandardLigatures);
                        }
                });
            });
        })).GeneratePdf();
        return new MemoryStream(bytes);
    }

    [Theory]
    [InlineData("pl.docx")]
    [InlineData("pl.pdf")]
    public void PackingListEmWordOuPdf(string ficheiro)
    {
        using var s = ficheiro.EndsWith(".pdf") ? Pdf() : Word();
        var r = new PackingListService().Ler(s, ficheiro);

        Assert.Empty(r.Avisos);
        Assert.Equal("PL-TESTE-001", r.Numero);
        Assert.Equal("0001322602230A", r.Contrato);
        Assert.Equal(new[] { "02312TCX", "25030429", "01076667" }, r.Linhas.Select(l => l.Codigo).ToArray());
        Assert.Equal(new[] { 2m, 80m, 1m }, r.Linhas.Select(l => l.Quantidade).ToArray());
        Assert.Equal("M", r.Linhas[1].Unidade);
        Assert.StartsWith("Rectifier Module, R4850", r.Linhas[0].Descricao);
        Assert.Equal(3, r.Series.Count);
    }

    [Fact]
    public void DocAntigoNaoESuportado()
    {
        var ex = Assert.Throws<System.NotSupportedException>(() => DocumentoTabelas.Ler(new MemoryStream(new byte[10]), "guia.doc"));
        Assert.Contains(".docx", ex.Message);
    }
}
