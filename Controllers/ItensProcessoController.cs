using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Services;

namespace ComparacaoPropostas.Controllers;

// Importing the supplier's filled-in response stays scoped to the Processo (not the Pedido)
// because the resulting Proposta always belongs to a Processo — that's where evaluation
// happens. Requesting/adding items to ask for lives on the Pedido now (ItensPedidoController).
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

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ImportarResposta(int processoId, IFormFile ficheiro)
    {
        var processo = _db.Processos
            .Include(p => p.PedidoProposta).ThenInclude(pp => pp.ItensPedido).ThenInclude(ip => ip.ItemMaterial)
            .FirstOrDefault(p => p.Id == processoId);

        if (processo == null) return NotFound();

        if (ficheiro == null || ficheiro.Length == 0)
        {
            TempData["EmailWarning"] = "Selecione o ficheiro Excel preenchido pelo fornecedor.";
            return RedirectToAction("Index", "ItensPedido", new { pedidoId = processo.PedidoPropostaId });
        }

        try
        {
            using var stream = ficheiro.OpenReadStream();
            var linhas = _excelService.LerRespostaExcel(stream);

            var proposta = new Proposta
            {
                ProcessoId = processo.Id,
                Fornecedor = string.IsNullOrWhiteSpace(processo.Fornecedor) ? "Fornecedor" : processo.Fornecedor,
                Status = StatusProposta.Recebida
            };
            _db.Propostas.Add(proposta);

            // GroupBy+First (not ToDictionary): defensive against the Pedido somehow
            // ending up with two ItensPedido for the same ItemMaterial.
            var itensPorNome = processo.PedidoProposta.ItensPedido
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
            return RedirectToAction("Index", "ItensPedido", new { pedidoId = processo.PedidoPropostaId });
        }
    }
}
