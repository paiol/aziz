using Microsoft.AspNetCore.Mvc;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Controllers;

public class FornecedoresController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<FornecedoresController> _logger;

    public FornecedoresController(AppDbContext db, ILogger<FornecedoresController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public IActionResult Index()
    {
        var fornecedores = _db.Fornecedores
            .OrderBy(f => f.Nome)
            .ToList();

        return View(fornecedores);
    }

    public IActionResult Create()
    {
        return View(new Fornecedor());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Fornecedor fornecedor)
    {
        if (!ModelState.IsValid) return View(fornecedor);

        try
        {
            fornecedor.Nome = fornecedor.Nome.Trim();
            fornecedor.Contribuinte = fornecedor.Contribuinte?.Trim();
            fornecedor.Contacto = fornecedor.Contacto?.Trim();
            fornecedor.Email = fornecedor.Email?.Trim();

            if (_db.Fornecedores.Any(f => f.Nome == fornecedor.Nome))
            {
                ModelState.AddModelError(nameof(Fornecedor.Nome), "Já existe um fornecedor registado com este nome.");
                return View(fornecedor);
            }

            _db.Fornecedores.Add(fornecedor);
            _db.SaveChanges();

            TempData["Sucesso"] = "Fornecedor registado com sucesso.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao registar fornecedor.");
            ModelState.AddModelError("", "Não foi possível registar o fornecedor.");
            return View(fornecedor);
        }
    }

    public IActionResult Edit(int id)
    {
        var fornecedor = _db.Fornecedores.Find(id);
        if (fornecedor == null) return NotFound();
        return View(fornecedor);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, Fornecedor fornecedor)
    {
        if (id != fornecedor.Id) return NotFound();
        if (!ModelState.IsValid) return View(fornecedor);

        try
        {
            var existente = _db.Fornecedores.Find(id);
            if (existente == null) return NotFound();

            var nomeNormalizado = fornecedor.Nome.Trim();
            if (_db.Fornecedores.Any(f => f.Id != id && f.Nome == nomeNormalizado))
            {
                ModelState.AddModelError(nameof(Fornecedor.Nome), "Já existe um fornecedor registado com este nome.");
                return View(fornecedor);
            }

            existente.Nome = nomeNormalizado;
            existente.Contribuinte = fornecedor.Contribuinte?.Trim();
            existente.Contacto = fornecedor.Contacto?.Trim();
            existente.Email = fornecedor.Email?.Trim();
            existente.Ativo = fornecedor.Ativo;

            _db.SaveChanges();
            TempData["Sucesso"] = "Fornecedor atualizado com sucesso.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao editar fornecedor {Id}.", id);
            ModelState.AddModelError("", "Não foi possível guardar as alterações.");
            return View(fornecedor);
        }
    }
}
