using Microsoft.AspNetCore.Mvc;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Services;
using ComparacaoPropostas.ViewModels.ItensMQT;

namespace ComparacaoPropostas.Controllers;

public class ItensMQTController : Controller
{
    private readonly AppDbContext _db;
    private readonly IMqtExcelService _mqtExcelService;
    private readonly ILogger<ItensMQTController> _logger;

    public ItensMQTController(AppDbContext db, IMqtExcelService mqtExcelService, ILogger<ItensMQTController> logger)
    {
        _db = db;
        _mqtExcelService = mqtExcelService;
        _logger = logger;
    }

    public IActionResult Index(int projetoObraId)
    {
        var projeto = _db.ProjetosObra.Find(projetoObraId);
        if (projeto == null) return NotFound();

        var itens = _db.ItensMQT
            .Where(i => i.ProjetoObraId == projetoObraId)
            .OrderBy(i => i.CodigoIndexacao)
            .ThenBy(i => i.Descricao)
            .ToList();

        ViewBag.ProjetoObra = projeto;
        return View(itens);
    }

    public IActionResult Create(int projetoObraId)
    {
        var projeto = _db.ProjetosObra.Find(projetoObraId);
        if (projeto == null) return NotFound();

        ViewBag.ProjetoObraNome = projeto.Designacao;
        return View(new NovosItensMQTVM { ProjetoObraId = projetoObraId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(NovosItensMQTVM model)
    {
        var projeto = _db.ProjetosObra.Find(model.ProjetoObraId);
        if (projeto == null) return NotFound();

        var linhasValidas = (model.Itens ?? new()).Where(i => !string.IsNullOrWhiteSpace(i.Descricao)).ToList();
        if (linhasValidas.Count == 0)
        {
            ModelState.AddModelError("", "Adicione pelo menos uma linha de item.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.ProjetoObraNome = projeto.Designacao;
            return View(model);
        }

        foreach (var linha in linhasValidas)
        {
            linha.ProjetoObraId = model.ProjetoObraId;
            _db.ItensMQT.Add(linha);
        }
        _db.SaveChanges();

        TempData["Sucesso"] = $"{linhasValidas.Count} item(ns) adicionado(s) ao Mapa de Quantidades.";
        return RedirectToAction(nameof(Index), new { projetoObraId = model.ProjetoObraId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ImportarExcel(int projetoObraId, IFormFile ficheiro)
    {
        var projeto = _db.ProjetosObra.Find(projetoObraId);
        if (projeto == null) return NotFound();

        if (ficheiro == null || ficheiro.Length == 0)
        {
            TempData["EmailWarning"] = "Selecione o ficheiro Excel do Mapa de Quantidades.";
            return RedirectToAction(nameof(Index), new { projetoObraId });
        }

        try
        {
            using var stream = ficheiro.OpenReadStream();
            var linhas = _mqtExcelService.LerMqtExcel(stream);

            if (linhas.Count == 0)
            {
                TempData["EmailWarning"] = "Não foi possível reconhecer nenhuma linha no ficheiro. Verifique se tem colunas de Descrição/Designação, Unidade e Quantidade.";
                return RedirectToAction(nameof(Index), new { projetoObraId });
            }

            foreach (var linha in linhas)
            {
                _db.ItensMQT.Add(new ItemMQT
                {
                    ProjetoObraId = projetoObraId,
                    CodigoIndexacao = linha.CodigoIndexacao,
                    Descricao = linha.Descricao,
                    Unidade = linha.Unidade,
                    Quantidade = linha.Quantidade
                });
            }
            _db.SaveChanges();

            TempData["Sucesso"] = $"Mapa de Quantidades importado: {linhas.Count} item(ns).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao importar Mapa de Quantidades para o projeto {ProjetoObraId}.", projetoObraId);
            TempData["EmailWarning"] = "Não foi possível ler o ficheiro Excel.";
        }

        return RedirectToAction(nameof(Index), new { projetoObraId });
    }

    public IActionResult Edit(int id)
    {
        var item = _db.ItensMQT.Find(id);
        if (item == null) return NotFound();
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, ItemMQT item)
    {
        if (id != item.Id) return NotFound();
        if (!ModelState.IsValid) return View(item);

        _db.ItensMQT.Update(item);
        _db.SaveChanges();

        TempData["Sucesso"] = "Item atualizado.";
        return RedirectToAction(nameof(Index), new { projetoObraId = item.ProjetoObraId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var item = _db.ItensMQT.Find(id);
        if (item == null) return NotFound();

        var projetoObraId = item.ProjetoObraId;
        _db.ItensMQT.Remove(item);
        _db.SaveChanges();

        TempData["Sucesso"] = "Item removido.";
        return RedirectToAction(nameof(Index), new { projetoObraId });
    }
}
