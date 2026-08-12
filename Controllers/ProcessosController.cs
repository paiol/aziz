using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Services;
using ComparacaoPropostas.ViewModels.Processos;

namespace ComparacaoPropostas.Controllers;

public class ProcessosController : Controller
{
    private readonly AppDbContext _db;
    private readonly IScoringService _scoringService;
    private readonly IEmailService _emailService;
    private readonly ILogger<ProcessosController> _logger;

    public ProcessosController(AppDbContext db, IScoringService scoringService, IEmailService emailService, ILogger<ProcessosController> logger)
    {
        _db = db;
        _scoringService = scoringService;
        _emailService = emailService;
        _logger = logger;
    }

    public IActionResult Index()
    {
        var lista = _db.Processos
            .OrderByDescending(p => p.CriadoEm)
            .Select(p => new ProcessoIndexVM
            {
                Id = p.Id,
                Nome = p.Nome,
                Status = p.Status,
                PrazoFinal = p.PrazoFinal,
                TipoProcesso = p.TipoProcesso,
                OrcamentoEstimado = p.OrcamentoEstimado,
                TotalPropostas = p.Propostas.Count,
                MenorValorOfertado = p.Propostas.Any() ? p.Propostas.Min(pr => pr.ValorTotal) : (decimal?)null
            })
            .ToList();

        return View(lista);
    }

    public IActionResult Details(int id)
    {
        var processo = _db.Processos
            .Include(p => p.Criterios)
            .Include(p => p.Propostas).ThenInclude(pr => pr.Avaliacoes)
            .FirstOrDefault(p => p.Id == id);

        if (processo == null) return NotFound();

        var pedidos = _db.Pedidos.Where(pd => pd.ProcessoId == id).ToList();

        var vm = new ProcessoDetailVM
        {
            Processo = processo,
            SomaPesos = processo.Criterios.Sum(c => c.Peso),
            TotalPedidos = pedidos.Count,
            PedidosPendentes = pedidos.Count(pd => pd.Status == Models.Entities.Enums.StatusPedido.Pendente),
            Propostas = processo.Propostas
                .Select(p => new PropostaResumo
                {
                    Id = p.Id,
                    Fornecedor = p.Fornecedor,
                    ValorTotal = p.ValorTotal,
                    Status = p.Status,
                    PontuacaoPonderada = _scoringService.CalcularPontuacaoPonderada(p)
                })
                .OrderByDescending(p => p.PontuacaoPonderada)
                .ToList()
        };

        return View(vm);
    }

    public IActionResult Create()
    {
        return View(new Processo());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Processo processo)
    {
        if (!ModelState.IsValid) return View(processo);

        try
        {
            _db.Processos.Add(processo);
            _db.SaveChanges();
            TempData["Sucesso"] = "Processo criado com sucesso.";
            return RedirectToAction(nameof(Details), new { id = processo.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao criar processo.");
            ModelState.AddModelError("", "Não foi possível criar o processo.");
            return View(processo);
        }
    }

    public IActionResult Edit(int id)
    {
        var processo = _db.Processos.Find(id);
        if (processo == null) return NotFound();
        return View(processo);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, Processo processo)
    {
        if (id != processo.Id) return NotFound();
        if (!ModelState.IsValid) return View(processo);

        try
        {
            _db.Processos.Update(processo);
            _db.SaveChanges();
            TempData["Sucesso"] = "Processo atualizado com sucesso.";
            return RedirectToAction(nameof(Details), new { id = processo.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao editar processo {Id}.", id);
            ModelState.AddModelError("", "Não foi possível guardar as alterações.");
            return View(processo);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        try
        {
            var processo = _db.Processos.Find(id);
            if (processo == null) return NotFound();

            _db.Processos.Remove(processo);
            _db.SaveChanges();
            TempData["Sucesso"] = "Processo removido com sucesso.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover processo {Id}.", id);
            TempData["EmailWarning"] = "Não foi possível remover o processo.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MudarStatus(int id, StatusProcesso novoStatus)
    {
        var processo = _db.Processos
            .Include(p => p.Propostas).ThenInclude(pr => pr.Avaliacoes)
            .ThenInclude(a => a.Criterio)
            .FirstOrDefault(p => p.Id == id);

        if (processo == null) return NotFound();

        var statusAnterior = processo.Status;
        processo.Status = novoStatus;
        _db.SaveChanges();

        if (novoStatus == StatusProcesso.Decidido && statusAnterior != StatusProcesso.Decidido)
        {
            try
            {
                await _emailService.EnviarNotificacaoDecisaoAsync(processo);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Falha ao notificar decisão do processo {Id}.", id);
                TempData["EmailWarning"] = "Estado atualizado, mas não foi possível enviar a notificação por email.";
            }
        }

        TempData["Sucesso"] = "Estado do processo atualizado.";
        return RedirectToAction(nameof(Details), new { id });
    }
}
