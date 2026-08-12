using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Services;

namespace ComparacaoPropostas.Controllers;

public class ItensMaterialController : Controller
{
    private readonly AppDbContext _db;
    private readonly IPropostaExcelService _excelService;
    private readonly ILogger<ItensMaterialController> _logger;

    public ItensMaterialController(AppDbContext db, IPropostaExcelService excelService, ILogger<ItensMaterialController> logger)
    {
        _db = db;
        _excelService = excelService;
        _logger = logger;
    }

    public IActionResult Index()
    {
        var itens = _db.ItensMaterial
            .Include(i => i.SubItens)
            .Where(i => i.ItemPaiId == null)
            .OrderBy(i => i.NomeItem)
            .ToList();

        // Flatten parent -> sub-items so the view can render a simple indented list.
        var lista = new List<ItemMaterial>();
        foreach (var pai in itens)
        {
            lista.Add(pai);
            lista.AddRange(pai.SubItens.OrderBy(s => s.NomeItem));
        }

        return View(lista);
    }

    private void CarregarItensPai(int? excluirId = null)
    {
        ViewBag.ItensPai = _db.ItensMaterial
            .Where(i => i.ItemPaiId == null && i.Id != excluirId)
            .OrderBy(i => i.NomeItem)
            .ToList();
    }

    public IActionResult Create()
    {
        CarregarItensPai();
        return View(new ItemMaterial());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(ItemMaterial item)
    {
        if (!ModelState.IsValid)
        {
            CarregarItensPai();
            return View(item);
        }

        _db.ItensMaterial.Add(item);
        _db.SaveChanges();
        TempData["Sucesso"] = "Item criado com sucesso.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Edit(int id)
    {
        var item = _db.ItensMaterial.Find(id);
        if (item == null) return NotFound();

        CarregarItensPai(id);
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, ItemMaterial item)
    {
        if (id != item.Id) return NotFound();
        if (item.ItemPaiId == item.Id)
        {
            ModelState.AddModelError("ItemPaiId", "Um item não pode ser pai de si mesmo.");
        }
        if (!ModelState.IsValid)
        {
            CarregarItensPai(id);
            return View(item);
        }

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
            _logger.LogError(ex, "Erro ao remover item {Id}. Provavelmente está em uso por alguma proposta ou tem sub-itens.", id);
            TempData["EmailWarning"] = "Não foi possível remover: este item está a ser usado numa ou mais propostas, ou ainda tem sub-itens associados.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ImportarExcel(IFormFile ficheiro, string? dominio)
    {
        if (ficheiro == null || ficheiro.Length == 0)
        {
            TempData["EmailWarning"] = "Selecione um ficheiro Excel.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            using var stream = ficheiro.OpenReadStream();
            var linhas = _excelService.LerCatalogoExcel(stream);

            if (linhas.Count == 0)
            {
                TempData["EmailWarning"] = "Não foi possível encontrar uma coluna 'Item' no ficheiro.";
                return RedirectToAction(nameof(Index));
            }

            var existentes = new HashSet<string>(_db.ItensMaterial.Select(i => i.NomeItem), StringComparer.OrdinalIgnoreCase);
            var criados = 0;

            foreach (var linha in linhas)
            {
                if (!existentes.Add(linha.NomeItem)) continue; // already in the catalog, skip

                _db.ItensMaterial.Add(new ItemMaterial
                {
                    NomeItem = linha.NomeItem,
                    Categoria = linha.Categoria,
                    Unidade = linha.Unidade,
                    Dominio = string.IsNullOrWhiteSpace(dominio) ? null : dominio
                });
                criados++;
            }

            _db.SaveChanges();
            TempData["Sucesso"] = $"{criados} item(ns) novo(s) importado(s) para o catálogo ({linhas.Count - criados} já existiam e foram ignorados).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao importar catálogo de itens.");
            TempData["EmailWarning"] = "Não foi possível ler o ficheiro Excel.";
        }

        return RedirectToAction(nameof(Index));
    }
}
