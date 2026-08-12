using Microsoft.AspNetCore.Mvc;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Services;

namespace ComparacaoPropostas.Controllers;

public class ItensCoreController : Controller
{
    private readonly AppDbContext _db;
    private readonly IPropostaExcelService _excelService;
    private readonly ILogger<ItensCoreController> _logger;

    public ItensCoreController(AppDbContext db, IPropostaExcelService excelService, ILogger<ItensCoreController> logger)
    {
        _db = db;
        _excelService = excelService;
        _logger = logger;
    }

    public IActionResult Index() => View(_db.ItensCore.OrderBy(i => i.Nome).ToList());

    public IActionResult Create() => View(new ItemCore());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(ItemCore item)
    {
        if (!ModelState.IsValid) return View(item);

        _db.ItensCore.Add(item);
        _db.SaveChanges();
        TempData["Sucesso"] = "Item criado com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Edit(int id)
    {
        var item = _db.ItensCore.Find(id);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, ItemCore item)
    {
        if (id != item.Id) return NotFound();
        if (!ModelState.IsValid) return View(item);

        _db.ItensCore.Update(item);
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
            var item = _db.ItensCore.Find(id);
            if (item == null) return NotFound();

            _db.ItensCore.Remove(item);
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

            var existentes = new HashSet<string>(_db.ItensCore.Select(i => i.Nome), StringComparer.OrdinalIgnoreCase);
            var criados = 0;

            foreach (var linha in linhas)
            {
                if (!existentes.Add(linha.NomeItem)) continue;

                _db.ItensCore.Add(new ItemCore
                {
                    Nome = linha.NomeItem,
                    Categoria = linha.Categoria,
                    Unidade = linha.Unidade,
                    Dominio = "Core"
                });
                criados++;
            }

            _db.SaveChanges();
            TempData["Sucesso"] = $"{criados} item(ns) novo(s) importado(s) ({linhas.Count - criados} já existiam e foram ignorados).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao importar catálogo de Item Core.");
            TempData["EmailWarning"] = "Não foi possível ler o ficheiro Excel.";
        }

        return RedirectToAction(nameof(Index));
    }
}
