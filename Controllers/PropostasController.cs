using Microsoft.AspNetCore.Mvc;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Controllers;

public class PropostasController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<PropostasController> _logger;

    public PropostasController(AppDbContext db, ILogger<PropostasController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public IActionResult Create(int processoId)
    {
        var processo = _db.Processos.Find(processoId);
        if (processo == null) return NotFound();

        ViewBag.ProcessoNome = processo.Nome;
        return View(new Proposta { ProcessoId = processoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Proposta proposta)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.ProcessoNome = _db.Processos.Find(proposta.ProcessoId)?.Nome;
            return View(proposta);
        }

        _db.Propostas.Add(proposta);
        _db.SaveChanges();
        TempData["Sucesso"] = "Proposta adicionada com sucesso.";
        return RedirectToAction("Details", "Processos", new { id = proposta.ProcessoId });
    }

    public IActionResult Edit(int id)
    {
        var proposta = _db.Propostas.Find(id);
        if (proposta == null) return NotFound();

        ViewBag.ProcessoNome = _db.Processos.Find(proposta.ProcessoId)?.Nome;
        return View(proposta);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, Proposta proposta)
    {
        if (id != proposta.Id) return NotFound();
        if (!ModelState.IsValid)
        {
            ViewBag.ProcessoNome = _db.Processos.Find(proposta.ProcessoId)?.Nome;
            return View(proposta);
        }

        _db.Propostas.Update(proposta);
        _db.SaveChanges();
        TempData["Sucesso"] = "Proposta atualizada com sucesso.";
        return RedirectToAction("Details", "Processos", new { id = proposta.ProcessoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var proposta = _db.Propostas.Find(id);
        if (proposta == null) return NotFound();

        var processoId = proposta.ProcessoId;
        try
        {
            _db.Propostas.Remove(proposta);
            _db.SaveChanges();
            TempData["Sucesso"] = "Proposta removida com sucesso.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover proposta {Id}.", id);
            TempData["EmailWarning"] = "Não foi possível remover a proposta.";
        }

        return RedirectToAction("Details", "Processos", new { id = processoId });
    }
}
