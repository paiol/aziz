using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Services.Stock;

namespace ComparacaoPropostas.Controllers;

// Inventário do armazém (contagem física por área). Enquanto está aberto, as contagens podem vir
// do Excel do armazém ou ser escritas na plataforma; ao fechar, o stock da área passa a ser o contado.
public class InventarioController : Controller
{
    private readonly AppDbContext _db;
    private readonly IInventarioExcelService _excel;
    private readonly ILogger<InventarioController> _logger;

    public InventarioController(AppDbContext db, IInventarioExcelService excel, ILogger<InventarioController> logger)
    {
        _db = db;
        _excel = excel;
        _logger = logger;
    }

    public IActionResult Index()
    {
        var inventarios = _db.InventariosStock.Include(i => i.Linhas).OrderByDescending(i => i.Data).ThenByDescending(i => i.Id).AsNoTracking().ToList();
        return View(inventarios);
    }

    public IActionResult Create() => View(new InventarioStock());

    // comStockAtual: começa com uma linha por material com saldo na área (para contar por cima).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(InventarioStock inventario, bool comStockAtual)
    {
        if (!ModelState.IsValid) return View(inventario);

        if (comStockAtual)
        {
            foreach (var m in _db.MateriaisStock.Include(x => x.Movimentos).AsNoTracking().ToList()
                         .Where(m => StockCalculos.Saldo(m.Movimentos, inventario.Area) != 0).OrderBy(m => m.Descricao))
            {
                inventario.Linhas.Add(new LinhaInventario { MaterialStockId = m.Id, Codigo = m.Codigo, Descricao = m.Descricao, Unidade = m.Unidade });
            }
        }
        _db.InventariosStock.Add(inventario);
        _db.SaveChanges();

        TempData["Sucesso"] = comStockAtual
            ? $"Inventário criado com {inventario.Linhas.Count} material(is) em stock na área. Preencha as quantidades contadas."
            : "Inventário criado. Importe o Excel da contagem ou acrescente as linhas.";
        return RedirectToAction(nameof(Details), new { id = inventario.Id });
    }

    public IActionResult Details(int id)
    {
        var inventario = _db.InventariosStock.Include(i => i.Linhas).AsNoTracking().FirstOrDefault(i => i.Id == id);
        if (inventario == null) return NotFound();

        // Saldo atual da plataforma por código (só interessa enquanto está aberto; fechado usa o valor guardado).
        var codigos = inventario.Linhas.Select(l => l.Codigo).ToList();
        ViewBag.Saldos = _db.MateriaisStock.Where(m => codigos.Contains(m.Codigo)).Include(m => m.Movimentos).AsNoTracking().ToList()
            .ToDictionary(m => m.Codigo, m => StockCalculos.Saldo(m.Movimentos, inventario.Area), StringComparer.OrdinalIgnoreCase);
        return View(inventario);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Importar(int id, IFormFile? ficheiro)
    {
        var inventario = CarregarAberto(id, out var erro);
        if (inventario == null) return erro!;

        if (ficheiro == null || ficheiro.Length == 0)
        {
            TempData["EmailWarning"] = "Escolha o ficheiro Excel do inventário.";
            return RedirectToAction(nameof(Details), new { id });
        }

        List<LinhaInventarioImportada> linhas;
        List<string> avisos;
        try
        {
            using var stream = ficheiro.OpenReadStream();
            linhas = _excel.Ler(stream, out avisos);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ler o inventário {Ficheiro}.", ficheiro.FileName);
            TempData["EmailWarning"] = "Não foi possível ler o ficheiro. Confirme que é um Excel (.xls ou .xlsx).";
            return RedirectToAction(nameof(Details), new { id });
        }
        if (linhas.Count == 0)
        {
            TempData["EmailWarning"] = "O ficheiro não tem linhas de inventário. " + string.Join(" ", avisos);
            return RedirectToAction(nameof(Details), new { id });
        }

        var codigos = linhas.Select(l => l.Codigo).ToList();
        var materiais = _db.MateriaisStock.Where(m => codigos.Contains(m.Codigo)).ToList().ToDictionary(m => m.Codigo, StringComparer.OrdinalIgnoreCase);
        int novas = 0, atualizadas = 0;
        foreach (var l in linhas)
        {
            var existente = inventario.Linhas.FirstOrDefault(x => x.Codigo.Equals(l.Codigo, StringComparison.OrdinalIgnoreCase));
            if (existente == null)
            {
                existente = new LinhaInventario { Codigo = Limitar(l.Codigo, 60)! };
                inventario.Linhas.Add(existente);
                novas++;
            }
            else atualizadas++;

            materiais.TryGetValue(l.Codigo, out var material);
            existente.MaterialStockId = material?.Id ?? existente.MaterialStockId;
            existente.Descricao ??= l.Descricao ?? material?.Descricao;
            existente.Unidade ??= Limitar(l.Unidade, 20) ?? material?.Unidade;
            if (l.Quantidade != null) existente.QuantidadeContada = l.Quantidade;
            if (l.Series.Count > 0)
                existente.Series = string.Join(" ", InventarioCalculos.SepararSeries(existente.Series).Concat(l.Series).Distinct(StringComparer.OrdinalIgnoreCase));
        }
        inventario.NomeFicheiro = Limitar(Path.GetFileName(ficheiro.FileName), 200);
        _db.SaveChanges();

        TempData["Sucesso"] = $"Inventário importado: {novas} linha(s) nova(s), {atualizadas} atualizada(s).";
        return RedirectToAction(nameof(Details), new { id });
    }

    // Grava as quantidades contadas escritas na página (campos contado_<idLinha> e series_<idLinha>).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult GuardarContagem(int id, IFormCollection form)
    {
        var inventario = CarregarAberto(id, out var erro);
        if (inventario == null) return erro!;

        foreach (var l in inventario.Linhas)
        {
            if (form.TryGetValue($"contado_{l.Id}", out var v))
            {
                var texto = v.ToString().Trim();
                l.QuantidadeContada = texto.Length == 0 ? null
                    : decimal.TryParse(texto, System.Globalization.NumberStyles.Number, new System.Globalization.CultureInfo("pt-PT"), out var d) ? d : l.QuantidadeContada;
            }
            if (form.TryGetValue($"series_{l.Id}", out var s)) l.Series = string.IsNullOrWhiteSpace(s) ? null : s.ToString().Trim();
        }
        _db.SaveChanges();
        TempData["Sucesso"] = "Contagem guardada.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AdicionarLinha(int id, string? codigo, string? descricao, string? unidade, decimal? contado)
    {
        var inventario = CarregarAberto(id, out var erro);
        if (inventario == null) return erro!;

        codigo = codigo?.Trim();
        if (string.IsNullOrEmpty(codigo))
        {
            TempData["EmailWarning"] = "Indique o código do material.";
        }
        else if (inventario.Linhas.Any(l => l.Codigo.Equals(codigo, StringComparison.OrdinalIgnoreCase)))
        {
            TempData["EmailWarning"] = $"O código {codigo} já está no inventário.";
        }
        else
        {
            var material = _db.MateriaisStock.FirstOrDefault(m => m.Codigo == codigo);
            inventario.Linhas.Add(new LinhaInventario
            {
                Codigo = Limitar(codigo, 60)!,
                MaterialStockId = material?.Id,
                Descricao = string.IsNullOrWhiteSpace(descricao) ? material?.Descricao : descricao.Trim(),
                Unidade = string.IsNullOrWhiteSpace(unidade) ? material?.Unidade : Limitar(unidade.Trim(), 20),
                QuantidadeContada = contado
            });
            _db.SaveChanges();
            TempData["Sucesso"] = $"{codigo} acrescentado.";
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult RemoverLinha(int id, int linhaId)
    {
        var inventario = CarregarAberto(id, out var erro);
        if (inventario == null) return erro!;
        var linha = inventario.Linhas.FirstOrDefault(l => l.Id == linhaId);
        if (linha != null)
        {
            _db.LinhasInventario.Remove(linha);
            _db.SaveChanges();
        }
        return RedirectToAction(nameof(Details), new { id });
    }

    // Fecha o inventário: o saldo da área passa a ser o contado (ajustes) e os nºs de série contados ficam em armazém.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Fechar(int id)
    {
        var inventario = CarregarAberto(id, out var erro);
        if (inventario == null) return erro!;

        var contadas = inventario.Linhas.Where(l => l.QuantidadeContada != null).ToList();
        if (contadas.Count == 0)
        {
            TempData["EmailWarning"] = "Não há quantidades contadas: preencha ou importe a contagem antes de fechar.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var codigos = contadas.Select(l => l.Codigo).ToList();
        var materiais = _db.MateriaisStock.Include(m => m.Movimentos).Include(m => m.NumerosSerie)
            .Where(m => codigos.Contains(m.Codigo)).ToList().ToDictionary(m => m.Codigo, StringComparer.OrdinalIgnoreCase);

        int ajustes = 0, novosMateriais = 0, novasSeries = 0;
        foreach (var l in contadas)
        {
            if (!materiais.TryGetValue(l.Codigo, out var material))
            {
                material = new MaterialStock { Codigo = l.Codigo, Descricao = l.Descricao ?? l.Codigo, Unidade = l.Unidade };
                _db.MateriaisStock.Add(material);
                materiais[l.Codigo] = material;
                novosMateriais++;
            }
            l.Material = material;

            var saldo = StockCalculos.Saldo(material.Movimentos, inventario.Area);
            l.QuantidadeSistema = saldo;
            if (InventarioCalculos.Ajuste(l.QuantidadeContada, saldo) is decimal ajuste)
            {
                _db.MovimentosStock.Add(new MovimentoStock
                {
                    Material = material,
                    Area = inventario.Area,
                    Tipo = TipoMovimentoStock.Ajuste,
                    Quantidade = ajuste,
                    Data = inventario.Data,
                    Inventario = inventario,
                    Observacoes = $"Inventário {inventario.Data:dd/MM/yyyy}{(inventario.Localizacao != null ? " · " + inventario.Localizacao : "")}"
                });
                ajustes++;
            }

            foreach (var serie in InventarioCalculos.SepararSeries(l.Series).Where(s => s.Length <= 80))
            {
                var existente = material.NumerosSerie.FirstOrDefault(s => s.Serie.Equals(serie, StringComparison.OrdinalIgnoreCase));
                if (existente == null)
                {
                    material.NumerosSerie.Add(new NumeroSerieStock { Serie = serie, Area = inventario.Area, Estado = EstadoNumeroSerie.EmArmazem });
                    novasSeries++;
                }
                else if (existente.Estado == EstadoNumeroSerie.EmArmazem && existente.Area != inventario.Area)
                {
                    existente.Area = inventario.Area; // o equipamento foi encontrado nesta área
                }
            }
        }

        inventario.Estado = EstadoInventario.Fechado;
        inventario.FechadoEm = DateTime.UtcNow;
        _db.SaveChanges();

        TempData["Sucesso"] = $"Inventário fechado: {ajustes} ajuste(s) de stock, {novosMateriais} material(is) novo(s), {novasSeries} nº(s) de série registado(s).";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var inventario = _db.InventariosStock.Find(id);
        if (inventario == null) return NotFound();
        if (inventario.Estado == EstadoInventario.Fechado)
        {
            TempData["EmailWarning"] = "Um inventário fechado não pode ser apagado (já acertou o stock).";
            return RedirectToAction(nameof(Details), new { id });
        }
        _db.InventariosStock.Remove(inventario);
        _db.SaveChanges();
        TempData["Sucesso"] = "Inventário apagado.";
        return RedirectToAction(nameof(Index));
    }

    private InventarioStock? CarregarAberto(int id, out IActionResult? erro)
    {
        erro = null;
        var inventario = _db.InventariosStock.Include(i => i.Linhas).FirstOrDefault(i => i.Id == id);
        if (inventario == null)
        {
            erro = NotFound();
            return null;
        }
        if (inventario.Estado == EstadoInventario.Fechado)
        {
            TempData["EmailWarning"] = "O inventário já está fechado.";
            erro = RedirectToAction(nameof(Details), new { id });
            return null;
        }
        return inventario;
    }

    private static string? Limitar(string? s, int max) => s == null ? null : s.Length > max ? s[..max] : s;
}
