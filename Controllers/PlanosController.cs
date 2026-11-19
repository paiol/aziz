using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Services;
using ComparacaoPropostas.ViewModels.Planos;

namespace ComparacaoPropostas.Controllers;

public class PlanosController : Controller
{
    private readonly AppDbContext _db;
    private readonly IPlanoExcelService _planoExcelService;
    private readonly ILogger<PlanosController> _logger;

    public PlanosController(AppDbContext db, IPlanoExcelService planoExcelService, ILogger<PlanosController> logger)
    {
        _db = db;
        _planoExcelService = planoExcelService;
        _logger = logger;
    }

    public IActionResult Index()
    {
        var planos = _db.Planos
            .Include(p => p.Projetos)
            .Include(p => p.AcoesFormacao)
            .OrderByDescending(p => p.Ano)
            .ThenBy(p => p.Designacao)
            .AsNoTracking()
            .ToList();

        return View(planos);
    }

    // Resumo do plano: totais, por rubrica, por caixa do plano, carga mensal vs capacidade e formação.
    public IActionResult Details(int id)
    {
        var plano = _db.Planos
            .Include(p => p.Projetos)
            .Include(p => p.AcoesFormacao)
            .AsNoTracking()
            .FirstOrDefault(p => p.Id == id);

        if (plano == null) return NotFound();

        return View(new PlanoDetailVM { Plano = plano, Resumo = PlanoCalculos.CalcularResumo(plano) });
    }

    public IActionResult Create() => View(new Plano { Designacao = $"Plano {DateTime.Today.Year + 1} DEP-INF" });

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(Plano plano)
    {
        if (!ModelState.IsValid) return View(plano);

        _db.Planos.Add(plano);
        _db.SaveChanges();

        TempData["Sucesso"] = "Plano criado com sucesso.";
        return RedirectToAction(nameof(Details), new { id = plano.Id });
    }

    public IActionResult Edit(int id)
    {
        var plano = _db.Planos.Find(id);
        if (plano == null) return NotFound();
        return View(plano);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, Plano plano)
    {
        if (id != plano.Id) return NotFound();

        var existente = _db.Planos.AsNoTracking().FirstOrDefault(p => p.Id == id);
        if (existente == null) return NotFound();
        plano.CriadoEm = existente.CriadoEm;

        if (!ModelState.IsValid) return View(plano);

        _db.Planos.Update(plano);
        _db.SaveChanges();

        TempData["Sucesso"] = "Plano atualizado com sucesso.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var plano = _db.Planos.Find(id);
        if (plano == null) return NotFound();

        try
        {
            _db.Planos.Remove(plano);
            _db.SaveChanges();
            TempData["Sucesso"] = "Plano removido com sucesso.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover plano {Id}.", id);
            TempData["EmailWarning"] = "Não foi possível remover o plano.";
        }

        return RedirectToAction(nameof(Index));
    }

    // Cria um plano novo a partir do ficheiro SCOPE_INF (.xlsx).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Importar(IFormFile ficheiro)
    {
        var importado = LerFicheiro(ficheiro);
        if (importado == null) return RedirectToAction(nameof(Index));

        var plano = new Plano
        {
            Ano = importado.Ano ?? DateTime.Today.Year + 1,
            Designacao = importado.Designacao ?? $"Plano {importado.Ano ?? DateTime.Today.Year + 1}",
            CapacidadeArquiteturaDias = importado.CapacidadeArquiteturaDias,
            CapacidadeEnergiaDias = importado.CapacidadeEnergiaDias,
            Descricao = $"Importado de {Path.GetFileName(ficheiro.FileName)}"
        };
        AplicarImportacao(plano, importado);

        _db.Planos.Add(plano);
        _db.SaveChanges();

        TempData["Sucesso"] = MensagemImportacao(importado);
        return RedirectToAction(nameof(Details), new { id = plano.Id });
    }

    // Recarrega projetos e ações de formação de um plano existente (substitui os atuais).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult ImportarParaPlano(int id, IFormFile ficheiro)
    {
        var plano = _db.Planos
            .Include(p => p.Projetos)
            .Include(p => p.AcoesFormacao)
            .FirstOrDefault(p => p.Id == id);
        if (plano == null) return NotFound();

        var importado = LerFicheiro(ficheiro);
        if (importado == null) return RedirectToAction(nameof(Details), new { id });

        _db.ProjetosPlano.RemoveRange(plano.Projetos);
        _db.AcoesFormacaoPlano.RemoveRange(plano.AcoesFormacao);
        plano.Projetos.Clear();
        plano.AcoesFormacao.Clear();
        plano.CapacidadeArquiteturaDias = importado.CapacidadeArquiteturaDias ?? plano.CapacidadeArquiteturaDias;
        plano.CapacidadeEnergiaDias = importado.CapacidadeEnergiaDias ?? plano.CapacidadeEnergiaDias;
        AplicarImportacao(plano, importado);

        _db.SaveChanges();

        TempData["Sucesso"] = MensagemImportacao(importado);
        return RedirectToAction(nameof(Details), new { id });
    }

    private PlanoImportado? LerFicheiro(IFormFile? ficheiro)
    {
        if (ficheiro == null || ficheiro.Length == 0)
        {
            TempData["EmailWarning"] = "Selecione o ficheiro Excel do plano (SCOPE_INF).";
            return null;
        }

        try
        {
            using var stream = ficheiro.OpenReadStream();
            var importado = _planoExcelService.LerPlanoExcel(stream);
            if (importado.Projetos.Count == 0 && importado.AcoesFormacao.Count == 0)
            {
                TempData["EmailWarning"] = "Não foi encontrado nenhum projeto nem ação de formação no ficheiro. "
                    + string.Join(" ", importado.Avisos);
                return null;
            }
            return importado;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao ler o ficheiro do plano {Ficheiro}.", ficheiro.FileName);
            TempData["EmailWarning"] = "Não foi possível ler o ficheiro Excel.";
            return null;
        }
    }

    private static void AplicarImportacao(Plano plano, PlanoImportado importado)
    {
        foreach (var p in importado.Projetos) plano.Projetos.Add(p);
        foreach (var a in importado.AcoesFormacao) plano.AcoesFormacao.Add(a);
    }

    private static string MensagemImportacao(PlanoImportado importado)
    {
        var msg = $"Plano importado: {importado.Projetos.Count} projeto(s) e {importado.AcoesFormacao.Count} ação(ões) de formação/feiras.";
        if (importado.Avisos.Count > 0) msg += " Avisos: " + string.Join(" ", importado.Avisos.Take(5));
        return msg;
    }
}
