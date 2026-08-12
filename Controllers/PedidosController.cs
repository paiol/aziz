using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Controllers;

public class PedidosController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<PedidosController> _logger;

    public PedidosController(AppDbContext db, ILogger<PedidosController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public IActionResult Index()
    {
        var pedidos = _db.Pedidos
            .Include(p => p.Processo)
            .OrderByDescending(p => p.CriadoEm)
            .ToList();

        return View(pedidos);
    }

    public IActionResult Create() => View(new PedidoProposta());

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(PedidoProposta pedido)
    {
        if (!ModelState.IsValid) return View(pedido);

        pedido.Status = StatusPedido.Pendente;
        _db.Pedidos.Add(pedido);
        _db.SaveChanges();

        TempData["Sucesso"] = "Pedido de proposta registado. Cria agora o Processo correspondente.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var pedido = _db.Pedidos.Include(p => p.Processo).FirstOrDefault(p => p.Id == id);
        if (pedido == null) return NotFound();

        if (pedido.Processo != null)
        {
            TempData["EmailWarning"] = "Não é possível remover: este pedido já gerou um processo.";
            return RedirectToAction(nameof(Index));
        }

        try
        {
            _db.Pedidos.Remove(pedido);
            _db.SaveChanges();
            TempData["Sucesso"] = "Pedido removido.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover pedido {Id}.", id);
            TempData["EmailWarning"] = "Não foi possível remover o pedido.";
        }

        return RedirectToAction(nameof(Index));
    }
}
