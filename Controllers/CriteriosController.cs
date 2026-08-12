using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.ViewModels.Criterios;

namespace ComparacaoPropostas.Controllers;

public class CriteriosController : Controller
{
    private readonly AppDbContext _db;

    public CriteriosController(AppDbContext db)
    {
        _db = db;
    }

    public IActionResult Index(int processoId)
    {
        var processo = _db.Processos.Find(processoId);
        if (processo == null) return NotFound();

        var criterios = _db.Criterios
            .Where(c => c.ProcessoId == processoId)
            .OrderByDescending(c => c.Peso)
            .ToList();

        ViewBag.Processo = processo;
        return View(criterios);
    }

    public IActionResult Create(int processoId)
    {
        var processo = _db.Processos.Find(processoId);
        if (processo == null) return NotFound();

        ViewBag.ProcessoNome = processo.Nome;
        return View(new NovosCriteriosVM { ProcessoId = processoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(NovosCriteriosVM model)
    {
        var processo = _db.Processos.Find(model.ProcessoId);
        if (processo == null) return NotFound();

        var linhasValidas = (model.Itens ?? new()).Where(i => !string.IsNullOrWhiteSpace(i.Nome)).ToList();
        if (linhasValidas.Count == 0)
        {
            ModelState.AddModelError("", "Adicione pelo menos um critério.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.ProcessoNome = processo.Nome;
            return View(model);
        }

        foreach (var linha in linhasValidas)
        {
            linha.ProcessoId = model.ProcessoId;
            _db.Criterios.Add(linha);
        }
        _db.SaveChanges();

        TempData["Sucesso"] = $"{linhasValidas.Count} critério(s) adicionado(s).";
        return RedirectToAction(nameof(Index), new { processoId = model.ProcessoId });
    }

    public IActionResult Edit(int id)
    {
        var criterio = _db.Criterios.Find(id);
        if (criterio == null) return NotFound();
        return View(criterio);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, Criterio criterio)
    {
        if (id != criterio.Id) return NotFound();
        if (!ModelState.IsValid) return View(criterio);

        _db.Criterios.Update(criterio);
        _db.SaveChanges();
        TempData["Sucesso"] = "Critério atualizado com sucesso.";
        return RedirectToAction(nameof(Index), new { processoId = criterio.ProcessoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var criterio = _db.Criterios.Include(c => c.Avaliacoes).FirstOrDefault(c => c.Id == id);
        if (criterio == null) return NotFound();

        var processoId = criterio.ProcessoId;

        // Avaliacao->Criterio is Restrict (not Cascade), so remove dependent Avaliacoes explicitly.
        _db.Avaliacoes.RemoveRange(criterio.Avaliacoes);
        _db.Criterios.Remove(criterio);
        _db.SaveChanges();

        TempData["Sucesso"] = "Critério removido.";
        return RedirectToAction(nameof(Index), new { processoId });
    }
}
