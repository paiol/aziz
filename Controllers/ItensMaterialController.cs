using Microsoft.AspNetCore.Mvc;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Controllers;

public class ItensMaterialController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<ItensMaterialController> _logger;

    public ItensMaterialController(AppDbContext db, ILogger<ItensMaterialController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public IActionResult Index()
    {
        var itens = _db.ItensMaterial.OrderBy(i => i.NomeItem).ToList();
        return View(itens);
    }

    public IActionResult Create() => View(new ItemMaterial());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(ItemMaterial item)
    {
        if (!ModelState.IsValid) return View(item);

        _db.ItensMaterial.Add(item);
        _db.SaveChanges();
        TempData["Sucesso"] = "Item criado com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Edit(int id)
    {
        var item = _db.ItensMaterial.Find(id);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, ItemMaterial item)
    {
        if (id != item.Id) return NotFound();
        if (!ModelState.IsValid) return View(item);

        _db.ItensMaterial.Update(item);
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
            var item = _db.ItensMaterial.Find(id);
            if (item == null) return NotFound();

            _db.ItensMaterial.Remove(item);
            _db.SaveChanges();
            TempData["Sucesso"] = "Item removido com sucesso.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover item {Id}. Provavelmente está em uso por alguma proposta.", id);
            TempData["EmailWarning"] = "Não foi possível remover: este item está a ser usado numa ou mais propostas.";
        }

        return RedirectToAction(nameof(Index));
    }
}
