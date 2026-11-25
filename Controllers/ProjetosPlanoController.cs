using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Services;
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

        ListasPlano.AplicarFormulario(projeto, Request.Form);
        ValidarMeses(projeto);
        if (!ModelState.IsValid)
        {
            PrepararListas(plano);
            return View(projeto);
        }

        if (projeto.EstadoAprovacao != EstadoAprovacaoProjeto.Proposto) projeto.DataDecisao = DateTime.UtcNow;
        ListasPlano.GarantirValores(_db, new[] { projeto });
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

        ViewBag.MovimentosStock = _db.MovimentosStock.Include(x => x.Material).Include(x => x.Entrada)
            .Where(x => x.ProjetoPlanoId == id).OrderByDescending(x => x.Data).AsNoTracking().ToList();
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
        // Estado do envio para o Airtable: não vem no formulário.
        projeto.ScopePendente = existente.ScopePendente;
        projeto.ScopeRecordId = existente.ScopeRecordId;
        projeto.ScopeEnviadoEm = existente.ScopeEnviadoEm;
        projeto.ScopeErro = existente.ScopeErro;
        projeto.ScopeTentativas = existente.ScopeTentativas;
        projeto.ScopeProximaTentativa = existente.ScopeProximaTentativa;
        projeto.DataDecisao = projeto.EstadoAprovacao == existente.EstadoAprovacao ? existente.DataDecisao
            : projeto.EstadoAprovacao == EstadoAprovacaoProjeto.Proposto ? null : DateTime.UtcNow;

        ListasPlano.AplicarFormulario(projeto, Request.Form);
        ValidarMeses(projeto);
        ValidarRejeicaoComCompras(id, projeto.EstadoAprovacao);
        if (!ModelState.IsValid)
        {
            PrepararListas(_db.Planos.Find(projeto.PlanoId)!);
            return View(projeto);
        }

        ListasPlano.GarantirValores(_db, new[] { projeto });
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

    // Decide de uma vez os projetos marcados na lista (caixas de seleção): aceitar, rejeitar ou voltar a proposto.
    // Projetos com compras ou obras não podem deixar de estar aceites: ficam como estão e são indicados.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DecidirVarios(int planoId, List<int>? ids, EstadoAprovacaoProjeto estado, string? motivo, string? voltarPara)
    {
        var voltar = Url.IsLocalUrl(voltarPara) ? Redirect(voltarPara!) : (IActionResult)RedirectToAction(nameof(Index), new { planoId });
        ids = (ids ?? new List<int>()).Distinct().ToList();
        if (ids.Count == 0)
        {
            TempData["EmailWarning"] = "Selecione pelo menos um projeto (caixas de seleção da lista).";
            return voltar;
        }

        var projetos = _db.ProjetosPlano.Where(p => p.PlanoId == planoId && ids.Contains(p.Id)).ToList();
        var bloqueados = new List<string>();
        var alterados = 0;
        foreach (var p in projetos)
        {
            if (estado != EstadoAprovacaoProjeto.Aceite && TemComprasOuObras(p.Id))
            {
                bloqueados.Add(p.Nome);
                continue;
            }
            if (p.EstadoAprovacao == estado && string.IsNullOrWhiteSpace(motivo)) continue;   // já estava assim
            p.EstadoAprovacao = estado;
            p.DataDecisao = estado == EstadoAprovacaoProjeto.Proposto ? null : DateTime.UtcNow;
            if (motivo != null) p.MotivoDecisao = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
            alterados++;
        }
        _db.SaveChanges();

        TempData["Sucesso"] = $"{alterados} projeto(s): {estado.ToLabel()}.";
        if (bloqueados.Count > 0)
            TempData["EmailWarning"] = $"{bloqueados.Count} projeto(s) com compras ou obras continuam aceites: {string.Join(", ", bloqueados.Take(5))}{(bloqueados.Count > 5 ? "…" : "")}.";
        return voltar;
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
        if (_db.Planos.Any(p => p.Id == planoId && p.Estado == EstadoPlano.Concluido))
        {
            TempData["EmailWarning"] = "O plano está concluído: para retirar o projeto, rejeite-o (é retirado também do Airtable) ou reabra o plano.";
            return RedirectToAction(nameof(Index), new { planoId });
        }
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
        => _db.Pedidos.Any(p => p.ProjetoPlanoId == id) || _db.ProjetosObra.Any(o => o.ProjetoPlanoId == id)
           || _db.EntradasStock.Any(e => e.ProjetoPlanoId == id) || _db.MovimentosStock.Any(m => m.ProjetoPlanoId == id);

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

    // Valores das listas do plano (Ilha, Tipo, Coordenação…) para os campos de escolha do formulário.
    private void PrepararListas(Plano plano)
    {
        ViewBag.Plano = plano;
        ViewBag.Listas = ListasPlano.CarregarAtivas(_db);
    }
}
