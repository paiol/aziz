using Microsoft.AspNetCore.Mvc;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Helper;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Services;
using ComparacaoPropostas.ViewModels.PropostasEmpreiteiro;

namespace ComparacaoPropostas.Controllers;

public class ItensPropostaEmpreiteiroController : Controller
{
    private readonly AppDbContext _db;
    private readonly IMqtExcelService _mqtExcelService;
    private readonly ILogger<ItensPropostaEmpreiteiroController> _logger;

    public ItensPropostaEmpreiteiroController(AppDbContext db, IMqtExcelService mqtExcelService, ILogger<ItensPropostaEmpreiteiroController> logger)
    {
        _db = db;
        _mqtExcelService = mqtExcelService;
        _logger = logger;
    }

    public IActionResult Index(int propostaEmpreiteiroId)
    {
        var proposta = _db.PropostasEmpreiteiro.Find(propostaEmpreiteiroId);
        if (proposta == null) return NotFound();

        var itens = _db.ItensPropostaEmpreiteiro
            .Where(i => i.PropostaEmpreiteiroId == propostaEmpreiteiroId)
            .ToList();

        foreach (var item in itens)
            item.ItemMQT = _db.ItensMQT.Find(item.ItemMQTId)!;

        ViewBag.PropostaEmpreiteiro = proposta;
        return View(itens.OrderBy(i => i.ItemMQT.CodigoIndexacao).ThenBy(i => i.ItemMQT.Descricao).ToList());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditarTodos(int propostaEmpreiteiroId, List<int> itemId, List<decimal> quantidade, List<decimal> preco)
    {
        var proposta = _db.PropostasEmpreiteiro.Find(propostaEmpreiteiroId);
        if (proposta == null) return NotFound();

        var itens = _db.ItensPropostaEmpreiteiro
            .Where(i => i.PropostaEmpreiteiroId == propostaEmpreiteiroId)
            .ToDictionary(i => i.Id);

        for (var i = 0; i < itemId.Count; i++)
        {
            if (!itens.TryGetValue(itemId[i], out var item)) continue;
            item.QuantidadeFornecida = i < quantidade.Count ? quantidade[i] : 0;
            item.PrecoUnitario = i < preco.Count ? preco[i] : 0;
            item.Incluido = item.QuantidadeFornecida > 0 || item.PrecoUnitario > 0;
        }

        _db.SaveChanges();

        TempData["Sucesso"] = "Itens atualizados com sucesso.";
        return RedirectToAction(nameof(Index), new { propostaEmpreiteiroId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ExportarExcel(int propostaEmpreiteiroId)
    {
        var proposta = _db.PropostasEmpreiteiro.Find(propostaEmpreiteiroId);
        if (proposta == null) return NotFound();

        var projeto = _db.ProjetosObra.Find(proposta.ProjetoObraId);
        var itensMQT = _db.ItensMQT.Where(i => i.ProjetoObraId == proposta.ProjetoObraId).ToList();

        var bytes = _mqtExcelService.GerarModeloExcel(projeto?.Designacao ?? "", proposta.Empreiteiro, itensMQT);
        return File(bytes, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", $"MQT_{proposta.Empreiteiro}.xlsx");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ImportarExcel(int propostaEmpreiteiroId, IFormFile ficheiro)
    {
        var proposta = _db.PropostasEmpreiteiro.Find(propostaEmpreiteiroId);
        if (proposta == null) return NotFound();

        if (ficheiro == null || ficheiro.Length == 0)
        {
            TempData["EmailWarning"] = "Selecione o ficheiro Excel preenchido pelo empreiteiro.";
            return RedirectToAction(nameof(Index), new { propostaEmpreiteiroId });
        }

        try
        {
            using var stream = ficheiro.OpenReadStream();
            var linhas = _mqtExcelService.LerRespostaExcel(stream);

            var itensMQT = _db.ItensMQT.Where(i => i.ProjetoObraId == proposta.ProjetoObraId).ToList();
            var itensMQTPorNome = itensMQT
                .GroupBy(i => i.Descricao.Trim(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.First(), StringComparer.OrdinalIgnoreCase);

            var existentesPorItemMQT = _db.ItensPropostaEmpreiteiro
                .Where(i => i.PropostaEmpreiteiroId == propostaEmpreiteiroId)
                .ToDictionary(i => i.ItemMQTId);

            var pendentes = new List<ItemPendenteMQTVM>();
            var atualizados = 0;

            foreach (var linha in linhas)
            {
                if (!itensMQTPorNome.TryGetValue(linha.NomeItem.Trim(), out var itemMQT))
                {
                    pendentes.Add(new ItemPendenteMQTVM
                    {
                        NomeItem = linha.NomeItem,
                        QuantidadeFornecida = linha.QuantidadeFornecida ?? 0,
                        PrecoUnitario = linha.PrecoUnitario ?? 0,
                        SugestaoItemMQTId = MelhorSugestao(linha.NomeItem, itensMQT)
                    });
                    continue;
                }

                var quantidadeFornecida = linha.QuantidadeFornecida ?? 0;
                var preco = linha.PrecoUnitario ?? 0;

                if (existentesPorItemMQT.TryGetValue(itemMQT.Id, out var existente))
                {
                    existente.QuantidadeFornecida = quantidadeFornecida;
                    existente.PrecoUnitario = preco;
                    existente.Incluido = quantidadeFornecida > 0 || preco > 0;
                    atualizados++;
                }
            }

            _db.SaveChanges();

            if (pendentes.Count > 0)
            {
                var vm = new ConfirmarImportacaoMQTVM
                {
                    PropostaEmpreiteiroId = propostaEmpreiteiroId,
                    Empreiteiro = proposta.Empreiteiro,
                    Itens = pendentes,
                    ItensMQTDisponiveis = itensMQT
                        .Select(i => new OpcaoItemMQTVM { ItemMQTId = i.Id, Descricao = i.Descricao })
                        .OrderBy(o => o.Descricao)
                        .ToList()
                };

                TempData["EmailWarning"] = $"{atualizados} item(ns) importado(s) diretamente. " +
                    $"{pendentes.Count} item(ns) do ficheiro têm um nome diferente de qualquer item do MQT — confirme abaixo se é o mesmo item ou um item novo.";

                return View("ConfirmarImportacao", vm);
            }

            TempData["Sucesso"] = $"Ficheiro importado: {atualizados} item(ns) atualizado(s).";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao importar Excel para a proposta de empreiteiro {PropostaEmpreiteiroId}.", propostaEmpreiteiroId);
            TempData["EmailWarning"] = "Não foi possível ler o ficheiro Excel. Verifique se é o modelo descarregado do sistema.";
        }

        return RedirectToAction(nameof(Index), new { propostaEmpreiteiroId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ConfirmarImportacao(ConfirmarImportacaoMQTVM model)
    {
        var proposta = _db.PropostasEmpreiteiro.Find(model.PropostaEmpreiteiroId);
        if (proposta == null) return NotFound();

        var itensMQTPorId = _db.ItensMQT.Where(i => i.ProjetoObraId == proposta.ProjetoObraId).ToDictionary(i => i.Id);
        var existentesPorItemMQT = _db.ItensPropostaEmpreiteiro
            .Where(i => i.PropostaEmpreiteiroId == model.PropostaEmpreiteiroId)
            .ToDictionary(i => i.ItemMQTId);

        var confirmadosMesmoItem = 0;
        var adicionadosNaoPrevistos = 0;

        foreach (var linha in model.Itens ?? new())
        {
            int itemMQTId;

            if (linha.EscolhaItemMQTId.HasValue && itensMQTPorId.ContainsKey(linha.EscolhaItemMQTId.Value))
            {
                itemMQTId = linha.EscolhaItemMQTId.Value;
                confirmadosMesmoItem++;
            }
            else
            {
                var itemExistenteNoCatalogo = _db.ItensMQT.FirstOrDefault(i => i.ProjetoObraId == proposta.ProjetoObraId && i.Descricao == linha.NomeItem);
                if (itemExistenteNoCatalogo != null)
                {
                    itemMQTId = itemExistenteNoCatalogo.Id;
                }
                else
                {
                    var novoItemMQT = new ItemMQT
                    {
                        ProjetoObraId = proposta.ProjetoObraId,
                        Descricao = linha.NomeItem,
                        Quantidade = 0,
                        NaoPrevisto = true
                    };
                    _db.ItensMQT.Add(novoItemMQT);
                    _db.SaveChanges();
                    itemMQTId = novoItemMQT.Id;
                }
                adicionadosNaoPrevistos++;
            }

            if (existentesPorItemMQT.TryGetValue(itemMQTId, out var existente))
            {
                existente.QuantidadeFornecida = linha.QuantidadeFornecida;
                existente.PrecoUnitario = linha.PrecoUnitario;
                existente.Incluido = linha.QuantidadeFornecida > 0 || linha.PrecoUnitario > 0;
            }
            else
            {
                _db.ItensPropostaEmpreiteiro.Add(new ItemPropostaEmpreiteiro
                {
                    PropostaEmpreiteiroId = model.PropostaEmpreiteiroId,
                    ItemMQTId = itemMQTId,
                    QuantidadeFornecida = linha.QuantidadeFornecida,
                    PrecoUnitario = linha.PrecoUnitario,
                    Incluido = linha.QuantidadeFornecida > 0 || linha.PrecoUnitario > 0
                });
            }
        }

        _db.SaveChanges();

        TempData["Sucesso"] = $"{confirmadosMesmoItem} item(ns) associado(s) ao MQT (nomenclatura diferente), " +
            $"{adicionadosNaoPrevistos} item(ns) adicionado(s) como não previsto(s) no MQT.";

        return RedirectToAction(nameof(Index), new { propostaEmpreiteiroId = model.PropostaEmpreiteiroId });
    }

    private static int? MelhorSugestao(string nomeItem, IEnumerable<ItemMQT> itensMQT)
    {
        ItemMQT? melhor = null;
        var melhorScore = 0.0;

        foreach (var item in itensMQT)
        {
            var score = TextSimilarityHelper.Similaridade(nomeItem, item.Descricao);
            if (score > melhorScore)
            {
                melhorScore = score;
                melhor = item;
            }
        }

        return melhorScore >= 0.5 ? melhor?.Id : null;
    }
}
