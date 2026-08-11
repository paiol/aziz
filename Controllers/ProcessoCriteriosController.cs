using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.ViewModels.Criterios;

namespace ComparacaoPropostas.Controllers;

public class ProcessoCriteriosController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<ProcessoCriteriosController> _logger;

    public ProcessoCriteriosController(AppDbContext db, ILogger<ProcessoCriteriosController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public IActionResult Gerenciar(int processoId)
    {
        var processo = _db.Processos
            .Include(p => p.Criterios)
            .FirstOrDefault(p => p.Id == processoId);
        if (processo == null) return NotFound();

        var criteriosDoDominio = _db.CriteriosAvaliacao
            .Where(c => c.Dominio == processo.TipoProcesso)
            .OrderBy(c => c.Nome)
            .ToList();

        var vm = new GerenciarCriteriosVM
        {
            ProcessoId = processo.Id,
            ProcessoNome = processo.Nome,
            TipoProcesso = processo.TipoProcesso,
            Itens = criteriosDoDominio.Select(c =>
            {
                var existente = processo.Criterios.FirstOrDefault(pc => pc.CriterioAvaliacaoId == c.Id);
                return new ItemCriterioVM
                {
                    CriterioAvaliacaoId = c.Id,
                    Nome = c.Nome,
                    Categoria = c.Categoria,
                    Selecionado = existente != null,
                    Peso = existente?.Peso ?? 0
                };
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Gerenciar(GerenciarCriteriosVM model)
    {
        var processoId = model.ProcessoId;
        var processo = _db.Processos
            .Include(p => p.Criterios).ThenInclude(pc => pc.Avaliacoes)
            .FirstOrDefault(p => p.Id == processoId);
        if (processo == null) return NotFound();

        foreach (var item in model.Itens)
        {
            var existente = processo.Criterios.FirstOrDefault(pc => pc.CriterioAvaliacaoId == item.CriterioAvaliacaoId);

            if (item.Selecionado)
            {
                if (existente != null)
                {
                    existente.Peso = item.Peso;
                }
                else
                {
                    _db.ProcessosCriterio.Add(new ProcessoCriterio
                    {
                        ProcessoId = processoId,
                        CriterioAvaliacaoId = item.CriterioAvaliacaoId,
                        Peso = item.Peso
                    });
                }
            }
            else if (existente != null)
            {
                // Avaliacao->ProcessoCriterio is Restrict (not Cascade) to avoid SQL Server's
                // multiple-cascade-paths error, so remove dependent Avaliacoes explicitly here.
                _db.Avaliacoes.RemoveRange(existente.Avaliacoes);
                _db.ProcessosCriterio.Remove(existente);
            }
        }

        _db.SaveChanges();
        TempData["Sucesso"] = "Critérios do processo atualizados.";
        return RedirectToAction("Details", "Processos", new { id = processoId });
    }
}
