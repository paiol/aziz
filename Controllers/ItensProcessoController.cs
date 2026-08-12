using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
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
        ViewBag.AreaSugerida = processo.PedidoProposta.Area;
        return View(new NovosItensProcessoVM { ProcessoId = processoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(NovosItensProcessoVM model)
    {
        var processo = _db.Processos.Include(p => p.PedidoProposta).FirstOrDefault(p => p.Id == model.ProcessoId);
        if (processo == null) return NotFound();

        var linhasValidas = (model.Itens ?? new()).Where(i => !string.IsNullOrWhiteSpace(i.ChaveItem)).ToList();
        if (linhasValidas.Count == 0)
            ModelState.AddModelError("", "Adicione pelo menos uma linha de item (escolha um item da lista de sugestões).");

        if (!ModelState.IsValid)
        {
            ViewBag.ProcessoNome = processo.Nome;
            ViewBag.AreaSugerida = processo.PedidoProposta.Area;
            return View(model);
        }

        var adicionados = 0;
        foreach (var linha in linhasValidas)
        {
            var itemMaterialId = ResolverChaveItem(linha.ChaveItem);
            if (itemMaterialId == null) continue;

            _db.ItensPedido.Add(new ItemPedido
            {
                ProcessoId = model.ProcessoId,
                ItemMaterialId = itemMaterialId.Value,
                QuantidadeSolicitada = linha.QuantidadeSolicitada,
                Observacao = linha.Observacao
            });
            adicionados++;
        }
        _db.SaveChanges();

        TempData["Sucesso"] = $"{adicionados} item(ns) adicionado(s) ao pedido.";
        return RedirectToAction(nameof(Index), new { processoId = model.ProcessoId });
    }

    // Cross-catalog typeahead used by the item picker: matches by name across the shared
    // ItemMaterial catalog and the 4 domain-specific tables (Energia/MBB/FBB/Core). With no
    // "termo", falls back to listing items tagged with "dominio" so the picker can show a
    // starter list for the Processo's suggested Área on focus.
    [HttpGet]
    public IActionResult BuscarItens(string? termo, string? dominio)
    {
        termo = (termo ?? "").Trim();
        dominio = (dominio ?? "").Trim();

        if (termo.Length < 2 && dominio.Length == 0)
            return Json(new List<ItemBuscaResultado>());

        var resultados = new List<ItemBuscaResultado>();

        resultados.AddRange(_db.ItensMaterial.Where(i => i.ItemPaiId == null).ToList()
            .Where(i => CorrespondeAoFiltro(i.NomeItem, i.Dominio, termo, dominio))
            .Take(10).Select(i => new ItemBuscaResultado { Chave = $"material:{i.Id}", Nome = i.NomeItem, Categoria = i.Categoria, Unidade = i.Unidade, Dominio = i.Dominio, Origem = "Catálogo" }));

        resultados.AddRange(_db.ItensEnergia.ToList()
            .Where(i => CorrespondeAoFiltro(i.Nome, i.Dominio, termo, dominio))
            .Take(10).Select(i => new ItemBuscaResultado { Chave = $"energia:{i.Id}", Nome = i.Nome, Categoria = i.Categoria, Unidade = i.Unidade, Dominio = i.Dominio, Origem = "Energia" }));

        resultados.AddRange(_db.ItensMbb.ToList()
            .Where(i => CorrespondeAoFiltro(i.Nome, i.Dominio, termo, dominio))
            .Take(10).Select(i => new ItemBuscaResultado { Chave = $"mbb:{i.Id}", Nome = i.Nome, Categoria = i.Categoria, Unidade = i.Unidade, Dominio = i.Dominio, Origem = "MBB" }));

        resultados.AddRange(_db.ItensFbb.ToList()
            .Where(i => CorrespondeAoFiltro(i.Nome, i.Dominio, termo, dominio))
            .Take(10).Select(i => new ItemBuscaResultado { Chave = $"fbb:{i.Id}", Nome = i.Nome, Categoria = i.Categoria, Unidade = i.Unidade, Dominio = i.Dominio, Origem = "FBB" }));

        resultados.AddRange(_db.ItensCore.ToList()
            .Where(i => CorrespondeAoFiltro(i.Nome, i.Dominio, termo, dominio))
            .Take(10).Select(i => new ItemBuscaResultado { Chave = $"core:{i.Id}", Nome = i.Nome, Categoria = i.Categoria, Unidade = i.Unidade, Dominio = i.Dominio, Origem = "Core" }));

        return Json(resultados.OrderBy(r => r.Nome).Take(30));
    }

    private static bool CorrespondeAoFiltro(string nomeItem, string? dominioItem, string termo, string dominio)
    {
        if (termo.Length >= 2) return nomeItem.Contains(termo, StringComparison.OrdinalIgnoreCase);
        if (dominio.Length > 0) return string.Equals(dominioItem, dominio, StringComparison.OrdinalIgnoreCase);
        return false;
    }

    // Bridges a pick from any of the 4 domain catalogs into the shared ItemMaterial table
    // (found-or-created by name) so ItemPedido/ItemProposta/Excel/Comparação keep working
    // against a single catalog, exactly as before this picker existed.
    private int? ResolverChaveItem(string chave)
    {
        var partes = chave.Split(':', 2);
        if (partes.Length != 2 || !int.TryParse(partes[1], out var id)) return null;

        if (partes[0] == "material")
            return _db.ItensMaterial.Any(m => m.Id == id) ? id : null;

        string nome, dominioPadrao;
        string? categoria, unidade, dominio;

        switch (partes[0])
        {
            case "energia":
                var e = _db.ItensEnergia.Find(id);
                if (e == null) return null;
                (nome, categoria, unidade, dominio, dominioPadrao) = (e.Nome, e.Categoria, e.Unidade, e.Dominio, "Energia");
                break;
            case "mbb":
                var m = _db.ItensMbb.Find(id);
                if (m == null) return null;
                (nome, categoria, unidade, dominio, dominioPadrao) = (m.Nome, m.Categoria, m.Unidade, m.Dominio, "MBB");
                break;
            case "fbb":
                var f = _db.ItensFbb.Find(id);
                if (f == null) return null;
                (nome, categoria, unidade, dominio, dominioPadrao) = (f.Nome, f.Categoria, f.Unidade, f.Dominio, "FBB");
                break;
            case "core":
                var c = _db.ItensCore.Find(id);
                if (c == null) return null;
                (nome, categoria, unidade, dominio, dominioPadrao) = (c.Nome, c.Categoria, c.Unidade, c.Dominio, "Core");
                break;
            default:
                return null;
        }

        var existente = _db.ItensMaterial.FirstOrDefault(im => im.NomeItem == nome);
        if (existente != null) return existente.Id;

        var novo = new ItemMaterial
        {
            NomeItem = nome,
            Categoria = categoria,
            Unidade = unidade,
            Dominio = string.IsNullOrWhiteSpace(dominio) ? dominioPadrao : dominio
        };
        _db.ItensMaterial.Add(novo);
        _db.SaveChanges();
        return novo.Id;
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
