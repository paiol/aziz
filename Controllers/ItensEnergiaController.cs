using Microsoft.AspNetCore.Mvc;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Controllers;

public class ItensEnergiaController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<ItensEnergiaController> _logger;

    public ItensEnergiaController(AppDbContext db, ILogger<ItensEnergiaController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public IActionResult Index() => View(_db.ItensEnergia.OrderBy(i => i.Nome).ToList());

    public IActionResult Create() => View(new ItemEnergia());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(ItemEnergia item)
    {
        if (!ModelState.IsValid) return View(item);

        _db.ItensEnergia.Add(item);
        _db.SaveChanges();
        TempData["Sucesso"] = "Item criado com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Edit(int id)
    {
        var item = _db.ItensEnergia.Find(id);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, ItemEnergia item)
    {
        if (id != item.Id) return NotFound();
        if (!ModelState.IsValid) return View(item);

        _db.ItensEnergia.Update(item);
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
            var item = _db.ItensEnergia.Find(id);
            if (item == null) return NotFound();

            _db.ItensEnergia.Remove(item);
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
