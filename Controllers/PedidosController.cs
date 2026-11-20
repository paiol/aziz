using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Helper;
using Microsoft.AspNetCore.Mvc.Rendering;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Services;

namespace ComparacaoPropostas.Controllers;

public class PedidosController : Controller
{
    private readonly AppDbContext _db;
    private readonly IPropostaExcelService _excelService;
    private readonly ILogger<PedidosController> _logger;

    public PedidosController(AppDbContext db, IPropostaExcelService excelService, ILogger<PedidosController> logger)
    {
        _db = db;
        _excelService = excelService;
        _logger = logger;
    }

    public IActionResult Index()
    {
        var pedidos = _db.Pedidos
            .Include(p => p.Processo)
            .Include(p => p.ItensPedido)
            .Include(p => p.ProjetoPlano).ThenInclude(pp => pp!.Plano)
            .OrderByDescending(p => p.CriadoEm)
            .ToList();

        return View(pedidos);
    }

    // Uma compra nasce sempre de um projeto Capex aceite (pode vir pré-escolhido do Capex).
    public IActionResult Create(int? projetoPlanoId)
    {
        var pedido = new PedidoProposta { ProjetoPlanoId = projetoPlanoId };
        if (projetoPlanoId.HasValue && _db.ProjetosPlano.Find(projetoPlanoId.Value) is { } projeto)
            pedido.Area = projeto.Area;

        CarregarProjetosCapex(pedido.ProjetoPlanoId);
        return View(pedido);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(PedidoProposta pedido)
    {
        ValidarProjetoCapex(pedido.ProjetoPlanoId, obrigatorio: true);
        if (!ModelState.IsValid)
        {
            CarregarProjetosCapex(pedido.ProjetoPlanoId);
            return View(pedido);
        }

        pedido.Status = StatusPedido.EmCurso;
        _db.Pedidos.Add(pedido);
        _db.SaveChanges();

        TempData["Sucesso"] = "Pedido de proposta registado. Cria agora o Processo correspondente.";
        return RedirectToAction(nameof(Index));
    }

    public IActionResult Edit(int id)
    {
        var pedido = _db.Pedidos.Find(id);
        if (pedido == null) return NotFound();
        CarregarProjetosCapex(pedido.ProjetoPlanoId);
        return View(pedido);
    }

    // Lista os projetos aceites (Capex) agrupados por plano, para escolher na compra.
    // O projeto atual da compra aparece sempre, mesmo que já não esteja aceite.
    private void CarregarProjetosCapex(int? atualId)
    {
        var projetos = _db.ProjetosPlano
            .Include(p => p.Plano)
            .Where(p => p.EstadoAprovacao == EstadoAprovacaoProjeto.Aceite || p.Id == atualId)
            .AsNoTracking()
            .ToList()
            .Where(p => p.Caixa != CaixaPlano.ForaDoPlano || p.Id == atualId)
            .OrderByDescending(p => p.Plano.Ano).ThenBy(p => p.Area).ThenBy(p => p.Nome)
            .ToList();

        var grupos = projetos.Select(p => p.Plano.Designacao).Distinct().ToDictionary(d => d, d => new SelectListGroup { Name = d });
        ViewBag.ProjetosCapex = projetos
            .Select(p => new SelectListItem($"{p.Area.ToLabel()} · {p.Nome}", p.Id.ToString(), p.Id == atualId) { Group = grupos[p.Plano.Designacao] })
            .ToList();
    }

    private void ValidarProjetoCapex(int? projetoPlanoId, bool obrigatorio, int? atualId = null)
    {
        if (projetoPlanoId == null)
        {
            if (obrigatorio) ModelState.AddModelError(nameof(PedidoProposta.ProjetoPlanoId), "Escolha o projeto Capex a que esta compra pertence.");
            return;
        }

        var projeto = _db.ProjetosPlano.AsNoTracking().FirstOrDefault(p => p.Id == projetoPlanoId);
        var aceite = projeto != null && projeto.EstadoAprovacao == EstadoAprovacaoProjeto.Aceite && projeto.Caixa != CaixaPlano.ForaDoPlano;
        if (!aceite && projetoPlanoId != atualId)
            ModelState.AddModelError(nameof(PedidoProposta.ProjetoPlanoId), "Só é possível associar compras a projetos aceites no Capex.");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, PedidoProposta pedido)
    {
        if (id != pedido.Id) return NotFound();

        // Status is only ever changed by app logic (Processo creation/cancelamento), never
        // directly from this form — keep whatever is already stored. CriadoEm is likewise
        // fixed at registo time.
        var existente = _db.Pedidos.AsNoTracking().First(p => p.Id == id);
        pedido.Status = existente.Status;
        pedido.CriadoEm = existente.CriadoEm;
        ModelState.Remove(nameof(PedidoProposta.Status));
        ModelState.Remove(nameof(PedidoProposta.CriadoEm));

        // Pedidos antigos (anteriores à ligação ao Capex) podem continuar sem projeto;
        // depois de ligado, o projeto não pode ser retirado.
        ValidarProjetoCapex(pedido.ProjetoPlanoId, obrigatorio: existente.ProjetoPlanoId != null, atualId: existente.ProjetoPlanoId);

        if (!ModelState.IsValid)
        {
            CarregarProjetosCapex(pedido.ProjetoPlanoId);
            return View(pedido);
        }

        try
        {
            _db.Pedidos.Update(pedido);
            _db.SaveChanges();
            TempData["Sucesso"] = "Pedido atualizado com sucesso.";
            return RedirectToAction(nameof(Index));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao editar pedido {Id}.", id);
            ModelState.AddModelError("", "Não foi possível guardar as alterações.");
            CarregarProjetosCapex(pedido.ProjetoPlanoId);
            return View(pedido);
        }
    }

    public IActionResult ExportarExcel(int id)
    {
        var pedido = _db.Pedidos
            .Include(p => p.Processo)
            .Include(p => p.ItensPedido).ThenInclude(ip => ip.ItemMaterial)
            .FirstOrDefault(p => p.Id == id);

        if (pedido == null) return NotFound();

        if (pedido.ItensPedido.Count == 0)
        {
            TempData["EmailWarning"] = "Este pedido ainda não tem itens. Adiciona itens a partir do Processo correspondente.";
            return RedirectToAction(nameof(Index));
        }

        var fornecedor = pedido.Processo?.Fornecedor ?? "-";
        var conteudo = _excelService.GerarPedidoExcel(pedido.TipoProposta, fornecedor, pedido.ItensPedido);
        var nomeFicheiro = $"Pedido_{pedido.TipoProposta}.xlsx".Replace(" ", "_");
        return File(conteudo, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nomeFicheiro);
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
