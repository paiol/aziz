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

        var itens = proposta.ItensProposta.OrderBy(ip => ip.NomeExibicao).ToList();
        var incluidos = itens.Where(i => i.Incluido).ToList();

        var indexVm = new ItensPropostaIndexVM
        {
            Proposta = proposta,
            Itens = itens,
            ResumoPorItem = incluidos
                .GroupBy(i => i.NomeExibicao)
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

            var itensPedido = proposta.Processo.PedidoProposta.ItensPedido;
            var itensPorNome = itensPedido
                .GroupBy(ip => ip.ItemMaterial.NomeItem.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var existentesPorItemMaterial = proposta.ItensProposta
                .Where(i => i.ItemMaterialId.HasValue)
                .ToDictionary(i => i.ItemMaterialId!.Value);

            var pendentes = new List<ItemPendenteVM>();
            var atualizados = 0;
            var criados = 0;

            foreach (var linha in linhas)
            {
                // Nome não corresponde a nenhum item pedido neste processo — em vez de
                // importar às cegas (ou ignorar), pedimos confirmação ao utilizador.
                if (!itensPorNome.TryGetValue(linha.NomeItem.Trim(), out var itemPedido))
                {
                    pendentes.Add(new ItemPendenteVM
                    {
                        NomeItem = linha.NomeItem,
                        QuantidadeFornecida = linha.QuantidadeFornecida ?? 0,
                        PrecoUnitario = linha.PrecoUnitario ?? 0,
                        Observacao = linha.Observacao,
                        SugestaoItemPedidoId = MelhorSugestao(linha.NomeItem, itensPedido)
                    });
                    continue;
                }

                var quantidadeFornecida = linha.QuantidadeFornecida ?? 0;
                var preco = linha.PrecoUnitario ?? 0;

                if (existentesPorItemMaterial.TryGetValue(itemPedido.ItemMaterialId, out var existente))
                {
                    existente.Quantidade = quantidadeFornecida;
                    existente.PrecoUnitario = preco;
                    if (!string.IsNullOrWhiteSpace(linha.Observacao)) existente.Observacao = linha.Observacao;
                    existente.Incluido = quantidadeFornecida > 0 || preco > 0;
                    atualizados++;
                }
                else
                {
                    var novo = new ItemProposta
                    {
                        PropostaId = propostaId,
                        ItemMaterialId = itemPedido.ItemMaterialId,
                        QuantidadeSolicitada = itemPedido.QuantidadeSolicitada,
                        Quantidade = quantidadeFornecida,
                        PrecoUnitario = preco,
                        Observacao = linha.Observacao,
                        Incluido = quantidadeFornecida > 0 || preco > 0,
                        NaoSolicitado = false
                    };
                    _db.ItensProposta.Add(novo);
                    existentesPorItemMaterial[itemPedido.ItemMaterialId] = novo;
                    criados++;
                }
            }

            _db.SaveChanges();
            RecalcularTotalProposta(propostaId);

            if (pendentes.Count > 0)
            {
                var vm = new ConfirmarImportacaoVM
                {
                    PropostaId = propostaId,
                    PropostaFornecedor = proposta.Fornecedor,
                    Itens = pendentes,
                    ItensPedidoDisponiveis = itensPedido
                        .Select(ip => new OpcaoItemPedidoVM
                        {
                            ItemPedidoId = ip.Id,
                            ItemMaterialId = ip.ItemMaterialId,
                            NomeItem = ip.ItemMaterial.NomeItem
                        })
                        .OrderBy(o => o.NomeItem)
                        .ToList()
                };

                TempData["EmailWarning"] = $"{atualizados + criados} item(ns) importado(s) diretamente. " +
                    $"{pendentes.Count} item(ns) do ficheiro têm um nome diferente de qualquer item pedido — confirme abaixo se é o mesmo item ou um item novo.";

                return View("ConfirmarImportacao", vm);
            }

            TempData["Sucesso"] = $"Ficheiro importado: {atualizados} item(ns) atualizado(s), {criados} novo(s).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao importar Excel para a proposta {PropostaId}.", propostaId);
            TempData["EmailWarning"] = "Não foi possível ler o ficheiro Excel. Verifique se é o modelo descarregado do sistema.";
        }

        return RedirectToAction(nameof(Index), new { propostaId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ConfirmarImportacao(ConfirmarImportacaoVM model)
    {
        var proposta = _db.Propostas
            .Include(p => p.ItensProposta)
            .Include(p => p.Processo).ThenInclude(pr => pr.PedidoProposta).ThenInclude(pp => pp.ItensPedido).ThenInclude(ip => ip.ItemMaterial)
            .FirstOrDefault(p => p.Id == model.PropostaId);

        if (proposta == null) return NotFound();

        var itensPedidoPorId = proposta.Processo.PedidoProposta.ItensPedido.ToDictionary(ip => ip.Id);
        var existentesPorItemMaterial = proposta.ItensProposta
            .Where(i => i.ItemMaterialId.HasValue)
            .ToDictionary(i => i.ItemMaterialId!.Value);

        var confirmadosMesmoItem = 0;
        var adicionadosNaoSolicitados = 0;

        foreach (var linha in model.Itens ?? new())
        {
            if (linha.EscolhaItemPedidoId.HasValue && itensPedidoPorId.TryGetValue(linha.EscolhaItemPedidoId.Value, out var itemPedido))
            {
                // Confirmado: é o mesmo item pedido, só o nome veio diferente no Excel.
                if (existentesPorItemMaterial.TryGetValue(itemPedido.ItemMaterialId, out var existente))
                {
                    existente.Quantidade = linha.QuantidadeFornecida;
                    existente.PrecoUnitario = linha.PrecoUnitario;
                    if (!string.IsNullOrWhiteSpace(linha.Observacao)) existente.Observacao = linha.Observacao;
                    existente.Incluido = linha.QuantidadeFornecida > 0 || linha.PrecoUnitario > 0;
                }
                else
                {
                    var novo = new ItemProposta
                    {
                        PropostaId = model.PropostaId,
                        ItemMaterialId = itemPedido.ItemMaterialId,
                        QuantidadeSolicitada = itemPedido.QuantidadeSolicitada,
                        Quantidade = linha.QuantidadeFornecida,
                        PrecoUnitario = linha.PrecoUnitario,
                        Observacao = linha.Observacao,
                        Incluido = linha.QuantidadeFornecida > 0 || linha.PrecoUnitario > 0,
                        NaoSolicitado = false
                    };
                    _db.ItensProposta.Add(novo);
                    existentesPorItemMaterial[itemPedido.ItemMaterialId] = novo;
                }
                confirmadosMesmoItem++;
            }
            else
            {
                // Confirmado: é mesmo um item diferente, não fazia parte do pedido.
                // Reaproveita o item do catálogo geral se já existir com este nome
                // exato; caso contrário guarda só o nome, sem criar item novo no catálogo.
                var itemCatalogo = _db.ItensMaterial.FirstOrDefault(im => im.NomeItem == linha.NomeItem);

                _db.ItensProposta.Add(new ItemProposta
                {
                    PropostaId = model.PropostaId,
                    ItemMaterialId = itemCatalogo?.Id,
                    NomeItemLivre = itemCatalogo == null ? linha.NomeItem : null,
                    Quantidade = linha.QuantidadeFornecida,
                    PrecoUnitario = linha.PrecoUnitario,
                    Observacao = linha.Observacao,
                    Incluido = linha.QuantidadeFornecida > 0 || linha.PrecoUnitario > 0,
                    NaoSolicitado = true
                });
                adicionadosNaoSolicitados++;
            }
        }

        _db.SaveChanges();
        RecalcularTotalProposta(model.PropostaId);

        TempData["Sucesso"] = $"{confirmadosMesmoItem} item(ns) associado(s) ao pedido (nomenclatura diferente), " +
            $"{adicionadosNaoSolicitados} item(ns) adicionado(s) como não solicitado(s).";

        return RedirectToAction(nameof(Index), new { propostaId = model.PropostaId });
    }

    private static int? MelhorSugestao(string nomeItem, IEnumerable<ItemPedido> itensPedido)
    {
        ItemPedido? melhor = null;
        var melhorScore = 0.0;

        foreach (var ip in itensPedido)
        {
            var score = TextSimilarityHelper.Similaridade(nomeItem, ip.ItemMaterial.NomeItem);
            if (score > melhorScore)
            {
                melhorScore = score;
                melhor = ip;
            }
        }

        return melhorScore >= 0.5 ? melhor?.Id : null;
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
