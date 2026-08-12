using Microsoft.AspNetCore.Mvc;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Controllers;

public class ItensMbbController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<ItensMbbController> _logger;

    public ItensMbbController(AppDbContext db, ILogger<ItensMbbController> logger)
    {
        _db = db;
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
}
