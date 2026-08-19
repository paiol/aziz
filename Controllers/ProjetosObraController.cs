using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Services;
using ComparacaoPropostas.ViewModels.ProjetosObra;

namespace ComparacaoPropostas.Controllers;

public class ProjetosObraController : Controller
{
    private readonly AppDbContext _db;
    private readonly IScoringObraService _scoringObraService;
    private readonly IEmailObraService _emailObraService;
    private readonly ILogger<ProjetosObraController> _logger;

    public ProjetosObraController(AppDbContext db, IScoringObraService scoringObraService, IEmailObraService emailObraService, ILogger<ProjetosObraController> logger)
    {
        _db = db;
        _scoringObraService = scoringObraService;
        _emailObraService = emailObraService;
        _logger = logger;
    }

    public IActionResult Index()
    {
        var lista = _db.ProjetosObra
            .Include(p => p.Propostas)
            .Include(p => p.ItensMQT)
            .OrderByDescending(p => p.CriadoEm)
            .ToList();

        return View(lista);
    }

    public IActionResult Details(int id)
    {
        // PropostaVencedora não precisa de Include próprio: como já vem carregada dentro de
        // Propostas (com ItensProposta), o EF Core resolve a navegação automaticamente.
        var projeto = _db.ProjetosObra
            .Include(p => p.Criterios)
            .Include(p => p.ItensMQT)
            .Include(p => p.Anexos)
            .Include(p => p.Propostas).ThenInclude(pr => pr.Avaliacoes)
            .Include(p => p.Propostas).ThenInclude(pr => pr.ItensProposta)
            .FirstOrDefault(p => p.Id == id);

        if (projeto == null) return NotFound();

        var propostasOrdenadas = projeto.Propostas
            .Select(p => new PropostaEmpreiteiroResumo
            {
                Id = p.Id,
                Empreiteiro = p.Empreiteiro,
                ValorTotal = p.Subtotal,
                PrazoEntregaDias = p.PrazoEntregaDias,
                Status = p.Status,
                PontuacaoPonderada = _scoringObraService.CalcularPontuacaoPonderada(p, projeto.Criterios)
            })
            .OrderByDescending(p => p.PontuacaoPonderada)
            .ThenBy(p => p.ValorTotal)
            .ToList();

        for (var i = 0; i < propostasOrdenadas.Count; i++) propostasOrdenadas[i].PosicaoRanking = i + 1;

        var podeComunicar = projeto.Status == StatusProjetoObra.Concluido
            && projeto.PropostaVencedoraId.HasValue
            && !string.IsNullOrWhiteSpace(projeto.EmailsNotificacao);

        var vm = new ProjetoObraDetailVM
        {
            ProjetoObra = projeto,
            SomaPesos = projeto.SomaPesos,
            TotalItensMQT = projeto.ItensMQT.Count,
            Propostas = propostasOrdenadas
        };

        if (podeComunicar)
        {
            vm.MailtoResultado = _emailObraService.ConstruirLinkMailto(projeto);
            var (destinatarios, assunto, corpo) = _emailObraService.ObterDadosEmailParaCopiar(projeto);
            vm.EmailDestinatarios = destinatarios;
            vm.EmailAssunto = assunto;
            vm.EmailCorpo = corpo;
        }

        return View(vm);
    }

    public IActionResult Create()
    {
        return View(new ProjetoObra());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(ProjetoObra projeto)
    {
        if (!ModelState.IsValid) return View(projeto);

        _db.ProjetosObra.Add(projeto);
        _db.SaveChanges();

        TempData["Sucesso"] = "Projeto de obra criado com sucesso.";
        return RedirectToAction(nameof(Details), new { id = projeto.Id });
    }

    public IActionResult Edit(int id)
    {
        var projeto = _db.ProjetosObra.Find(id);
        if (projeto == null) return NotFound();
        return View(projeto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, ProjetoObra projeto)
    {
        if (id != projeto.Id) return NotFound();

        var existente = _db.ProjetosObra.AsNoTracking().FirstOrDefault(p => p.Id == id);
        if (existente == null) return NotFound();

        projeto.PropostaVencedoraId = existente.PropostaVencedoraId;
        projeto.ValorAdjudicado = existente.ValorAdjudicado;
        projeto.DataAdjudicacao = existente.DataAdjudicacao;
        projeto.ResponsavelAdjudicacao = existente.ResponsavelAdjudicacao;
        projeto.JustificativaAdjudicacao = existente.JustificativaAdjudicacao;
        projeto.CriadoEm = existente.CriadoEm;

        if (!ModelState.IsValid) return View(projeto);

        try
        {
            _db.ProjetosObra.Update(projeto);
            _db.SaveChanges();
            TempData["Sucesso"] = "Projeto atualizado com sucesso.";
            return RedirectToAction(nameof(Details), new { id = projeto.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao editar projeto de obra {Id}.", id);
            ModelState.AddModelError("", "Não foi possível guardar as alterações.");
            return View(projeto);
        }
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        try
        {
            var projeto = _db.ProjetosObra.Find(id);
            if (projeto == null) return NotFound();

            _db.ProjetosObra.Remove(projeto);
            _db.SaveChanges();
            TempData["Sucesso"] = "Projeto removido com sucesso.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover projeto de obra {Id}.", id);
            TempData["EmailWarning"] = "Não foi possível remover o projeto.";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult MudarStatus(int id, StatusProjetoObra novoStatus)
    {
        var projeto = _db.ProjetosObra.Find(id);
        if (projeto == null) return NotFound();

        projeto.Status = novoStatus;
        _db.SaveChanges();

        TempData["Sucesso"] = "Estado do projeto atualizado.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpGet]
    public IActionResult Adjudicar(int id)
    {
        var projeto = _db.ProjetosObra
            .Include(p => p.Criterios)
            .Include(p => p.Propostas).ThenInclude(pr => pr.Avaliacoes)
            .Include(p => p.Propostas).ThenInclude(pr => pr.ItensProposta)
            .FirstOrDefault(p => p.Id == id);

        if (projeto == null) return NotFound();

        if (projeto.Propostas.Count == 0)
        {
            TempData["EmailWarning"] = "O projeto não possui propostas para adjudicação.";
            return RedirectToAction(nameof(Details), new { id });
        }

        var ranking = projeto.Propostas
            .Select(p => new OpcaoPropostaEmpreiteiroAdjudicacao
            {
                Id = p.Id,
                Empreiteiro = p.Empreiteiro,
                PontuacaoPonderada = _scoringObraService.CalcularPontuacaoPonderada(p, projeto.Criterios),
                ValorTotal = p.Subtotal,
                PrazoEntregaDias = p.PrazoEntregaDias
            })
            .OrderByDescending(p => p.PontuacaoPonderada)
            .ThenBy(p => p.ValorTotal)
            .ToList();

        for (var i = 0; i < ranking.Count; i++) ranking[i].PosicaoRanking = i + 1;

        var primeiro = ranking.FirstOrDefault();

        var vm = new AdjudicacaoObraVM
        {
            ProjetoObraId = projeto.Id,
            ProjetoObraNome = projeto.Designacao,
            PropostaVencedoraId = projeto.PropostaVencedoraId ?? primeiro?.Id ?? 0,
            ResponsavelAdjudicacao = projeto.ResponsavelAdjudicacao ?? "",
            JustificativaAdjudicacao = projeto.JustificativaAdjudicacao,
            PrimeiroLugarRankingId = primeiro?.Id,
            PrimeiroLugarEmpreiteiro = primeiro?.Empreiteiro,
            PropostasDisponiveis = ranking
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Adjudicar(AdjudicacaoObraVM model)
    {
        var projeto = _db.ProjetosObra
            .Include(p => p.Criterios)
            .Include(p => p.Propostas).ThenInclude(pr => pr.Avaliacoes)
            .Include(p => p.Propostas).ThenInclude(pr => pr.ItensProposta)
            .FirstOrDefault(p => p.Id == model.ProjetoObraId);

        if (projeto == null) return NotFound();

        var propostaVencedora = projeto.Propostas.FirstOrDefault(p => p.Id == model.PropostaVencedoraId);
        if (propostaVencedora == null)
        {
            ModelState.AddModelError("PropostaVencedoraId", "A proposta selecionada não pertence a este projeto.");
        }

        var ranking = projeto.Propostas
            .Select(p => new { p.Id, Pontuacao = _scoringObraService.CalcularPontuacaoPonderada(p, projeto.Criterios), p.Subtotal })
            .OrderByDescending(p => p.Pontuacao)
            .ThenBy(p => p.Subtotal)
            .ToList();

        var primeiroId = ranking.FirstOrDefault()?.Id;
        if (primeiroId.HasValue && model.PropostaVencedoraId != primeiroId.Value && string.IsNullOrWhiteSpace(model.JustificativaAdjudicacao))
        {
            ModelState.AddModelError("JustificativaAdjudicacao", "É obrigatório fornecer uma justificação ao selecionar um empreiteiro diferente do 1º classificado no ranking.");
        }

        if (!ModelState.IsValid)
        {
            model.PropostasDisponiveis = projeto.Propostas
                .Select(p => new OpcaoPropostaEmpreiteiroAdjudicacao
                {
                    Id = p.Id,
                    Empreiteiro = p.Empreiteiro,
                    PontuacaoPonderada = _scoringObraService.CalcularPontuacaoPonderada(p, projeto.Criterios),
                    ValorTotal = p.Subtotal,
                    PrazoEntregaDias = p.PrazoEntregaDias
                })
                .OrderByDescending(p => p.PontuacaoPonderada)
                .ThenBy(p => p.ValorTotal)
                .ToList();

            for (var i = 0; i < model.PropostasDisponiveis.Count; i++)
                model.PropostasDisponiveis[i].PosicaoRanking = i + 1;

            model.PrimeiroLugarRankingId = primeiroId;
            model.PrimeiroLugarEmpreiteiro = projeto.Propostas.FirstOrDefault(p => p.Id == primeiroId)?.Empreiteiro;
            return View(model);
        }

        try
        {
            projeto.PropostaVencedoraId = propostaVencedora!.Id;
            projeto.ValorAdjudicado = propostaVencedora.Subtotal;
            projeto.DataAdjudicacao = DateTime.UtcNow;
            projeto.ResponsavelAdjudicacao = model.ResponsavelAdjudicacao.Trim();
            projeto.JustificativaAdjudicacao = string.IsNullOrWhiteSpace(model.JustificativaAdjudicacao) ? null : model.JustificativaAdjudicacao.Trim();
            projeto.Status = StatusProjetoObra.Concluido;

            foreach (var p in projeto.Propostas)
            {
                p.Status = p.Id == propostaVencedora.Id ? StatusPropostaEmpreiteiro.Aceite : StatusPropostaEmpreiteiro.Rejeitada;
            }

            _db.SaveChanges();

            TempData["Sucesso"] = $"Adjudicação registada com sucesso para o empreiteiro '{propostaVencedora.Empreiteiro}'.";
            return RedirectToAction(nameof(Details), new { id = projeto.Id });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao registar adjudicação do projeto de obra {ProjetoObraId}.", model.ProjetoObraId);
            ModelState.AddModelError("", "Não foi possível concluir a adjudicação.");
            return View(model);
        }
    }
}
