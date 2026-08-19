using Microsoft.AspNetCore.Mvc;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.ViewModels.AvaliacoesObra;

namespace ComparacaoPropostas.Controllers;

public class AvaliacoesObraController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<AvaliacoesObraController> _logger;

    public AvaliacoesObraController(AppDbContext db, ILogger<AvaliacoesObraController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public IActionResult Editar(int propostaEmpreiteiroId)
    {
        var proposta = _db.PropostasEmpreiteiro.Find(propostaEmpreiteiroId);
        if (proposta == null) return NotFound();

        var criterios = _db.CriteriosObra
            .Where(c => c.ProjetoObraId == proposta.ProjetoObraId)
            .OrderByDescending(c => c.Peso)
            .ToList();

        var avaliacoesExistentes = _db.AvaliacoesObra
            .Where(a => a.PropostaEmpreiteiroId == propostaEmpreiteiroId)
            .ToList();

        var vm = new AvaliacaoObraFormVM
        {
            PropostaEmpreiteiroId = proposta.Id,
            Empreiteiro = proposta.Empreiteiro,
            ProjetoObraId = proposta.ProjetoObraId,
            Avaliador = avaliacoesExistentes.FirstOrDefault()?.Avaliador,
            Itens = criterios.Select(c =>
            {
                var existente = avaliacoesExistentes.FirstOrDefault(a => a.CriterioObraId == c.Id);
                return new ItemAvaliacaoObraVM
                {
                    CriterioObraId = c.Id,
                    CriterioNome = c.Nome,
                    Peso = c.Peso,
                    Nota = existente?.Nota ?? 3,
                    Comentario = existente?.Comentario
                };
            }).ToList()
        };

        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Editar(AvaliacaoObraFormVM model)
    {
        var proposta = _db.PropostasEmpreiteiro.Find(model.PropostaEmpreiteiroId);
        if (proposta == null) return NotFound();

        foreach (var item in model.Itens)
        {
            if (item.Nota < 1 || item.Nota > 5)
            {
                ModelState.AddModelError("", $"A nota do critério '{item.CriterioNome}' deve estar entre 1 e 5 estrelas.");
            }
        }

        if (!ModelState.IsValid) return View(model);

        try
        {
            var existentes = _db.AvaliacoesObra
                .Where(a => a.PropostaEmpreiteiroId == model.PropostaEmpreiteiroId)
                .ToList();

            foreach (var item in model.Itens)
            {
                var existente = existentes.FirstOrDefault(a => a.CriterioObraId == item.CriterioObraId);

                if (existente != null)
                {
                    existente.Nota = item.Nota;
                    existente.Comentario = item.Comentario?.Trim();
                    existente.Avaliador = model.Avaliador?.Trim();
                    existente.AvaliadoEm = DateTime.UtcNow;
                }
                else
                {
                    _db.AvaliacoesObra.Add(new AvaliacaoObra
                    {
                        PropostaEmpreiteiroId = model.PropostaEmpreiteiroId,
                        CriterioObraId = item.CriterioObraId,
                        Avaliador = model.Avaliador?.Trim(),
                        Nota = item.Nota,
                        Comentario = item.Comentario?.Trim(),
                        AvaliadoEm = DateTime.UtcNow
                    });
                }
            }

            _db.SaveChanges();

            TempData["Sucesso"] = "Avaliação guardada com sucesso.";
            return RedirectToAction("Details", "ProjetosObra", new { id = proposta.ProjetoObraId });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao gravar avaliação para a proposta de empreiteiro {PropostaEmpreiteiroId}.", model.PropostaEmpreiteiroId);
            ModelState.AddModelError("", "Não foi possível guardar a avaliação.");
            return View(model);
        }
    }
}
