using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Helper;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Services;
using ComparacaoPropostas.ViewModels.Pedidos;

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

    public IActionResult Index(int processoId)
    {
        var processo = _db.Processos.Find(processoId);
        if (processo == null) return NotFound();

        var pedidos = _db.Pedidos
            .Include(p => p.ItensPedido)
            .Include(p => p.Propostas)
            .Where(p => p.ProcessoId == processoId)
            .OrderByDescending(p => p.CriadoEm)
            .ToList();

        ViewBag.Processo = processo;
        return View(pedidos);
    }

    public IActionResult Create(int processoId)
    {
        var processo = _db.Processos.Find(processoId);
        if (processo == null) return NotFound();

        ViewBag.ProcessoNome = processo.Nome;
        ViewBag.Itens = CatalogoItensHelper.CarregarIndentado(_db, processo.TipoProcesso);
        return View(new NovoPedidoVM { ProcessoId = processoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(NovoPedidoVM model)
    {
        var processo = _db.Processos.Find(model.ProcessoId);
        if (processo == null) return NotFound();

        var linhasValidas = (model.Itens ?? new()).Where(i => i.ItemMaterialId > 0).ToList();
        if (string.IsNullOrWhiteSpace(model.Fornecedor))
            ModelState.AddModelError(nameof(model.Fornecedor), "Indique o nome do fornecedor.");
        if (linhasValidas.Count == 0)
            ModelState.AddModelError("", "Adicione pelo menos uma linha de item.");

        if (!ModelState.IsValid)
        {
            ViewBag.ProcessoNome = processo.Nome;
            ViewBag.Itens = CatalogoItensHelper.CarregarIndentado(_db, processo.TipoProcesso);
            return View(model);
        }

        var pedido = new PedidoProposta
        {
            ProcessoId = model.ProcessoId,
            Fornecedor = model.Fornecedor,
            Status = StatusPedido.Pendente
        };
        _db.Pedidos.Add(pedido);

        foreach (var linha in linhasValidas)
        {
            pedido.ItensPedido.Add(new ItemPedido
            {
                ItemMaterialId = linha.ItemMaterialId,
                QuantidadeSolicitada = linha.QuantidadeSolicitada,
                Observacao = linha.Observacao
            });
        }
        _db.SaveChanges();

        TempData["Sucesso"] = "Pedido de proposta criado. Descarregue o Excel para enviar ao fornecedor.";
        return RedirectToAction(nameof(Index), new { processoId = model.ProcessoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var pedido = _db.Pedidos.Include(p => p.Propostas).FirstOrDefault(p => p.Id == id);
        if (pedido == null) return NotFound();

        var processoId = pedido.ProcessoId;

        // Unlink first: PedidoProposta.Propostas is Restrict, not Cascade, so the real
        // supplier proposals it produced survive deleting the request that led to them.
        foreach (var proposta in pedido.Propostas)
            proposta.PedidoPropostaId = null;

        _db.Pedidos.Remove(pedido);
        _db.SaveChanges();
        TempData["Sucesso"] = "Pedido removido.";
        return RedirectToAction(nameof(Index), new { processoId });
    }

    public IActionResult ExportarExcel(int id)
    {
        var pedido = _db.Pedidos
            .Include(p => p.Processo)
            .Include(p => p.ItensPedido).ThenInclude(ip => ip.ItemMaterial)
            .FirstOrDefault(p => p.Id == id);

        if (pedido == null) return NotFound();

        var conteudo = _excelService.GerarPedidoExcel(pedido.Processo.Nome, pedido.Fornecedor, pedido.ItensPedido);
        var nomeFicheiro = $"Pedido_{pedido.Fornecedor}_{pedido.Processo.Nome}.xlsx".Replace(" ", "_");
        return File(conteudo, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nomeFicheiro);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ImportarResposta(int id, IFormFile ficheiro)
    {
        var pedido = _db.Pedidos
            .Include(p => p.Processo)
            .Include(p => p.ItensPedido).ThenInclude(ip => ip.ItemMaterial)
            .FirstOrDefault(p => p.Id == id);

        if (pedido == null) return NotFound();

        if (ficheiro == null || ficheiro.Length == 0)
        {
            TempData["EmailWarning"] = "Selecione o ficheiro Excel preenchido pelo fornecedor.";
            return RedirectToAction(nameof(Index), new { processoId = pedido.ProcessoId });
        }

        try
        {
            using var stream = ficheiro.OpenReadStream();
            var linhas = _excelService.LerRespostaExcel(stream);

            var proposta = new Proposta
            {
                ProcessoId = pedido.ProcessoId,
                PedidoPropostaId = pedido.Id,
                Fornecedor = pedido.Fornecedor,
                Status = StatusProposta.Recebida
            };
            _db.Propostas.Add(proposta);

            // GroupBy+First (not ToDictionary): defensive against a Pedido that
            // somehow ended up with two ItensPedido for the same ItemMaterial.
            var itensPorNome = pedido.ItensPedido
                .GroupBy(ip => ip.ItemMaterial.NomeItem.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var naoReconhecidos = new List<string>();
            var criados = 0;

            foreach (var linha in linhas)
            {
                decimal? quantidadeSolicitada = null;
                int? itemMaterialId = null;

                if (itensPorNome.TryGetValue(linha.NomeItem, out var itemPedido))
                {
                    quantidadeSolicitada = itemPedido.QuantidadeSolicitada;
                    itemMaterialId = itemPedido.ItemMaterialId;
                }
                else
                {
                    // Supplier quoted something outside the original request — accepted anyway,
                    // matched against the shared catalog by name, just without a requested quantity.
                    var itemCatalogo = _db.ItensMaterial.FirstOrDefault(im => im.NomeItem == linha.NomeItem);
                    if (itemCatalogo == null)
                    {
                        naoReconhecidos.Add(linha.NomeItem);
                        continue;
                    }
                    itemMaterialId = itemCatalogo.Id;
                }

                var quantidadeFornecida = linha.QuantidadeFornecida ?? 0;
                var preco = linha.PrecoUnitario ?? 0;

                proposta.ItensProposta.Add(new ItemProposta
                {
                    ItemMaterialId = itemMaterialId.Value,
                    QuantidadeSolicitada = quantidadeSolicitada,
                    Quantidade = quantidadeFornecida,
                    PrecoUnitario = preco,
                    Observacao = linha.Observacao,
                    Incluido = quantidadeFornecida > 0 || preco > 0
                });
                criados++;
            }

            proposta.ValorTotal = proposta.ItensProposta.Sum(i => i.Subtotal);
            pedido.Status = StatusPedido.Respondido;
            _db.SaveChanges();

            var mensagem = $"Proposta criada a partir da resposta: {criados} item(ns) importado(s).";
            if (naoReconhecidos.Count > 0)
                mensagem += $" Não reconhecidos (ignorados): {string.Join(", ", naoReconhecidos)}.";
            TempData["Sucesso"] = mensagem;

            return RedirectToAction("Index", "ItensProposta", new { propostaId = proposta.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao importar resposta do pedido {PedidoId}.", id);
            TempData["EmailWarning"] = "Não foi possível ler o ficheiro Excel. Verifique se é o modelo descarregado do sistema.";
            return RedirectToAction(nameof(Index), new { processoId = pedido.ProcessoId });
        }
    }
}
