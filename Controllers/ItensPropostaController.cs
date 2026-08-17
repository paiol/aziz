using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Helper;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Services;
using ComparacaoPropostas.ViewModels.ItensProposta;

namespace ComparacaoPropostas.Controllers;

public class ItensPropostaController : Controller
{
    private readonly AppDbContext _db;
    private readonly IPropostaExcelService _excelService;
    private readonly ILogger<ItensPropostaController> _logger;

    public ItensPropostaController(AppDbContext db, IPropostaExcelService excelService, ILogger<ItensPropostaController> logger)
    {
        _db = db;
        _excelService = excelService;
        _logger = logger;
    }

    private void RecalcularTotalProposta(int propostaId)
    {
        var proposta = _db.Propostas.Find(propostaId);
        if (proposta == null) return;

        proposta.ValorTotal = _db.ItensProposta
            .Where(i => i.PropostaId == propostaId && i.Incluido)
            .Sum(i => i.Quantidade * i.PrecoUnitario);

        _db.SaveChanges();
    }

    public IActionResult Index(int propostaId)
    {
        var proposta = _db.Propostas
            .Include(p => p.ItensProposta).ThenInclude(ip => ip.ItemMaterial)
            .FirstOrDefault(p => p.Id == propostaId);

        if (proposta == null) return NotFound();

        var itens = proposta.ItensProposta.OrderBy(ip => ip.ItemMaterial.NomeItem).ToList();
        var incluidos = itens.Where(i => i.Incluido).ToList();

        var indexVm = new ItensPropostaIndexVM
        {
            Proposta = proposta,
            Itens = itens,
            ResumoPorItem = incluidos
                .GroupBy(i => i.ItemMaterial.NomeItem)
                .Select(g => new ResumoItemVM
                {
                    NomeItem = g.Key,
                    QuantidadeTotal = g.Sum(i => i.Quantidade),
                    ValorTotal = g.Sum(i => i.Subtotal)
                })
                .OrderBy(r => r.NomeItem)
                .ToList(),
            QuantidadeGeral = incluidos.Sum(i => i.Quantidade),
            ValorGeral = incluidos.Sum(i => i.Subtotal)
        };

        return View(indexVm);
    }

    public IActionResult Create(int propostaId)
    {
        var proposta = _db.Propostas.Include(p => p.Processo).FirstOrDefault(p => p.Id == propostaId);
        if (proposta == null) return NotFound();

        ViewBag.PropostaFornecedor = proposta.Fornecedor;
        ViewBag.Itens = CatalogoItensHelper.CarregarIndentado(_db);
        ViewBag.Dominios = CatalogoItensHelper.ObterDominios(_db);
        return View(new NovosItensPropostaVM { PropostaId = propostaId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(NovosItensPropostaVM model)
    {
        var proposta = _db.Propostas.Include(p => p.Processo).FirstOrDefault(p => p.Id == model.PropostaId);
        if (proposta == null) return NotFound();

        var linhasValidas = (model.Itens ?? new()).Where(i => i.ItemMaterialId > 0).ToList();
        if (linhasValidas.Count == 0)
        {
            ModelState.AddModelError("", "Adicione pelo menos uma linha de item.");
        }

        if (!ModelState.IsValid)
        {
            ViewBag.PropostaFornecedor = proposta.Fornecedor;
            ViewBag.Itens = CatalogoItensHelper.CarregarIndentado(_db);
            ViewBag.Dominios = CatalogoItensHelper.ObterDominios(_db);
            return View(model);
        }

        foreach (var linha in linhasValidas)
        {
            linha.PropostaId = model.PropostaId;
            _db.ItensProposta.Add(linha);
        }
        _db.SaveChanges();

        RecalcularTotalProposta(model.PropostaId);

        TempData["Sucesso"] = $"{linhasValidas.Count} item(ns) adicionado(s) à proposta.";
        return RedirectToAction(nameof(Index), new { propostaId = model.PropostaId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ImportarExcel(int propostaId, IFormFile ficheiro)
    {
        var proposta = _db.Propostas
            .Include(p => p.ItensProposta)
            .Include(p => p.Processo).ThenInclude(pr => pr.PedidoProposta).ThenInclude(pp => pp.ItensPedido).ThenInclude(ip => ip.ItemMaterial)
            .FirstOrDefault(p => p.Id == propostaId);

        if (proposta == null) return NotFound();

        if (ficheiro == null || ficheiro.Length == 0)
        {
            TempData["EmailWarning"] = "Selecione o ficheiro Excel preenchido pelo fornecedor.";
            return RedirectToAction(nameof(Index), new { propostaId });
        }

        try
        {
            using var stream = ficheiro.OpenReadStream();
            var linhas = _excelService.LerRespostaExcel(stream);

            var itensPorNome = proposta.Processo.PedidoProposta.ItensPedido
                .GroupBy(ip => ip.ItemMaterial.NomeItem.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var existentesPorItemMaterial = proposta.ItensProposta.ToDictionary(i => i.ItemMaterialId);

            var naoReconhecidos = new List<string>();
            var atualizados = 0;
            var criados = 0;

            foreach (var linha in linhas)
            {
                int? itemMaterialId;
                decimal? quantidadeSolicitada = null;

                if (itensPorNome.TryGetValue(linha.NomeItem.Trim(), out var itemPedido))
                {
                    itemMaterialId = itemPedido.ItemMaterialId;
                    quantidadeSolicitada = itemPedido.QuantidadeSolicitada;
                }
                else
                {
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

                if (existentesPorItemMaterial.TryGetValue(itemMaterialId.Value, out var existente))
                {
                    existente.Quantidade = quantidadeFornecida;
                    existente.PrecoUnitario = preco;
                    if (!string.IsNullOrWhiteSpace(linha.Observacao)) existente.Observacao = linha.Observacao;
                    existente.Incluido = quantidadeFornecida > 0 || preco > 0;
                    atualizados++;
                }
                else
                {
                    _db.ItensProposta.Add(new ItemProposta
                    {
                        PropostaId = propostaId,
                        ItemMaterialId = itemMaterialId.Value,
                        QuantidadeSolicitada = quantidadeSolicitada,
                        Quantidade = quantidadeFornecida,
                        PrecoUnitario = preco,
                        Observacao = linha.Observacao,
                        Incluido = quantidadeFornecida > 0 || preco > 0
                    });
                    criados++;
                }
            }

            _db.SaveChanges();
            RecalcularTotalProposta(propostaId);

            var mensagem = $"Ficheiro importado: {atualizados} item(ns) atualizado(s), {criados} novo(s).";
            if (naoReconhecidos.Count > 0)
                mensagem += $" Não reconhecidos (ignorados): {string.Join(", ", naoReconhecidos)}.";
            TempData["Sucesso"] = mensagem;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao importar Excel para a proposta {PropostaId}.", propostaId);
            TempData["EmailWarning"] = "Não foi possível ler o ficheiro Excel. Verifique se é o modelo descarregado do sistema.";
        }

        return RedirectToAction(nameof(Index), new { propostaId });
    }

    public IActionResult Edit(int id)
    {
        var item = _db.ItensProposta.Find(id);
        if (item == null) return NotFound();

        var proposta = _db.Propostas.Include(p => p.Processo).First(p => p.Id == item.PropostaId);
        ViewBag.PropostaFornecedor = proposta.Fornecedor;
        ViewBag.Itens = CatalogoItensHelper.CarregarIndentado(_db);
        return View(item);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, ItemProposta item)
    {
        if (id != item.Id) return NotFound();
        if (!ModelState.IsValid)
        {
            var proposta = _db.Propostas.Include(p => p.Processo).First(p => p.Id == item.PropostaId);
            ViewBag.PropostaFornecedor = proposta.Fornecedor;
            ViewBag.Itens = CatalogoItensHelper.CarregarIndentado(_db);
            return View(item);
        }

        _db.ItensProposta.Update(item);
        _db.SaveChanges();

        RecalcularTotalProposta(item.PropostaId);

        TempData["Sucesso"] = "Item atualizado.";
        return RedirectToAction(nameof(Index), new { propostaId = item.PropostaId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var item = _db.ItensProposta.Find(id);
        if (item == null) return NotFound();

        var propostaId = item.PropostaId;
        _db.ItensProposta.Remove(item);
        _db.SaveChanges();

        RecalcularTotalProposta(propostaId);

        TempData["Sucesso"] = "Item removido.";
        return RedirectToAction(nameof(Index), new { propostaId });
    }
}
