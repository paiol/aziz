using System.Data;
using System.Text;
using System.Text.RegularExpressions;
using DocumentFormat.OpenXml.Packaging;
using ExcelDataReader;
using UglyToad.PdfPig;
using W = DocumentFormat.OpenXml.Wordprocessing;

namespace ComparacaoPropostas.Services.Stock;

// Converte o ficheiro carregado (Excel, Word ou PDF) num DataSet de "folhas" com linhas e colunas,
// para o Packing List e o inventário serem lidos da mesma forma, venha o documento de onde vier.
//  - Excel (.xls/.xlsx): como antes.
//  - Word (.docx): cada tabela do documento é uma folha; o texto antes da tabela fica nas primeiras linhas.
//  - PDF com texto (não digitalizado): as colunas vêm da linha de cabeçalho (ex.: PART NUMBER | DESCRIPTION | QTY);
//    cada palavra das linhas seguintes vai para a coluna por baixo da qual está.
public static class DocumentoTabelas
{
    public static readonly string[] Extensoes = { ".xls", ".xlsx", ".docx", ".pdf" };
    public const string Accept = ".xls,.xlsx,.docx,.pdf";

    public static DataSet Ler(Stream ficheiro, string? nomeFicheiro)
    {
        var ext = Path.GetExtension(nomeFicheiro ?? "").ToLowerInvariant();
        return ext switch
        {
            ".docx" => LerWord(ficheiro),
            ".pdf" => LerPdf(ficheiro),
            ".doc" => throw new NotSupportedException("Ficheiros .doc (Word antigo) não são suportados: guarde como .docx ou PDF."),
            _ => LerExcel(ficheiro)
        };
    }

    private static DataSet LerExcel(Stream ficheiro)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        using var reader = ExcelReaderFactory.CreateReader(ficheiro);
        return reader.AsDataSet();
    }

    // ---- Word ----------------------------------------------------------------------------------

    public static DataSet LerWord(Stream ficheiro)
    {
        var ds = new DataSet();
        using var doc = WordprocessingDocument.Open(ficheiro, false);
        var corpo = doc.MainDocumentPart?.Document.Body;
        if (corpo == null) return ds;

        var textoAntes = new List<string>();
        var n = 0;
        foreach (var el in corpo.ChildElements)
        {
            if (el is W.Paragraph p)
            {
                var t = p.InnerText.Trim();
                if (t.Length > 0) textoAntes.Add(t);
                continue;
            }
            if (el is not W.Table tabela) continue;

            var linhas = new List<List<string>>();
            foreach (var tl in textoAntes) linhas.Add(new List<string> { tl });
            textoAntes.Clear();
            foreach (var tr in tabela.Elements<W.TableRow>())
            {
                var celulas = new List<string>();
                foreach (var tc in tr.Elements<W.TableCell>())
                {
                    celulas.Add(string.Join(" ", tc.Elements<W.Paragraph>().Select(x => x.InnerText.Trim()).Where(x => x.Length > 0)));
                    // Células unidas na horizontal ocupam várias colunas: mantém o alinhamento.
                    var span = tc.TableCellProperties?.GridSpan?.Val?.Value ?? 1;
                    for (var i = 1; i < span; i++) celulas.Add("");
                }
                linhas.Add(celulas);
            }
            ds.Tables.Add(Tabela($"Tabela {++n}", linhas));
        }
        if (ds.Tables.Count == 0 && textoAntes.Count > 0)
            ds.Tables.Add(Tabela("Texto", textoAntes.Select(t => new List<string> { t }).ToList()));
        return ds;
    }

    // ---- PDF -----------------------------------------------------------------------------------

    private record Palavra(string Texto, double Esq, double Dir, double Baixo, double Alto)
    {
        public double Centro => (Esq + Dir) / 2;
        public double Altura => Math.Max(1, Alto - Baixo);
    }

    private record Coluna(double Esq, double Dir)
    {
        public double Distancia(double esq, double dir) => dir < Esq ? Esq - dir : esq > Dir ? esq - Dir : 0;
    }

    // As colunas saem do espaço vazio entre os dados (os números costumam estar alinhados à direita e o
    // cabeçalho à esquerda, por isso não basta a posição do cabeçalho). O texto do cabeçalho — que pode
    // ocupar duas linhas, ex.: "Contagem" / "Total" ou "CASE" / "NO." — vai para a coluna mais próxima.
    public static DataSet LerPdf(Stream ficheiro)
    {
        var ds = new DataSet();
        var saida = new List<List<string>>();
        List<Coluna>? colunas = null;
        List<string>? rotulos = null;

        using var pdf = PdfDocument.Open(ficheiro);
        foreach (var pagina in pdf.GetPages())
        {
            var palavras = pagina.GetWords()
                .Where(w => !string.IsNullOrWhiteSpace(w.Text))
                .Select(w => (w, Texto: Limpar(w.Text)))
                .Where(x => x.Texto.Length > 0)
                // Linha de base do texto (igual para toda a linha, ao contrário do fundo das letras: "p", "ç"...).
                .Select(x => { var w = x.w; var b = w.Letters.Count > 0 ? w.Letters[0].StartBaseLine.Y : w.BoundingBox.Bottom; return new Palavra(x.Texto, w.BoundingBox.Left, w.BoundingBox.Right, b, b + Math.Max(1, w.BoundingBox.Height)); })
                .ToList();
            var linhas = AgruparLinhas(palavras);
            var largura = Math.Max(1, pagina.Width);

            var h = linhas.FindIndex(l => EhCabecalho(Segmentos(l).Select(s => s.Texto)));
            // A tabela acaba na linha "Total..." (o resto da página são totais e rodapé).
            var fim = h < 0 ? -1 : linhas.FindIndex(h + 1, l => l[0].Texto.StartsWith("total", StringComparison.OrdinalIgnoreCase));
            var rodape = fim < 0 ? new List<List<Palavra>>() : linhas.Skip(fim).ToList();
            if (fim >= 0) linhas = linhas.Take(fim).ToList();
            var inicioDados = 0;
            if (h >= 0)
            {
                // Linhas antes do cabeçalho: texto livre (ex.: "Contract No. ...", "Invoice No. ...").
                var anterior = h > 0 && h - 1 >= 0 && PartedoCabecalho(linhas[h - 1], linhas[h], acima: true) ? h - 1 : -1;
                for (var i = 0; i < (anterior >= 0 ? anterior : h); i++) saida.Add(Segmentos(linhas[i]).Select(s => s.Texto).ToList());
                var linhasCab = new List<List<Palavra>>();
                if (anterior >= 0) linhasCab.Add(linhas[anterior]);
                linhasCab.Add(linhas[h]);
                inicioDados = h + 1;
                if (inicioDados < linhas.Count && PartedoCabecalho(linhas[inicioDados], linhas[h], acima: false))
                    linhasCab.Add(linhas[inicioDados++]);

                colunas = ColunasDosDados(linhas.Skip(inicioDados), linhasCab.SelectMany(l => l).ToList(), largura);
                rotulos = Rotulos(linhasCab, ref colunas);
                saida.Add(rotulos.ToList());
            }
            else if (colunas == null)
            {
                foreach (var l in linhas) saida.Add(Segmentos(l).Select(s => s.Texto).ToList());
                continue;
            }

            // Coluna do código: PART NUMBER / Código primeiro; "Material" só se não houver outra (na Huawei é a embalagem).
            var iCodigo = rotulos!.FindIndex(r => EhNomeCodigo(r) && !r.Trim().Equals("material", StringComparison.OrdinalIgnoreCase));
            if (iCodigo < 0) iCodigo = Math.Max(0, rotulos.FindIndex(EhNomeCodigo));
            var continuacaoPossivel = false;
            foreach (var linha in linhas.Skip(inicioDados))
            {
                var celulas = Distribuir(linha, colunas!);
                // Descrição em duas linhas: linha sem código e sem números junta-se à anterior.
                var soTexto = string.IsNullOrWhiteSpace(celulas[iCodigo])
                              && !celulas.Any(c => Regex.IsMatch(c, @"^\d+([.,]\d+)?$"));
                if (soTexto && continuacaoPossivel)
                {
                    var anterior = saida[^1];
                    for (var i = 0; i < celulas.Count && i < anterior.Count; i++)
                        if (celulas[i].Length > 0) anterior[i] = (anterior[i] + " " + celulas[i]).Trim();
                    continue;
                }
                saida.Add(celulas);
                continuacaoPossivel = !string.IsNullOrWhiteSpace(celulas[iCodigo]);
            }
            foreach (var l in rodape) saida.Add(Segmentos(l).Select(s => s.Texto).ToList());
        }
        ds.Tables.Add(Tabela("PDF", saida));
        return ds;
    }

    // Ligaduras tipográficas (ﬁ, ﬂ...) passam a letras normais; caracteres de controlo saem.
    private static string Limpar(string texto) =>
        new string(texto.Replace("FB00", "ff").Replace("FB01", "fi").Replace("FB02", "fl").Replace("FB03", "ffi").Replace("FB04", "ffl")
            .Where(c => !char.IsControl(c)).ToArray()).Trim();

    // Linha logo acima/abaixo do cabeçalho que ainda faz parte dele (cabeçalho em duas linhas).
    private static bool PartedoCabecalho(List<Palavra> linha, List<Palavra> cabecalho, bool acima)
    {
        var alt = cabecalho.Average(p => p.Altura);
        var dist = acima ? linha.Average(p => p.Baixo) - cabecalho.Average(p => p.Baixo) : cabecalho.Average(p => p.Baixo) - linha.Average(p => p.Baixo);
        if (dist <= 0 || dist > alt * 2.2) return false;
        if (linha.Any(p => p.Texto.Any(char.IsDigit))) return false;   // tem números: é dados ou metadados (ex.: "Invoice No. HWI2026...")
        var esq = cabecalho.Min(p => p.Esq) - alt;
        var dir = cabecalho.Max(p => p.Dir) + alt;
        return linha.All(p => p.Esq >= esq && p.Dir <= dir) && linha.Count <= cabecalho.Count;
    }

    // Intervalos horizontais ocupados pelos dados; onde nenhuma linha tem texto há uma separação de colunas.
    // Uma "coluna" com texto em muito poucas linhas (ex.: o fim de uma descrição longa cortada que invade a
    // coluna ao lado) não conta, para não partir a tabela.
    private static List<Coluna> ColunasDosDados(IEnumerable<List<Palavra>> linhas, List<Palavra> cabecalho, double largura)
    {
        var intervalos = new List<(double Esq, double Dir, int Linha)>();
        var comDados = linhas.Where(l => Segmentos(l).Count >= 2).ToList();   // sem frases soltas (rodapé, assinatura)
        // Poucas linhas: as palavras de uma célula (ex.: dois nºs de série) não se cruzam com outras linhas,
        // por isso junta-se primeiro o texto com espaços normais; com muitas linhas basta cada palavra.
        var poucas = comDados.Count < 5;
        var n = 0;
        foreach (var l in comDados)
        {
            n++;
            var seg = Segmentos(l);
            if (poucas)
            {
                foreach (var s in Segmentos(l, 0.45)) intervalos.Add((s.Esq, s.Dir, n));
                continue;
            }
            foreach (var p in l)
                if (seg.First(s => s.Esq <= p.Esq && p.Dir <= s.Dir + 0.01) is var s && s.Dir - s.Esq <= largura * 0.35)
                    intervalos.Add((p.Esq, p.Dir, n));
        }
        // Colunas sem dados (ex.: vazias) ficam com a posição do cabeçalho.
        if (intervalos.Count == 0) intervalos.AddRange(Segmentos(cabecalho).Select(s => (s.Esq, s.Dir, 0)));

        var grupos = new List<(double Esq, double Dir, HashSet<int> Linhas)>();
        foreach (var (esq, dir, linha) in intervalos.OrderBy(i => i.Esq))
        {
            if (grupos.Count > 0 && esq <= grupos[^1].Dir + 0.5)
            {
                grupos[^1].Linhas.Add(linha);
                grupos[^1] = (grupos[^1].Esq, Math.Max(grupos[^1].Dir, dir), grupos[^1].Linhas);
            }
            else grupos.Add((esq, dir, new HashSet<int> { linha }));
        }
        var minimo = n >= 5 ? Math.Max(2, (int)Math.Ceiling(n * 0.1)) : 1;
        return grupos.Where(g => g.Linhas.Count >= minimo).Select(g => new Coluna(g.Esq, g.Dir)).ToList();
    }

    // Nome de cada coluna: o texto do cabeçalho que fica por cima dela. Texto que não fica por cima de
    // nenhuma coluna com dados vai para a mais próxima, se esta ainda não tiver nome (ex.: "Contagem Total"
    // à esquerda dos números alinhados à direita); senão é uma coluna vazia (ex.: "CTNNO.", "DEVICE SN").
    private static List<string> Rotulos(List<List<Palavra>> linhasCab, ref List<Coluna> colunas)
    {
        var cols = colunas.Select(c => (Col: c, Rot: "", PorCima: false)).ToList();
        // Texto do cabeçalho por cima de duas colunas de dados (ex.: "Utiliz.livre Observações" muito juntos): palavra a palavra.
        var segmentos = new List<(string Texto, double Esq, double Dir)>();
        foreach (var l in linhasCab)
            foreach (var s in Segmentos(l))
            {
                var cobre = colunas.Count(c => Math.Min(c.Dir, s.Dir) - Math.Max(c.Esq, s.Esq) > 0);
                if (cobre > 1) segmentos.AddRange(l.Where(p => p.Esq >= s.Esq - 0.01 && p.Dir <= s.Dir + 0.01).Select(p => (p.Texto, p.Esq, p.Dir)));
                else segmentos.Add(s);
            }
        var semColuna = new List<(string Texto, double Esq, double Dir)>();
        foreach (var s in segmentos)
        {
            var (i, sobre) = MaiorSobreposicao(cols.Select(c => c.Col).ToList(), s.Esq, s.Dir);
            if (sobre > 0) cols[i] = (cols[i].Col, Juntar(cols[i].Rot, s.Texto), true);
            else semColuna.Add(s);
        }
        foreach (var s in semColuna)
        {
            var (i, sobre) = MaiorSobreposicao(cols.Select(c => c.Col).ToList(), s.Esq, s.Dir);
            if (sobre > 0) { cols[i] = (cols[i].Col, Juntar(cols[i].Rot, s.Texto), cols[i].PorCima); continue; }   // coluna extra já criada
            var p = new Palavra(s.Texto, s.Esq, s.Dir, 0, 1);
            var j = MaisProxima(cols.Select(c => c.Col).ToList(), p);
            if (cols.Count > 0 && !cols[j].PorCima) cols[j] = (cols[j].Col, Juntar(cols[j].Rot, s.Texto), false);
            else cols.Add((new Coluna(s.Esq, s.Dir), s.Texto, true));
        }
        cols = cols.OrderBy(c => c.Col.Esq).ToList();
        colunas = cols.Select(c => c.Col).ToList();
        return cols.Select(c => c.Rot).ToList();
    }

    private static string Juntar(string a, string b) => a.Length == 0 ? b : a + " " + b;

    private static (int Indice, double Sobreposicao) MaiorSobreposicao(List<Coluna> colunas, double esq, double dir)
    {
        var melhor = (-1, 0.0);
        for (var i = 0; i < colunas.Count; i++)
        {
            var o = Math.Min(colunas[i].Dir, dir) - Math.Max(colunas[i].Esq, esq);
            if (o > melhor.Item2) melhor = (i, o);
        }
        return melhor;
    }

    private static int MaisProxima(List<Coluna> colunas, Palavra p)
    {
        var melhor = 0;
        var dist = double.MaxValue;
        for (var i = 0; i < colunas.Count; i++)
        {
            var d = colunas[i].Distancia(p.Esq, p.Dir);
            if (d == 0) d = -Math.Min(colunas[i].Dir, p.Dir) + Math.Max(colunas[i].Esq, p.Esq);   // maior sobreposição ganha
            if (d < dist) { dist = d; melhor = i; }
        }
        return melhor;
    }

    // Palavras com a mesma altura na página formam uma linha (de cima para baixo, da esquerda para a direita).
    private static List<List<Palavra>> AgruparLinhas(List<Palavra> palavras)
    {
        var linhas = new List<List<Palavra>>();
        foreach (var p in palavras.OrderByDescending(p => p.Baixo).ThenBy(p => p.Esq))
        {
            var linha = linhas.LastOrDefault();
            if (linha != null && Math.Abs(linha.Average(x => x.Baixo) - p.Baixo) <= linha[0].Altura * 0.2)
                linha.Add(p);
            else
                linhas.Add(new List<Palavra> { p });
        }
        return linhas.Select(l => l.OrderBy(p => p.Esq).ToList()).ToList();
    }

    // Junta palavras próximas no mesmo "texto"; um espaço grande separa.
    private static List<(string Texto, double Esq, double Dir)> Segmentos(List<Palavra> linha, double espaco = 0.8)
    {
        var res = new List<(string Texto, double Esq, double Dir)>();
        foreach (var p in linha)
        {
            if (res.Count > 0 && p.Esq - res[^1].Dir < p.Altura * espaco)
                res[^1] = (res[^1].Texto + " " + p.Texto, res[^1].Esq, p.Dir);
            else
                res.Add((p.Texto, p.Esq, p.Dir));
        }
        return res;
    }

    private static readonly string[] NomesCabecalho = PackingListService.ColCodigo
        .Concat(PackingListService.ColDescricao).Concat(PackingListService.ColQuantidade).Concat(PackingListService.ColUnidade)
        .Concat(PackingListService.ColModelo).Concat(PackingListService.ColSerie).Concat(PackingListService.ColCaixa)
        .Concat(new[] { "material", "txtbrevematerial", "contagem", "contagem total", "quantidade contada", "área", "area", "depósito", "deposito", "centro" })
        .ToArray();

    // Uma linha é cabeçalho se tiver um nome de coluna de código e pelo menos mais um nome conhecido
    // (as palavras são vistas uma a uma e também juntas, ex.: "PART NUMBER").
    private static bool EhCabecalho(IEnumerable<string> textos)
    {
        var lista = textos.ToList();
        var palavras = lista.SelectMany(t => t.Split(' ', StringSplitOptions.RemoveEmptyEntries)).ToList();
        var todos = lista.Concat(palavras).ToList();
        return todos.Any(EhNomeCodigo) && todos.Count(Reconhecido) >= 2;
    }

    private static bool Reconhecido(string texto)
    {
        var t = Regex.Replace(texto.ToLowerInvariant(), @"\s+", " ").Trim().TrimEnd(':');
        return NomesCabecalho.Contains(t) || NomesCabecalho.Any(n => n.Length >= 4 && t.StartsWith(n));
    }

    private static bool EhNomeCodigo(string t)
    {
        var n = Regex.Replace(t.ToLowerInvariant(), @"\s+", " ").Trim().TrimEnd(':');
        return PackingListService.ColCodigo.Contains(n) || n == "material" || n.StartsWith("part number");
    }

    // Cada palavra vai para a coluna que a contém (ou a mais próxima).
    private static List<string> Distribuir(List<Palavra> linha, List<Coluna> colunas)
    {
        var celulas = Enumerable.Repeat("", colunas.Count).ToList();
        foreach (var p in linha)
        {
            var i = MaisProxima(colunas, p);
            celulas[i] = celulas[i].Length == 0 ? p.Texto : celulas[i] + " " + p.Texto;
        }
        return celulas;
    }

    private static DataTable Tabela(string nome, List<List<string>> linhas)
    {
        var t = new DataTable(nome);
        var colunas = Math.Max(1, linhas.Select(l => l.Count).DefaultIfEmpty(1).Max());
        for (var c = 0; c < colunas; c++) t.Columns.Add($"C{c}", typeof(object));
        foreach (var l in linhas)
        {
            var row = t.NewRow();
            for (var c = 0; c < l.Count; c++) row[c] = string.IsNullOrEmpty(l[c]) ? DBNull.Value : l[c];
            t.Rows.Add(row);
        }
        return t;
    }
}
