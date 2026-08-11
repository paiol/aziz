using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Controllers;

public class ItensPropostaController : Controller
{
    private readonly AppDbContext _db;

    public ItensPropostaController(AppDbContext db)
    {
        _db = db;
    }

    public IActionResult Index(int propostaId)
    {
        var proposta = _db.Propostas
            .Include(p => p.ItensProposta).ThenInclude(ip => ip.ItemMaterial)
            .FirstOrDefault(p => p.Id == propostaId);

        if (proposta == null) return NotFound();

        ViewBag.Proposta = proposta;
        return View(proposta.ItensProposta.OrderBy(ip => ip.ItemMaterial.NomeItem).ToList());
    }

    public IActionResult Create(int propostaId)
    {
        var proposta = _db.Propostas.Find(propostaId);
        if (proposta == null) return NotFound();

        ViewBag.PropostaFornecedor = proposta.Fornecedor;
        ViewBag.Itens = _db.ItensMaterial.OrderBy(i => i.NomeItem).ToList();
        return View(new ItemProposta { PropostaId = propostaId, Incluido = true });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(ItemProposta item)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.PropostaFornecedor = _db.Propostas.Find(item.PropostaId)?.Fornecedor;
            ViewBag.Itens = _db.ItensMaterial.OrderBy(i => i.NomeItem).ToList();
            return View(item);
        }

        _db.ItensProposta.Add(item);
        _db.SaveChanges();
        TempData["Sucesso"] = "Item adicionado à proposta.";
        return RedirectToAction(nameof(Index), new { propostaId = item.PropostaId });
    }

    public IActionResult Edit(int id)
    {
        var item = _db.ItensProposta.Find(id);
        if (item == null) return NotFound();

        ViewBag.PropostaFornecedor = _db.Propostas.Find(item.PropostaId)?.Fornecedor;
        ViewBag.Itens = _db.ItensMaterial.OrderBy(i => i.NomeItem).ToList();
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, ItemProposta item)
    {
        if (id != item.Id) return NotFound();
        if (!ModelState.IsValid)
        {
            ViewBag.PropostaFornecedor = _db.Propostas.Find(item.PropostaId)?.Fornecedor;
            ViewBag.Itens = _db.ItensMaterial.OrderBy(i => i.NomeItem).ToList();
            return View(item);
        }

        _db.ItensProposta.Update(item);
        _db.SaveChanges();
        TempData["Sucesso"] = "Item atualizado.";
        return RedirectToAction(nameof(Index), new { propostaId = item.PropostaId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var item = _db.ItensProposta.Find(id);
        if (item == null) return NotFound();

        var propostaId = item.PropostaId;
        _db.ItensProposta.Remove(item);
        _db.SaveChanges();
        TempData["Sucesso"] = "Item removido.";
        return RedirectToAction(nameof(Index), new { propostaId });
    }
}
