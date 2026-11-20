using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Helper;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Controllers;

// Projetos propostos pelas áreas para um plano. A página do plano lista todos os projetos
// introduzidos, com a decisão (Proposto / Aceite / Rejeitado); só os aceites formam o Capex.
public class ProjetosPlanoController : Controller
{
    private readonly AppDbContext _db;
    private readonly ILogger<ProjetosPlanoController> _logger;

    public ProjetosPlanoController(AppDbContext db, ILogger<ProjetosPlanoController> logger)
    {
        _db = db;
        _logger = logger;
    }

    public IActionResult Index(int planoId, CaixaPlano? caixa, AreaDepartamento? area, EstadoAprovacaoProjeto? aprovacao)
    {
        var plano = _db.Planos.Find(planoId);
        if (plano == null) return NotFound();

        var query = _db.ProjetosPlano.Where(p => p.PlanoId == planoId);
        if (area != null) query = query.Where(p => p.Area == area);
        if (aprovacao != null) query = query.Where(p => p.EstadoAprovacao == aprovacao);

        var projetos = query
            .AsNoTracking()
            .ToList()
            .Where(p => caixa == null || p.Caixa == caixa)
            .OrderBy(p => p.Area)
            .ThenBy(p => p.Caixa)
            .ThenBy(p => p.DesignacaoRubrica)
            .ThenBy(p => p.Nome)
            .ToList();

        ViewBag.Plano = plano;
        ViewBag.Caixa = caixa;
        ViewBag.Area = area;
        ViewBag.Aprovacao = aprovacao;
        return View(projetos);
    }

    public IActionResult Create(int planoId, AreaDepartamento? area)
    {
        var plano = _db.Planos.Find(planoId);
        if (plano == null) return NotFound();

        PrepararListas(plano);
        return View(new ProjetoPlano { PlanoId = planoId, Area = area ?? AreaDepartamento.DepInf, Situacao = SituacaoProjetoPlano.Novo });
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

        if (projeto.EstadoAprovacao != EstadoAprovacaoProjeto.Proposto) projeto.DataDecisao = DateTime.UtcNow;
        _db.ProjetosPlano.Add(projeto);
        _db.SaveChanges();

        TempData["Sucesso"] = "Projeto proposto adicionado ao plano.";
        return RedirectToAction(nameof(Index), new { planoId = projeto.PlanoId, area = projeto.Area });
    }

    // Ficha do projeto: dados do plano, decisão e execução pelas compras associadas.
    public IActionResult Details(int id)
    {
        var projeto = _db.ProjetosPlano
            .Include(p => p.Plano)
            .Include(p => p.ProjetoOrigem).ThenInclude(o => o!.Plano)
            .Include(p => p.Pedidos).ThenInclude(pe => pe.Processo)
            .Include(p => p.Obras)
            .AsNoTracking()
            .FirstOrDefault(p => p.Id == id);
        if (projeto == null) return NotFound();

        return View(projeto);
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
        projeto.ProjetoOrigemId = existente.ProjetoOrigemId;
        projeto.DataDecisao = projeto.EstadoAprovacao == existente.EstadoAprovacao ? existente.DataDecisao
            : projeto.EstadoAprovacao == EstadoAprovacaoProjeto.Proposto ? null : DateTime.UtcNow;

        ValidarMeses(projeto);
        ValidarRejeicaoComCompras(id, projeto.EstadoAprovacao);
        if (!ModelState.IsValid)
        {
            PrepararListas(_db.Planos.Find(projeto.PlanoId)!);
            return View(projeto);
        }

        _db.ProjetosPlano.Update(projeto);
        _db.SaveChanges();

        TempData["Sucesso"] = "Projeto atualizado com sucesso.";
        return RedirectToAction(nameof(Details), new { id });
    }

    // Aceitar / rejeitar / voltar a proposto a partir da listagem do plano.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Decidir(int id, EstadoAprovacaoProjeto estado, string? motivo, string? voltarPara)
    {
        var projeto = _db.ProjetosPlano.Find(id);
        if (projeto == null) return NotFound();

        if (estado != EstadoAprovacaoProjeto.Aceite && TemComprasOuObras(id))
        {
            TempData["EmailWarning"] = $"O projeto \"{projeto.Nome}\" já tem compras ou obras associadas e tem de continuar aceite.";
        }
        else
        {
            projeto.EstadoAprovacao = estado;
            projeto.DataDecisao = estado == EstadoAprovacaoProjeto.Proposto ? null : DateTime.UtcNow;
            if (motivo != null) projeto.MotivoDecisao = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
            _db.SaveChanges();
            TempData["Sucesso"] = $"Projeto \"{projeto.Nome}\": {estado.ToLabel()}.";
        }

        return Url.IsLocalUrl(voltarPara) ? Redirect(voltarPara!) : RedirectToAction(nameof(Index), new { planoId = projeto.PlanoId });
    }

    // Aceita de uma vez os projetos propostos que entram no plano (Transitado / Transitado parcial / Novo).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AceitarQueEntram(int planoId, AreaDepartamento? area)
    {
        var propostos = _db.ProjetosPlano
            .Where(p => p.PlanoId == planoId && p.EstadoAprovacao == EstadoAprovacaoProjeto.Proposto && (area == null || p.Area == area))
            .ToList()
            .Where(p => p.Caixa == CaixaPlano.Entra)
            .ToList();

        foreach (var p in propostos)
        {
            p.EstadoAprovacao = EstadoAprovacaoProjeto.Aceite;
            p.DataDecisao = DateTime.UtcNow;
        }
        _db.SaveChanges();

        TempData["Sucesso"] = $"{propostos.Count} projeto(s) aceite(s).";
        return RedirectToAction(nameof(Index), new { planoId, area });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var projeto = _db.ProjetosPlano.Find(id);
        if (projeto == null) return NotFound();

        var planoId = projeto.PlanoId;
        if (TemComprasOuObras(id))
        {
            TempData["EmailWarning"] = "Não é possível remover: o projeto tem compras ou obras associadas.";
            return RedirectToAction(nameof(Index), new { planoId });
        }

        try
        {
            foreach (var seguinte in _db.ProjetosPlano.Where(p => p.ProjetoOrigemId == id)) seguinte.ProjetoOrigemId = null;
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

    private bool TemComprasOuObras(int id)
        => _db.Pedidos.Any(p => p.ProjetoPlanoId == id) || _db.ProjetosObra.Any(o => o.ProjetoPlanoId == id);

    private void ValidarMeses(ProjetoPlano projeto)
    {
        if (projeto.MesInicio.HasValue && projeto.MesFim.HasValue && projeto.MesFim < projeto.MesInicio)
            ModelState.AddModelError(nameof(ProjetoPlano.MesFim), "O mês de fim não pode ser anterior ao mês de início.");
    }

    private void ValidarRejeicaoComCompras(int id, EstadoAprovacaoProjeto estado)
    {
        if (estado != EstadoAprovacaoProjeto.Aceite && TemComprasOuObras(id))
            ModelState.AddModelError(nameof(ProjetoPlano.EstadoAprovacao), "O projeto já tem compras ou obras associadas e tem de continuar aceite.");
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
