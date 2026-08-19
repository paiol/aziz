using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.ViewModels.CriteriosObra;

namespace ComparacaoPropostas.Controllers;

public class CriteriosObraController : Controller
{
    private readonly AppDbContext _db;

    public CriteriosObraController(AppDbContext db)
    {
        _db = db;
    }

    public IActionResult Index(int projetoObraId)
    {
        var projeto = _db.ProjetosObra.Find(projetoObraId);
        if (projeto == null) return NotFound();

        var criterios = _db.CriteriosObra
            .Where(c => c.ProjetoObraId == projetoObraId)
            .OrderByDescending(c => c.Peso)
            .ToList();

        ViewBag.ProjetoObra = projeto;
        return View(criterios);
    }

    public IActionResult Create(int projetoObraId)
    {
        var projeto = _db.ProjetosObra.Find(projetoObraId);
        if (projeto == null) return NotFound();

        ViewBag.ProjetoObraNome = projeto.Designacao;
        return View(new NovosCriteriosObraVM { ProjetoObraId = projetoObraId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(NovosCriteriosObraVM model)
    {
        var projeto = _db.ProjetosObra.Find(model.ProjetoObraId);
        if (projeto == null) return NotFound();

        var linhasValidas = (model.Itens ?? new()).Where(i => !string.IsNullOrWhiteSpace(i.Nome)).ToList();
        if (linhasValidas.Count == 0)
        {
            ModelState.AddModelError("", "Adicione pelo menos um critério.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.ProjetoObraNome = projeto.Designacao;
            return View(model);
        }

        foreach (var linha in linhasValidas)
        {
            linha.ProjetoObraId = model.ProjetoObraId;
            _db.CriteriosObra.Add(linha);
        }
        _db.SaveChanges();

        TempData["Sucesso"] = $"{linhasValidas.Count} critério(s) adicionado(s).";
        return RedirectToAction(nameof(Index), new { projetoObraId = model.ProjetoObraId });
    }

    public IActionResult Edit(int id)
    {
        var criterio = _db.CriteriosObra.Find(id);
        if (criterio == null) return NotFound();
        return View(criterio);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, CriterioObra criterio)
    {
        if (id != criterio.Id) return NotFound();
        if (!ModelState.IsValid) return View(criterio);

        _db.CriteriosObra.Update(criterio);
        _db.SaveChanges();
        TempData["Sucesso"] = "Critério atualizado com sucesso.";
        return RedirectToAction(nameof(Index), new { projetoObraId = criterio.ProjetoObraId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var criterio = _db.CriteriosObra.Include(c => c.Avaliacoes).FirstOrDefault(c => c.Id == id);
        if (criterio == null) return NotFound();

        var projetoObraId = criterio.ProjetoObraId;

        _db.AvaliacoesObra.RemoveRange(criterio.Avaliacoes);
        _db.CriteriosObra.Remove(criterio);
        _db.SaveChanges();

        TempData["Sucesso"] = "Critério removido.";
        return RedirectToAction(nameof(Index), new { projetoObraId });
    }
}
