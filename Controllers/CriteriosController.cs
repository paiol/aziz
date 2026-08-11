using Microsoft.AspNetCore.Mvc;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Controllers;

public class CriteriosController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<CriteriosController> _logger;

    public CriteriosController(AppDbContext db, ILogger<CriteriosController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public IActionResult Index(string? dominio)
    {
        var query = _db.CriteriosAvaliacao.AsQueryable();
        if (!string.IsNullOrWhiteSpace(dominio))
            query = query.Where(c => c.Dominio == dominio);

        ViewBag.Dominios = _db.CriteriosAvaliacao.Select(c => c.Dominio).Distinct().OrderBy(d => d).ToList();
        ViewBag.DominioSelecionado = dominio;

        return View(query.OrderBy(c => c.Dominio).ThenBy(c => c.Nome).ToList());
    }

    public IActionResult Create() => View(new CriterioAvaliacao());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(CriterioAvaliacao criterio)
    {
        if (!ModelState.IsValid) return View(criterio);

        _db.CriteriosAvaliacao.Add(criterio);
        _db.SaveChanges();
        TempData["Sucesso"] = "Critério criado com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Edit(int id)
    {
        var criterio = _db.CriteriosAvaliacao.Find(id);
        if (criterio == null) return NotFound();
        return View(criterio);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, CriterioAvaliacao criterio)
    {
        if (id != criterio.Id) return NotFound();
        if (!ModelState.IsValid) return View(criterio);

        _db.CriteriosAvaliacao.Update(criterio);
        _db.SaveChanges();
        TempData["Sucesso"] = "Critério atualizado com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        try
        {
            var criterio = _db.CriteriosAvaliacao.Find(id);
            if (criterio == null) return NotFound();

            _db.CriteriosAvaliacao.Remove(criterio);
            _db.SaveChanges();
            TempData["Sucesso"] = "Critério removido com sucesso.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover critério {Id}. Provavelmente está em uso por algum processo.", id);
            TempData["EmailWarning"] = "Não foi possível remover: este critério está a ser usado num ou mais processos.";
        }

        return RedirectToAction(nameof(Index));
    }
}
