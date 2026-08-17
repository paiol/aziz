using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Helper;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Services;

namespace ComparacaoPropostas.Controllers;

public class PropostasController : Controller
{
    private readonly AppDbContext _db;
    private readonly IScoringService _scoringService;
    private readonly ILogger<PropostasController> _logger;

    public PropostasController(AppDbContext db, IScoringService scoringService, ILogger<PropostasController> logger)
    {
        _db = db;
        _scoringService = scoringService;
        _logger = logger;
    }

    public IActionResult Create(int processoId)
    {
        var processo = _db.Processos.Find(processoId);
        if (processo == null) return NotFound();

        ViewBag.ProcessoNome = processo.Nome;
        ViewBag.TipoCompra = processo.TipoCompra;
        return View(new Proposta
        {
            ProcessoId = processoId,
            Moeda = MoedaHelper.MoedaParaTipoCompra(processo.TipoCompra),
            TaxaCambio = processo.TaxaCambioPadrao > 0 ? processo.TaxaCambioPadrao : MoedaHelper.TaxaEurCvePadrao
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Proposta proposta)
    {
        var processo = _db.Processos
            .Include(p => p.PedidoProposta).ThenInclude(pp => pp.ItensPedido)
            .FirstOrDefault(p => p.Id == proposta.ProcessoId);

        if (processo == null) return NotFound();

        proposta.Moeda = MoedaHelper.MoedaParaTipoCompra(processo.TipoCompra);

        if (proposta.TaxaCambio <= 0)
        {
            proposta.TaxaCambio = processo.TaxaCambioPadrao > 0 ? processo.TaxaCambioPadrao : MoedaHelper.TaxaEurCvePadrao;
        }

        if (!ModelState.IsValid)
        {
            ViewBag.ProcessoNome = processo.Nome;
            ViewBag.TipoCompra = processo.TipoCompra;
            return View(proposta);
        }

        using var transaction = _db.Database.BeginTransaction();
        try
        {
            _scoringService.ClonarItensPedidoParaProposta(proposta, processo.PedidoProposta.ItensPedido);
            proposta.ValorTotal = 0m;

            _db.Propostas.Add(proposta);
            _db.SaveChanges();
            transaction.Commit();

            TempData["Sucesso"] = $"Proposta adicionada para '{proposta.Fornecedor}' com {proposta.ItensProposta.Count} item(ns) pré-carregados.";
            return RedirectToAction("Index", "ItensProposta", new { propostaId = proposta.Id });
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Erro ao adicionar proposta ao processo {ProcessoId}.", proposta.ProcessoId);
            ModelState.AddModelError("", "Não foi possível criar a proposta.");
            ViewBag.ProcessoNome = processo.Nome;
            ViewBag.TipoCompra = processo.TipoCompra;
            return View(proposta);
        }
    }

    public IActionResult Edit(int id)
    {
        var proposta = _db.Propostas.Include(p => p.Anexos).FirstOrDefault(p => p.Id == id);
        if (proposta == null) return NotFound();

        var processo = _db.Processos.Find(proposta.ProcessoId);
        ViewBag.ProcessoNome = processo?.Nome;
        ViewBag.TipoCompra = processo?.TipoCompra ?? TipoCompra.Nacional;
        return View(proposta);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, Proposta proposta)
    {
        if (id != proposta.Id) return NotFound();

        var existente = _db.Propostas
            .Include(p => p.ItensProposta)
            .FirstOrDefault(p => p.Id == id);

        if (existente == null) return NotFound();

        var processo = _db.Processos.Find(proposta.ProcessoId);

        if (proposta.TaxaCambio <= 0)
        {
            proposta.TaxaCambio = processo?.TaxaCambioPadrao > 0 ? processo.TaxaCambioPadrao : MoedaHelper.TaxaEurCvePadrao;
        }

        if (!ModelState.IsValid)
        {
            ViewBag.ProcessoNome = processo?.Nome;
            ViewBag.TipoCompra = processo?.TipoCompra ?? TipoCompra.Nacional;
            return View(proposta);
        }

        try
        {
            existente.Fornecedor = proposta.Fornecedor.Trim();
            existente.Moeda = MoedaHelper.MoedaParaTipoCompra(processo?.TipoCompra ?? TipoCompra.Nacional);
            existente.TaxaCambio = proposta.TaxaCambio;
            existente.PrazoEntregaDias = proposta.PrazoEntregaDias;
            existente.Garantia = proposta.Garantia?.Trim();
            existente.ValidadeProposta = proposta.ValidadeProposta;
            existente.Status = proposta.Status;
            existente.Observacoes = proposta.Observacoes?.Trim();

            // Recalcular valor total a partir dos itens incluídos
            existente.ValorTotal = existente.ItensProposta
                .Where(i => i.Incluido)
                .Sum(i => i.Quantidade * i.PrecoUnitario);

            _db.SaveChanges();
            TempData["Sucesso"] = "Proposta atualizada com sucesso.";
            return RedirectToAction("Details", "Processos", new { id = existente.ProcessoId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao editar proposta {Id}.", id);
            ModelState.AddModelError("", "Não foi possível guardar as alterações.");
            ViewBag.ProcessoNome = processo?.Nome;
            ViewBag.TipoCompra = processo?.TipoCompra ?? TipoCompra.Nacional;
            return View(proposta);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var proposta = _db.Propostas.Find(id);
        if (proposta == null) return NotFound();

        var processoId = proposta.ProcessoId;
        try
        {
            _db.Propostas.Remove(proposta);
            _db.SaveChanges();
            TempData["Sucesso"] = "Proposta removida com sucesso.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover proposta {Id}.", id);
            TempData["EmailWarning"] = "Não foi possível remover a proposta.";
        }

        return RedirectToAction("Details", "Processos", new { id = processoId });
    }
}
