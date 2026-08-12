using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Helper;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Services;
using ComparacaoPropostas.ViewModels.ItensProcesso;

namespace ComparacaoPropostas.Controllers;

public class ItensProcessoController : Controller
{
    private readonly AppDbContext _db;
    private readonly IPropostaExcelService _excelService;
    private readonly ILogger<ItensProcessoController> _logger;

    public ItensProcessoController(AppDbContext db, IPropostaExcelService excelService, ILogger<ItensProcessoController> logger)
    {
        _db = db;
        _excelService = excelService;
        _logger = logger;
    }

    public IActionResult Index(int processoId)
    {
        var processo = _db.Processos
            .Include(p => p.PedidoProposta)
            .Include(p => p.ItensPedido).ThenInclude(ip => ip.ItemMaterial)
            .FirstOrDefault(p => p.Id == processoId);

        if (processo == null) return NotFound();

        ViewBag.Processo = processo;
        return View(processo.ItensPedido.OrderBy(ip => ip.ItemMaterial.NomeItem).ToList());
    }

    public IActionResult Create(int processoId)
    {
        var processo = _db.Processos.Include(p => p.PedidoProposta).FirstOrDefault(p => p.Id == processoId);
        if (processo == null) return NotFound();

        ViewBag.ProcessoNome = processo.Nome;
        ViewBag.Itens = CatalogoItensHelper.CarregarIndentado(_db);
        ViewBag.Dominios = CatalogoItensHelper.ObterDominios(_db);
        ViewBag.AreaSugerida = processo.PedidoProposta.Area;
        return View(new NovosItensProcessoVM { ProcessoId = processoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(NovosItensProcessoVM model)
    {
        var processo = _db.Processos.Find(model.ProcessoId);
        if (processo == null) return NotFound();

        var linhasValidas = (model.Itens ?? new()).Where(i => i.ItemMaterialId > 0).ToList();
        if (linhasValidas.Count == 0)
            ModelState.AddModelError("", "Adicione pelo menos uma linha de item.");

        if (!ModelState.IsValid)
        {
            ViewBag.ProcessoNome = processo.Nome;
            ViewBag.Itens = CatalogoItensHelper.CarregarIndentado(_db);
            ViewBag.Dominios = CatalogoItensHelper.ObterDominios(_db);
            return View(model);
        }

        foreach (var linha in linhasValidas)
        {
            linha.ProcessoId = model.ProcessoId;
            _db.ItensPedido.Add(linha);
        }
        _db.SaveChanges();

        TempData["Sucesso"] = $"{linhasValidas.Count} item(ns) adicionado(s) ao pedido.";
        return RedirectToAction(nameof(Index), new { processoId = model.ProcessoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var item = _db.ItensPedido.Find(id);
        if (item == null) return NotFound();

        var processoId = item.ProcessoId;
        _db.ItensPedido.Remove(item);
        _db.SaveChanges();
        TempData["Sucesso"] = "Item removido.";
        return RedirectToAction(nameof(Index), new { processoId });
    }

    public IActionResult ExportarExcel(int processoId)
    {
        var processo = _db.Processos
            .Include(p => p.ItensPedido).ThenInclude(ip => ip.ItemMaterial)
            .FirstOrDefault(p => p.Id == processoId);

        if (processo == null) return NotFound();

        if (processo.ItensPedido.Count == 0)
        {
            TempData["EmailWarning"] = "Adicione pelo menos um item antes de descarregar o Excel.";
            return RedirectToAction(nameof(Index), new { processoId });
        }

        var conteudo = _excelService.GerarPedidoExcel(processo.Nome, processo.Fornecedor, processo.ItensPedido);
        var nomeFicheiro = $"Pedido_{processo.Fornecedor}_{processo.Nome}.xlsx".Replace(" ", "_");
        return File(conteudo, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nomeFicheiro);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ImportarResposta(int processoId, IFormFile ficheiro)
    {
        var processo = _db.Processos
            .Include(p => p.ItensPedido).ThenInclude(ip => ip.ItemMaterial)
            .FirstOrDefault(p => p.Id == processoId);

        if (processo == null) return NotFound();

        if (ficheiro == null || ficheiro.Length == 0)
        {
            TempData["EmailWarning"] = "Selecione o ficheiro Excel preenchido pelo fornecedor.";
            return RedirectToAction(nameof(Index), new { processoId });
        }

        try
        {
            using var stream = ficheiro.OpenReadStream();
            var linhas = _excelService.LerRespostaExcel(stream);

            var proposta = new Proposta
            {
                ProcessoId = processo.Id,
                Fornecedor = processo.Fornecedor,
                Status = StatusProposta.Recebida
            };
            _db.Propostas.Add(proposta);

            // GroupBy+First (not ToDictionary): defensive against the Processo somehow
            // ending up with two ItensPedido for the same ItemMaterial.
            var itensPorNome = processo.ItensPedido
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
            _db.SaveChanges();

            var mensagem = $"Proposta criada a partir da resposta: {criados} item(ns) importado(s).";
            if (naoReconhecidos.Count > 0)
                mensagem += $" Não reconhecidos (ignorados): {string.Join(", ", naoReconhecidos)}.";
            TempData["Sucesso"] = mensagem;

            return RedirectToAction("Index", "ItensProposta", new { propostaId = proposta.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao importar resposta do processo {ProcessoId}.", processoId);
            TempData["EmailWarning"] = "Não foi possível ler o ficheiro Excel. Verifique se é o modelo descarregado do sistema.";
            return RedirectToAction(nameof(Index), new { processoId });
        }
    }
}
