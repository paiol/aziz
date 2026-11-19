using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Controllers;

public class ProjetosPlanoController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<ProjetosPlanoController> _logger;

    public ProjetosPlanoController(AppDbContext db, ILogger<ProjetosPlanoController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public IActionResult Index(int planoId, CaixaPlano? caixa)
    {
        var plano = _db.Planos.Find(planoId);
        if (plano == null) return NotFound();

        var projetos = _db.ProjetosPlano
            .Where(p => p.PlanoId == planoId)
            .AsNoTracking()
            .ToList()
            .Where(p => caixa == null || p.Caixa == caixa)
            .OrderBy(p => p.Caixa)
            .ThenBy(p => p.DesignacaoRubrica)
            .ThenBy(p => p.Nome)
            .ToList();

        ViewBag.Plano = plano;
        ViewBag.Caixa = caixa;
        return View(projetos);
    }

    public IActionResult Create(int planoId)
    {
        var plano = _db.Planos.Find(planoId);
        if (plano == null) return NotFound();

        PrepararListas(plano);
        return View(new ProjetoPlano { PlanoId = planoId, Situacao = SituacaoProjetoPlano.Novo });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Create(ProjetoPlano projeto)
    {
        var plano = _db.Planos.Find(projeto.PlanoId);
        if (plano == null) return NotFound();

        ValidarMeses(projeto);
        if (!ModelState.IsValid)
        {
            PrepararListas(plano);
            return View(projeto);
        }

        _db.ProjetosPlano.Add(projeto);
        _db.SaveChanges();

        TempData["Sucesso"] = "Projeto adicionado ao plano.";
        return RedirectToAction(nameof(Index), new { planoId = projeto.PlanoId });
    }

    public IActionResult Edit(int id)
    {
        var projeto = _db.ProjetosPlano.Include(p => p.Plano).FirstOrDefault(p => p.Id == id);
        if (projeto == null) return NotFound();

        PrepararListas(projeto.Plano);
        return View(projeto);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Edit(int id, ProjetoPlano projeto)
    {
        if (id != projeto.Id) return NotFound();

        var existente = _db.ProjetosPlano.AsNoTracking().FirstOrDefault(p => p.Id == id);
        if (existente == null) return NotFound();
        projeto.PlanoId = existente.PlanoId;

        ValidarMeses(projeto);
        if (!ModelState.IsValid)
        {
            PrepararListas(_db.Planos.Find(projeto.PlanoId)!);
            return View(projeto);
        }

        _db.ProjetosPlano.Update(projeto);
        _db.SaveChanges();

        TempData["Sucesso"] = "Projeto atualizado com sucesso.";
        return RedirectToAction(nameof(Index), new { planoId = projeto.PlanoId });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var projeto = _db.ProjetosPlano.Find(id);
        if (projeto == null) return NotFound();

        var planoId = projeto.PlanoId;
        try
        {
            _db.ProjetosPlano.Remove(projeto);
            _db.SaveChanges();
            TempData["Sucesso"] = "Projeto removido do plano.";
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Erro ao remover projeto do plano {Id}.", id);
            TempData["EmailWarning"] = "Não foi possível remover o projeto.";
        }

        return RedirectToAction(nameof(Index), new { planoId });
    }

    private void ValidarMeses(ProjetoPlano projeto)
    {
        if (projeto.MesInicio.HasValue && projeto.MesFim.HasValue && projeto.MesFim < projeto.MesInicio)
            ModelState.AddModelError(nameof(ProjetoPlano.MesFim), "O mês de fim não pode ser anterior ao mês de início.");
    }

    // Sugestões (datalist) com os valores já usados no plano, à semelhança das listas da folha "Listas".
    private void PrepararListas(Plano plano)
    {
        ViewBag.Plano = plano;
        var projetos = _db.ProjetosPlano.Where(p => p.PlanoId == plano.Id).AsNoTracking().ToList();

        List<string> Valores(Func<ProjetoPlano, string?> campo, params string[] padrao) => projetos
            .Select(campo)
            .Concat(padrao)
            .Where(v => !string.IsNullOrWhiteSpace(v))
            .Select(v => v!.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(v => v)
            .ToList();

        ViewBag.Sugestoes = new Dictionary<string, List<string>>
        {
            ["rubricas"] = Valores(p => p.DesignacaoRubrica),
            ["tipos"] = Valores(p => p.Tipo, "ENR", "EDF", "RAN", "PRC"),
            ["coords"] = Valores(p => p.Coordenacao, "CRN", "CRS", "SSM", "CLI", "CRF", "NAC"),
            ["ilhas"] = Valores(p => p.Ilha, "SA", "SV", "SN", "SL", "BV", "MO", "ST-PR", "ST-INT", "FG", "BR", "NAC"),
            ["classificacoes"] = Valores(p => p.Classificacao),
            ["capex"] = Valores(p => p.Capex),
            ["origens"] = Valores(p => p.OrigemIniciativa),
            ["trimestres"] = Valores(p => p.TrimestrePrevisto, "T1", "T2", "T3", "T4", "Ano todo"),
            ["zonas"] = Valores(p => p.Zona, "Urbano", "Rural"),
            ["executantes"] = Valores(p => p.ExecucaoProjeto, "DEI", "DOM", "Terceiro", "NA", "Chave na mão"),
            ["perfis"] = Valores(p => p.PerfilEquipaMontagem, "Energia", "Equipa", "ST", "SV"),
            ["simnao"] = new List<string> { "Sim", "Não", "NA" }
        };
    }
}
