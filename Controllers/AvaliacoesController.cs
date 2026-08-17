using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Services;
using ComparacaoPropostas.ViewModels.Avaliacoes;

namespace ComparacaoPropostas.Controllers;

public class AvaliacoesController : Controller
{
    private readonly AppDbContext _db;
    private readonly IScoringService _scoringService;
    private readonly ILogger<AvaliacoesController> _logger;

    public AvaliacoesController(AppDbContext db, IScoringService scoringService, ILogger<AvaliacoesController> logger)
    {
        _db = db;
        _scoringService = scoringService;
        _logger = logger;
    }

    public IActionResult Editar(int propostaId, int? avaliadorId = null)
    {
        var propostaRef = _db.Propostas.Find(propostaId);
        if (propostaRef == null) return NotFound();

        var processo = _db.Processos
            .Include(p => p.Criterios)
            .Include(p => p.PedidoProposta).ThenInclude(pp => pp.ItensPedido)
            .Include(p => p.Propostas).ThenInclude(pr => pr.Avaliacoes).ThenInclude(a => a.Avaliador)
            .Include(p => p.Propostas).ThenInclude(pr => pr.ItensProposta)
            .Include(p => p.Propostas).ThenInclude(pr => pr.MemoriaCalculo)
            .FirstOrDefault(p => p.Id == propostaRef.ProcessoId);

        if (processo == null) return NotFound();

        _scoringService.AtualizarAvaliacaoAutomatica(processo);

        var proposta = processo.Propostas.First(p => p.Id == propostaId);

        var avaliadores = _db.Avaliadores.Where(a => a.Ativo).OrderBy(a => a.Nome).ToList();

        // Se nenhum avaliadorId foi fornecido mas já há avaliações nesta proposta, usa o primeiro avaliador ou o primeiro da lista
        var avaliadorSelecionadoId = avaliadorId ?? avaliadores.FirstOrDefault()?.Id;

        var vm = new AvaliacaoFormVM
        {
            PropostaId = proposta.Id,
            PropostaFornecedor = proposta.Fornecedor,
            ProcessoId = proposta.ProcessoId,
            AvaliadorId = avaliadorSelecionadoId,
            AvaliadoresDisponiveis = avaliadores,
            Itens = processo.Criterios
                .Where(c => c.TipoAutomatico == TipoCriterioAutomatico.Nenhum)
                .OrderByDescending(c => c.Peso)
                .Select(c =>
                {
                    var existente = avaliadorSelecionadoId.HasValue
                        ? proposta.Avaliacoes.FirstOrDefault(a => a.CriterioId == c.Id && a.AvaliadorId == avaliadorSelecionadoId.Value)
                        : null;

                    return new ItemAvaliacaoVM
                    {
                        CriterioId = c.Id,
                        CriterioNome = c.Nome,
                        Peso = c.Peso,
                        Nota = existente != null ? existente.Nota : 3, // Padrão 3 estrelas
                        Comentario = existente?.Comentario
                    };
                })
                .ToList(),
            CriteriosAutomaticos = processo.Criterios
                .Where(c => c.TipoAutomatico != TipoCriterioAutomatico.Nenhum)
                .OrderByDescending(c => c.Peso)
                .Select(c =>
                {
                    var memoria = proposta.MemoriaCalculo.FirstOrDefault(m => m.CriterioId == c.Id);
                    return new CriterioAutomaticoVM
                    {
                        CriterioNome = c.Nome,
                        Peso = c.Peso,
                        Tipo = c.TipoAutomatico,
                        Nota = memoria?.Nota ?? 0m,
                        Justificativa = memoria?.Justificativa ?? "Ainda sem dados suficientes para calcular."
                    };
                })
                .ToList(),
            OutrasAvaliacoes = proposta.Avaliacoes
                .Where(a => !avaliadorSelecionadoId.HasValue || a.AvaliadorId != avaliadorSelecionadoId.Value)
                .GroupBy(a => a.Avaliador)
                .Select(g => new HistoricoAvaliacaoVM
                {
                    AvaliadorNome = g.Key.Nome,
                    Perfil = g.Key.Perfil,
                    AvaliadoEm = g.Max(a => a.AvaliadoEm),
                    NotasPorCriterio = g.ToDictionary(a => a.Criterio.Nome, a => a.Nota)
                })
                .ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Editar(AvaliacaoFormVM model)
    {
        var proposta = _db.Propostas
            .Include(p => p.Avaliacoes)
            .Include(p => p.Processo).ThenInclude(pr => pr.Criterios)
            .FirstOrDefault(p => p.Id == model.PropostaId);

        if (proposta == null) return NotFound();

        int avaliadorIdFinal = 0;

        // Se o utilizador digitou um novo avaliador, cria-o
        if (!string.IsNullOrWhiteSpace(model.NomeNovoAvaliador))
        {
            var novoAvaliador = new Avaliador
            {
                Nome = model.NomeNovoAvaliador.Trim(),
                Perfil = model.PerfilNovoAvaliador?.Trim(),
                Email = model.EmailNovoAvaliador?.Trim(),
                Ativo = true
            };
            _db.Avaliadores.Add(novoAvaliador);
            _db.SaveChanges();
            avaliadorIdFinal = novoAvaliador.Id;
        }
        else if (model.AvaliadorId.HasValue && model.AvaliadorId.Value > 0)
        {
            avaliadorIdFinal = model.AvaliadorId.Value;
        }
        else
        {
            ModelState.AddModelError("AvaliadorId", "Selecione um avaliador ou cadastre um novo avaliador.");
        }

        // Validar notas 1 a 5
        foreach (var item in model.Itens)
        {
            if (item.Nota < 1 || item.Nota > 5)
            {
                ModelState.AddModelError("", $"A nota do critério '{item.CriterioNome}' deve estar entre 1 e 5 estrelas.");
            }
        }

        if (!ModelState.IsValid)
        {
            model.AvaliadoresDisponiveis = _db.Avaliadores.Where(a => a.Ativo).OrderBy(a => a.Nome).ToList();
            model.OutrasAvaliacoes = proposta.Avaliacoes
                .Where(a => a.AvaliadorId != model.AvaliadorId)
                .GroupBy(a => a.Avaliador)
                .Select(g => new HistoricoAvaliacaoVM
                {
                    AvaliadorNome = g.Key.Nome,
                    Perfil = g.Key.Perfil,
                    AvaliadoEm = g.Max(a => a.AvaliadoEm),
                    NotasPorCriterio = g.ToDictionary(a => a.Criterio.Nome, a => a.Nota)
                })
                .ToList();
            return View(model);
        }

        using var transaction = _db.Database.BeginTransaction();
        try
        {
            foreach (var item in model.Itens)
            {
                var existente = proposta.Avaliacoes
                    .FirstOrDefault(a => a.CriterioId == item.CriterioId && a.AvaliadorId == avaliadorIdFinal);

                if (existente != null)
                {
                    existente.Nota = item.Nota;
                    existente.Comentario = item.Comentario?.Trim();
                    existente.AvaliadoEm = DateTime.UtcNow;
                }
                else
                {
                    _db.Avaliacoes.Add(new Avaliacao
                    {
                        PropostaId = model.PropostaId,
                        CriterioId = item.CriterioId,
                        AvaliadorId = avaliadorIdFinal,
                        Nota = item.Nota,
                        Comentario = item.Comentario?.Trim(),
                        AvaliadoEm = DateTime.UtcNow
                    });
                }
            }

            _db.SaveChanges();
            transaction.Commit();

            TempData["Sucesso"] = "Avaliação guardada com sucesso.";
            return RedirectToAction("Details", "Processos", new { id = model.ProcessoId });
        }
        catch (Exception ex)
        {
            transaction.Rollback();
            _logger.LogError(ex, "Erro ao gravar avaliação para a proposta {PropostaId}.", model.PropostaId);
            ModelState.AddModelError("", "Não foi possível guardar a avaliação.");
            model.AvaliadoresDisponiveis = _db.Avaliadores.Where(a => a.Ativo).OrderBy(a => a.Nome).ToList();
            return View(model);
        }
    }
}
