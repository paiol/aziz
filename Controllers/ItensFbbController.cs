using Microsoft.AspNetCore.Mvc;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Controllers;

public class ItensFbbController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<ItensFbbController> _logger;

    public ItensFbbController(AppDbContext db, ILogger<ItensFbbController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public IActionResult Index() => View(_db.ItensFbb.OrderBy(i => i.Nome).ToList());

    public IActionResult Create() => View(new ItemFbb());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(ItemFbb item)
    {
        if (!ModelState.IsValid) return View(item);

        _db.ItensFbb.Add(item);
        _db.SaveChanges();
        TempData["Sucesso"] = "Item criado com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Edit(int id)
    {
        var item = _db.ItensFbb.Find(id);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, ItemFbb item)
    {
        if (id != item.Id) return NotFound();
        if (!ModelState.IsValid) return View(item);

        _db.ItensFbb.Update(item);
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
            var item = _db.ItensFbb.Find(id);
            if (item == null) return NotFound();

            _db.ItensFbb.Remove(item);
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
