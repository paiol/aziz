using Microsoft.AspNetCore.Mvc;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Controllers;

public class ItensCoreController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<ItensCoreController> _logger;

    public ItensCoreController(AppDbContext db, ILogger<ItensCoreController> logger)
    {
        _db = db;
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
}
