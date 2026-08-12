using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.ViewModels.Avaliacoes;

namespace ComparacaoPropostas.Controllers;

public class AvaliacoesController : Controller
{
    private readonly AppDbContext _db;

    public AvaliacoesController(AppDbContext db)
    {
        _db = db;
    }

    public IActionResult Editar(int propostaId)
    {
        var proposta = _db.Propostas
            .Include(p => p.Avaliacoes)
            .Include(p => p.Processo).ThenInclude(pr => pr.Criterios)
            .FirstOrDefault(p => p.Id == propostaId);

        if (proposta == null) return NotFound();

        var vm = new AvaliacaoFormVM
        {
            PropostaId = proposta.Id,
            PropostaFornecedor = proposta.Fornecedor,
            ProcessoId = proposta.ProcessoId,
            Itens = proposta.Processo.Criterios
                .OrderByDescending(c => c.Peso)
                .Select(c =>
                {
                    var existente = proposta.Avaliacoes.FirstOrDefault(a => a.CriterioId == c.Id);
                    return new ItemAvaliacaoVM
                    {
                        CriterioId = c.Id,
                        CriterioNome = c.Nome,
                        Peso = c.Peso,
                        Nota = existente?.Nota ?? 0,
                        Comentario = existente?.Comentario
                    };
                })
                .ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Editar(AvaliacaoFormVM model)
    {
        var proposta = _db.Propostas.Include(p => p.Avaliacoes).FirstOrDefault(p => p.Id == model.PropostaId);
        if (proposta == null) return NotFound();

        foreach (var item in model.Itens)
        {
            var existente = proposta.Avaliacoes.FirstOrDefault(a => a.CriterioId == item.CriterioId);
            if (existente != null)
            {
                existente.Nota = item.Nota;
                existente.Comentario = item.Comentario;
            }
            else
            {
                _db.Avaliacoes.Add(new Avaliacao
                {
                    PropostaId = model.PropostaId,
                    CriterioId = item.CriterioId,
                    Nota = item.Nota,
                    Comentario = item.Comentario
                });
            }
        }

        _db.SaveChanges();
        TempData["Sucesso"] = "Avaliação guardada com sucesso.";
        return RedirectToAction("Details", "Processos", new { id = model.ProcessoId });
    }
}
