using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Services;

namespace ComparacaoPropostas.Controllers;

public class PropostasEmpreiteiroController : Controller
{
    private readonly AppDbContext _db;
    private readonly IScoringObraService _scoringObraService;
    private readonly ILogger<PropostasEmpreiteiroController> _logger;

    public PropostasEmpreiteiroController(AppDbContext db, IScoringObraService scoringObraService, ILogger<PropostasEmpreiteiroController> logger)
    {
        _db = db;
        _scoringObraService = scoringObraService;
        _logger = logger;
    }

    public IActionResult Create(int projetoObraId)
    {
        var projeto = _db.ProjetosObra.Find(projetoObraId);
        if (projeto == null) return NotFound();

        ViewBag.ProjetoObraNome = projeto.Designacao;
        return View(new PropostaEmpreiteiro { ProjetoObraId = projetoObraId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(PropostaEmpreiteiro proposta)
    {
        var projeto = _db.ProjetosObra.Find(proposta.ProjetoObraId);
        if (projeto == null) return NotFound();

        if (!ModelState.IsValid)
        {
            ViewBag.ProjetoObraNome = projeto.Designacao;
            return View(proposta);
        }

        try
        {
            var itensMQT = _db.ItensMQT.Where(i => i.ProjetoObraId == proposta.ProjetoObraId).ToList();
            _scoringObraService.ClonarItensMQTParaProposta(proposta, itensMQT);

            _db.PropostasEmpreiteiro.Add(proposta);
            _db.SaveChanges();

            TempData["Sucesso"] = $"Proposta adicionada para '{proposta.Empreiteiro}' com {itensMQT.Count} item(ns) pré-carregados do MQT.";
            return RedirectToAction("Index", "ItensPropostaEmpreiteiro", new { propostaEmpreiteiroId = proposta.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao adicionar proposta de empreiteiro ao projeto {ProjetoObraId}.", proposta.ProjetoObraId);
            ModelState.AddModelError("", "Não foi possível criar a proposta.");
            ViewBag.ProjetoObraNome = projeto.Designacao;
            return View(proposta);
        }
    }

    public IActionResult Edit(int id)
    {
        var proposta = _db.PropostasEmpreiteiro.Include(p => p.ItensProposta).FirstOrDefault(p => p.Id == id);
        if (proposta == null) return NotFound();

        var projeto = _db.ProjetosObra.Find(proposta.ProjetoObraId);
        ViewBag.ProjetoObraNome = projeto?.Designacao;
        return View(proposta);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, PropostaEmpreiteiro proposta)
    {
        if (id != proposta.Id) return NotFound();

        var existente = _db.PropostasEmpreiteiro.Find(id);
        if (existente == null) return NotFound();

        if (!ModelState.IsValid)
        {
            var projeto = _db.ProjetosObra.Find(proposta.ProjetoObraId);
            ViewBag.ProjetoObraNome = projeto?.Designacao;
            return View(proposta);
        }

        try
        {
            existente.Empreiteiro = proposta.Empreiteiro.Trim();
            existente.PrazoEntregaDias = proposta.PrazoEntregaDias;
            existente.ValidadeProposta = proposta.ValidadeProposta;
            existente.Status = proposta.Status;
            existente.Observacoes = proposta.Observacoes?.Trim();

            _db.SaveChanges();
            TempData["Sucesso"] = "Proposta atualizada com sucesso.";
            return RedirectToAction("Details", "ProjetosObra", new { id = existente.ProjetoObraId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao editar proposta de empreiteiro {Id}.", id);
            ModelState.AddModelError("", "Não foi possível guardar as alterações.");
            var projeto = _db.ProjetosObra.Find(proposta.ProjetoObraId);
            ViewBag.ProjetoObraNome = projeto?.Designacao;
            return View(proposta);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var proposta = _db.PropostasEmpreiteiro.Find(id);
        if (proposta == null) return NotFound();

        var projetoObraId = proposta.ProjetoObraId;
        try
        {
            _db.PropostasEmpreiteiro.Remove(proposta);
            _db.SaveChanges();
            TempData["Sucesso"] = "Proposta removida com sucesso.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover proposta de empreiteiro {Id}.", id);
            TempData["EmailWarning"] = "Não foi possível remover a proposta.";
        }

        return RedirectToAction("Details", "ProjetosObra", new { id = projetoObraId });
    }
}
