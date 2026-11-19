using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Services;

namespace ComparacaoPropostas.Controllers;

public class AcoesFormacaoPlanoController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<AcoesFormacaoPlanoController> _logger;

    public AcoesFormacaoPlanoController(AppDbContext db, ILogger<AcoesFormacaoPlanoController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public IActionResult Index(int planoId)
    {
        var plano = _db.Planos.Include(p => p.AcoesFormacao).AsNoTracking().FirstOrDefault(p => p.Id == planoId);
        if (plano == null) return NotFound();

        ViewBag.Plano = plano;
        ViewBag.Cenarios = PlanoCalculos.CalcularCenariosFormacao(plano.AcoesFormacao, plano.Ano);
        return View(plano.AcoesFormacao.OrderBy(a => a.Ano ?? plano.Ano).ThenBy(a => a.Prioridade).ThenBy(a => a.Acao).ToList());
    }

    public IActionResult Create(int planoId)
    {
        var plano = _db.Planos.Find(planoId);
        if (plano == null) return NotFound();

        ViewBag.Plano = plano;
        return View(new AcaoFormacaoPlano { PlanoId = planoId, Ano = plano.Ano });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(AcaoFormacaoPlano acao)
    {
        var plano = _db.Planos.Find(acao.PlanoId);
        if (plano == null) return NotFound();

        if (!ModelState.IsValid)
        {
            ViewBag.Plano = plano;
            return View(acao);
        }

        _db.AcoesFormacaoPlano.Add(acao);
        _db.SaveChanges();

        TempData["Sucesso"] = "Ação de formação/feira adicionada.";
        return RedirectToAction(nameof(Index), new { planoId = acao.PlanoId });
    }

    public IActionResult Edit(int id)
    {
        var acao = _db.AcoesFormacaoPlano.Include(a => a.Plano).FirstOrDefault(a => a.Id == id);
        if (acao == null) return NotFound();

        ViewBag.Plano = acao.Plano;
        return View(acao);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, AcaoFormacaoPlano acao)
    {
        if (id != acao.Id) return NotFound();

        var existente = _db.AcoesFormacaoPlano.AsNoTracking().FirstOrDefault(a => a.Id == id);
        if (existente == null) return NotFound();
        acao.PlanoId = existente.PlanoId;

        if (!ModelState.IsValid)
        {
            ViewBag.Plano = _db.Planos.Find(acao.PlanoId);
            return View(acao);
        }

        _db.AcoesFormacaoPlano.Update(acao);
        _db.SaveChanges();

        TempData["Sucesso"] = "Ação atualizada com sucesso.";
        return RedirectToAction(nameof(Index), new { planoId = acao.PlanoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var acao = _db.AcoesFormacaoPlano.Find(id);
        if (acao == null) return NotFound();

        var planoId = acao.PlanoId;
        try
        {
            _db.AcoesFormacaoPlano.Remove(acao);
            _db.SaveChanges();
            TempData["Sucesso"] = "Ação removida.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover ação de formação {Id}.", id);
            TempData["EmailWarning"] = "Não foi possível remover a ação.";
        }

        return RedirectToAction(nameof(Index), new { planoId });
    }
}
