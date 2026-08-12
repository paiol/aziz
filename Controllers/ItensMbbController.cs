using Microsoft.AspNetCore.Mvc;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Services;

namespace ComparacaoPropostas.Controllers;

public class ItensMbbController : Controller
{
    private readonly AppDbContext _db;
    private readonly IPropostaExcelService _excelService;
    private readonly ILogger<ItensMbbController> _logger;

    public ItensMbbController(AppDbContext db, IPropostaExcelService excelService, ILogger<ItensMbbController> logger)
    {
        _db = db;
        _excelService = excelService;
        _logger = logger;
    }

    public IActionResult Index() => View(_db.ItensMbb.OrderBy(i => i.Nome).ToList());

    public IActionResult Create() => View(new ItemMbb());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(ItemMbb item)
    {
        if (!ModelState.IsValid) return View(item);

        _db.ItensMbb.Add(item);
        _db.SaveChanges();
        TempData["Sucesso"] = "Item criado com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Edit(int id)
    {
        var item = _db.ItensMbb.Find(id);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, ItemMbb item)
    {
        if (id != item.Id) return NotFound();
        if (!ModelState.IsValid) return View(item);

        _db.ItensMbb.Update(item);
        _db.SaveChanges();
        TempData["Sucesso"] = "Item atualizado com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        try
        {
            var item = _db.ItensMbb.Find(id);
            if (item == null) return NotFound();

            _db.ItensMbb.Remove(item);
            _db.SaveChanges();
            TempData["Sucesso"] = "Item removido com sucesso.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover item {Id}.", id);
            TempData["EmailWarning"] = "Não foi possível remover o item.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ImportarExcel(IFormFile ficheiro)
    {
        if (ficheiro == null || ficheiro.Length == 0)
        {
            TempData["EmailWarning"] = "Selecione um ficheiro Excel.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            using var stream = ficheiro.OpenReadStream();
            var linhas = _excelService.LerCatalogoExcel(stream);

            if (linhas.Count == 0)
            {
                TempData["EmailWarning"] = "Não foi possível encontrar uma coluna 'Item' no ficheiro.";
                return RedirectToAction(nameof(Index));
            }

            var existentes = new HashSet<string>(_db.ItensMbb.Select(i => i.Nome), StringComparer.OrdinalIgnoreCase);
            var criados = 0;

            foreach (var linha in linhas)
            {
                if (!existentes.Add(linha.NomeItem)) continue;

                _db.ItensMbb.Add(new ItemMbb
                {
                    Nome = linha.NomeItem,
                    Categoria = linha.Categoria,
                    Unidade = linha.Unidade,
                    Dominio = "MBB"
                });
                criados++;
            }

            _db.SaveChanges();
            TempData["Sucesso"] = $"{criados} item(ns) novo(s) importado(s) ({linhas.Count - criados} já existiam e foram ignorados).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao importar catálogo de Item MBB.");
            TempData["EmailWarning"] = "Não foi possível ler o ficheiro Excel.";
        }

        return RedirectToAction(nameof(Index));
    }
}
