using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Helper;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.ViewModels.ItensPedido;

namespace ComparacaoPropostas.Controllers;

public class ItensPedidoController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<ItensPedidoController> _logger;

    public ItensPedidoController(AppDbContext db, ILogger<ItensPedidoController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public IActionResult Index(int pedidoId)
    {
        var pedido = _db.Pedidos
            .Include(p => p.Processo)
            .Include(p => p.ItensPedido).ThenInclude(ip => ip.ItemMaterial)
            .FirstOrDefault(p => p.Id == pedidoId);

        if (pedido == null) return NotFound();

        ViewBag.Pedido = pedido;
        return View(pedido.ItensPedido.OrderBy(ip => ip.ItemMaterial.NomeItem).ToList());
    }

    public IActionResult Create(int pedidoId)
    {
        var pedido = _db.Pedidos.Find(pedidoId);
        if (pedido == null) return NotFound();

        ViewBag.PedidoTipo = pedido.TipoProposta;
        ViewBag.Categorias = ItemPickerHelper.ObterCategorias(_db);
        return View(new NovoItensPedidoVM { PedidoId = pedidoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(NovoItensPedidoVM model)
    {
        var pedido = _db.Pedidos.Find(model.PedidoId);
        if (pedido == null) return NotFound();

        var linhasValidas = (model.Itens ?? new()).Where(i => !string.IsNullOrWhiteSpace(i.ChaveItem)).ToList();
        if (linhasValidas.Count == 0)
            ModelState.AddModelError("", "Adicione pelo menos uma linha de item (escolha um item da lista de sugestões).");

        if (!ModelState.IsValid)
        {
            ViewBag.PedidoTipo = pedido.TipoProposta;
            ViewBag.Categorias = ItemPickerHelper.ObterCategorias(_db);
            return View(model);
        }

        var adicionados = 0;
        foreach (var linha in linhasValidas)
        {
            var (itemMaterialId, tipoCatalogo) = ItemPickerHelper.ResolverChaveItem(_db, linha.ChaveItem);
            if (itemMaterialId == null) continue;

            _db.ItensPedido.Add(new ItemPedido
            {
                PedidoPropostaId = model.PedidoId,
                ItemMaterialId = itemMaterialId.Value,
                QuantidadeSolicitada = linha.QuantidadeSolicitada,
                Observacao = linha.Observacao,
                TipoCatalogo = tipoCatalogo
            });
            adicionados++;
        }
        _db.SaveChanges();

        TempData["Sucesso"] = $"{adicionados} item(ns) adicionado(s) ao pedido.";
        return RedirectToAction(nameof(Index), new { pedidoId = model.PedidoId });
    }

    [HttpGet]
    public IActionResult BuscarItens(string? termo, string? dominio)
        => Json(ItemPickerHelper.Buscar(_db, termo, dominio));

    [HttpGet]
    public IActionResult ObterMateriais(string categoria)
        => Json(ItemPickerHelper.ObterMateriaisPorCategoria(_db, categoria));

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var item = _db.ItensPedido.Find(id);
        if (item == null) return NotFound();

        var pedidoId = item.PedidoPropostaId;
        _db.ItensPedido.Remove(item);
        _db.SaveChanges();
        TempData["Sucesso"] = "Item removido.";
        return RedirectToAction(nameof(Index), new { pedidoId });
    }
}
