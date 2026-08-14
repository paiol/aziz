using Microsoft.AspNetCore.Mvc;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Controllers;

public class AvaliadoresController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<AvaliadoresController> _logger;

    public AvaliadoresController(AppDbContext db, ILogger<AvaliadoresController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public IActionResult Index()
    {
        var avaliadores = _db.Avaliadores
            .OrderBy(a => a.Nome)
            .ToList();

        return View(avaliadores);
    }

    public IActionResult Create()
    {
        return View(new Avaliador());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Avaliador avaliador)
    {
        if (!ModelState.IsValid) return View(avaliador);

        try
        {
            avaliador.Nome = avaliador.Nome.Trim();
            avaliador.Perfil = avaliador.Perfil?.Trim();
            avaliador.Email = avaliador.Email?.Trim();

            _db.Avaliadores.Add(avaliador);
            _db.SaveChanges();

            TempData["Sucesso"] = "Avaliador registado com sucesso.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao registar avaliador.");
            ModelState.AddModelError("", "Não foi possível registar o avaliador.");
            return View(avaliador);
        }
    }

    public IActionResult Edit(int id)
    {
        var avaliador = _db.Avaliadores.Find(id);
        if (avaliador == null) return NotFound();
        return View(avaliador);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, Avaliador avaliador)
    {
        if (id != avaliador.Id) return NotFound();
        if (!ModelState.IsValid) return View(avaliador);

        try
        {
            var existente = _db.Avaliadores.Find(id);
            if (existente == null) return NotFound();

            existente.Nome = avaliador.Nome.Trim();
            existente.Perfil = avaliador.Perfil?.Trim();
            existente.Email = avaliador.Email?.Trim();
            existente.Ativo = avaliador.Ativo;

            _db.SaveChanges();
            TempData["Sucesso"] = "Avaliador atualizado com sucesso.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao editar avaliador {Id}.", id);
            ModelState.AddModelError("", "Não foi possível guardar as alterações.");
            return View(avaliador);
        }
    }
}
