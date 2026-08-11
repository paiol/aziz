using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.ViewModels.ItensProposta;

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

        var itens = proposta.ItensProposta.OrderBy(ip => ip.ItemMaterial.NomeItem).ToList();
        var incluidos = itens.Where(i => i.Incluido).ToList();

        var indexVm = new ItensPropostaIndexVM
        {
            Proposta = proposta,
            Itens = itens,
            ResumoPorItem = incluidos
                .GroupBy(i => i.ItemMaterial.NomeItem)
                .Select(g => new ResumoItemVM
                {
                    NomeItem = g.Key,
                    QuantidadeTotal = g.Sum(i => i.Quantidade),
                    ValorTotal = g.Sum(i => i.Subtotal)
                })
                .OrderBy(r => r.NomeItem)
                .ToList(),
            QuantidadeGeral = incluidos.Sum(i => i.Quantidade),
            ValorGeral = incluidos.Sum(i => i.Subtotal)
        };

        return View(indexVm);
    }

    private List<ItemMaterial> CarregarCatalogoIndentado()
    {
        var paisComFilhos = _db.ItensMaterial
            .Include(i => i.SubItens)
            .Where(i => i.ItemPaiId == null)
            .OrderBy(i => i.NomeItem)
            .ToList();

        var lista = new List<ItemMaterial>();
        foreach (var pai in paisComFilhos)
        {
            lista.Add(pai);
            lista.AddRange(pai.SubItens.OrderBy(s => s.NomeItem));
        }
        return lista;
    }

    public IActionResult Create(int propostaId)
    {
        var proposta = _db.Propostas.Find(propostaId);
        if (proposta == null) return NotFound();

        ViewBag.PropostaFornecedor = proposta.Fornecedor;
        ViewBag.Itens = CarregarCatalogoIndentado();
        return View(new NovosItensPropostaVM { PropostaId = propostaId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(NovosItensPropostaVM model)
    {
        var proposta = _db.Propostas.Find(model.PropostaId);
        if (proposta == null) return NotFound();

        var linhasValidas = (model.Itens ?? new()).Where(i => i.ItemMaterialId > 0).ToList();
        if (linhasValidas.Count == 0)
        {
            ModelState.AddModelError("", "Adicione pelo menos uma linha de item.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.PropostaFornecedor = proposta.Fornecedor;
            ViewBag.Itens = CarregarCatalogoIndentado();
            return View(model);
        }

        foreach (var linha in linhasValidas)
        {
            linha.PropostaId = model.PropostaId;
            _db.ItensProposta.Add(linha);
        }
        _db.SaveChanges();

        TempData["Sucesso"] = $"{linhasValidas.Count} item(ns) adicionado(s) à proposta.";
        return RedirectToAction(nameof(Index), new { propostaId = model.PropostaId });
    }

    public IActionResult Edit(int id)
    {
        var item = _db.ItensProposta.Find(id);
        if (item == null) return NotFound();

        ViewBag.PropostaFornecedor = _db.Propostas.Find(item.PropostaId)?.Fornecedor;
        ViewBag.Itens = CarregarCatalogoIndentado();
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
            ViewBag.Itens = CarregarCatalogoIndentado();
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
