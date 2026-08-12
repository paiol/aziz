using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
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

    private List<ItemMaterial> CarregarCatalogoIndentado(string? tipoProcesso = null)
    {
        var query = _db.ItensMaterial
            .Include(i => i.SubItens)
            .Where(i => i.ItemPaiId == null);

        if (!string.IsNullOrWhiteSpace(tipoProcesso))
            query = query.Where(i => string.IsNullOrEmpty(i.Dominio) || i.Dominio == tipoProcesso);

        var paisComFilhos = query.OrderBy(i => i.NomeItem).ToList();

        var lista = new List<ItemMaterial>();
        foreach (var pai in paisComFilhos)
        {
            lista.Add(pai);
            var filhos = pai.SubItens.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(tipoProcesso))
                filhos = filhos.Where(s => string.IsNullOrEmpty(s.Dominio) || s.Dominio == tipoProcesso);
            lista.AddRange(filhos.OrderBy(s => s.NomeItem));
        }
        return lista;
    }

    public IActionResult Create(int propostaId)
    {
        var proposta = _db.Propostas.Include(p => p.Processo).FirstOrDefault(p => p.Id == propostaId);
        if (proposta == null) return NotFound();

        ViewBag.PropostaFornecedor = proposta.Fornecedor;
        ViewBag.Itens = CarregarCatalogoIndentado(proposta.Processo.TipoProcesso);
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
            ViewBag.Itens = CarregarCatalogoIndentado(proposta.Processo.TipoProcesso);
            return View(model);
        }

        foreach (var linha in linhasValidas)
        {
            linha.PropostaId = model.PropostaId;
            // The quantity entered here is what's being requested from the supplier;
            // preserved separately so it can still be compared after the supplier's
            // filled Excel overwrites Quantidade/PrecoUnitario with what they actually offer.
            linha.QuantidadeSolicitada = linha.Quantidade;
            _db.ItensProposta.Add(linha);
        }
        _db.SaveChanges();

        TempData["Sucesso"] = $"{linhasValidas.Count} item(ns) adicionado(s) à proposta.";
        return RedirectToAction(nameof(Index), new { propostaId = model.PropostaId });
    }

    public IActionResult Edit(int id)
    {
        var item = _db.ItensProposta.Find(id);
        if (item == null) return NotFound();

        var proposta = _db.Propostas.Include(p => p.Processo).First(p => p.Id == item.PropostaId);
        ViewBag.PropostaFornecedor = proposta.Fornecedor;
        ViewBag.Itens = CarregarCatalogoIndentado(proposta.Processo.TipoProcesso);
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
            ViewBag.Itens = CarregarCatalogoIndentado(proposta.Processo.TipoProcesso);
            return View(item);
        }

        _db.ItensProposta.Update(item);
        _db.SaveChanges();
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
        TempData["Sucesso"] = "Item removido.";
        return RedirectToAction(nameof(Index), new { propostaId });
    }

    public IActionResult ExportarExcel(int propostaId)
    {
        var proposta = _db.Propostas
            .Include(p => p.Processo)
            .Include(p => p.ItensProposta).ThenInclude(ip => ip.ItemMaterial)
            .FirstOrDefault(p => p.Id == propostaId);

        if (proposta == null) return NotFound();

        if (proposta.ItensProposta.Count == 0)
        {
            TempData["EmailWarning"] = "Adicione pelo menos um item antes de descarregar o pedido em Excel.";
            return RedirectToAction(nameof(Index), new { propostaId });
        }

        var conteudo = _excelService.GerarPedidoExcel(proposta);
        var nomeFicheiro = $"Pedido_{proposta.Fornecedor}_{proposta.Processo.Nome}.xlsx".Replace(" ", "_");
        return File(conteudo, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", nomeFicheiro);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ImportarExcel(int propostaId, IFormFile ficheiro)
    {
        var proposta = _db.Propostas
            .Include(p => p.ItensProposta).ThenInclude(ip => ip.ItemMaterial)
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
            var resultado = _excelService.ImportarPropostaExcel(proposta, stream);

            var mensagem = $"{resultado.Atualizados} item(ns) atualizado(s), {resultado.Criados} novo(s) item(ns) importado(s).";
            if (resultado.NaoReconhecidos.Count > 0)
                mensagem += $" Não reconhecidos (ignorados): {string.Join(", ", resultado.NaoReconhecidos)}.";

            TempData["Sucesso"] = mensagem;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao importar Excel da proposta {PropostaId}.", propostaId);
            TempData["EmailWarning"] = "Não foi possível ler o ficheiro Excel. Verifique se é o modelo descarregado do sistema.";
        }

        return RedirectToAction(nameof(Index), new { propostaId });
    }
}
