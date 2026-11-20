using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Services;
using ComparacaoPropostas.ViewModels.Capex;

namespace ComparacaoPropostas.Controllers;

// Capex do ano: os projetos aceites do plano, com a execução pelas compras associadas.
public class CapexController : Controller
{
    private readonly AppDbContext _db;

    public CapexController(AppDbContext db) => _db = db;

    public IActionResult Index(int? planoId, AreaDepartamento? area)
    {
        var planos = _db.Planos.AsNoTracking().OrderByDescending(p => p.Ano).ToList();
        if (planos.Count == 0)
        {
            TempData["EmailWarning"] = "Ainda não existe nenhum plano. Crie ou importe o plano Capex primeiro.";
            return RedirectToAction("Index", "Planos");
        }

        var plano = planoId.HasValue
            ? planos.FirstOrDefault(p => p.Id == planoId)
            : planos.FirstOrDefault(p => p.Ano == DateTime.Today.Year)
              ?? planos.Where(p => p.Ano > DateTime.Today.Year).OrderBy(p => p.Ano).FirstOrDefault()
              ?? planos.First();
        if (plano == null) return NotFound();

        var aceites = _db.ProjetosPlano
            .Where(p => p.PlanoId == plano.Id && p.EstadoAprovacao == EstadoAprovacaoProjeto.Aceite)
            .Include(p => p.Pedidos).ThenInclude(pe => pe.Processo)
            .AsNoTracking()
            .ToList()
            .Where(p => p.Caixa != CaixaPlano.ForaDoPlano)
            .ToList();

        var linhas = aceites
            .Select(p => new LinhaCapexVM { Projeto = p, Execucao = PlanoCalculos.CalcularExecucao(p) })
            .ToList();

        var vm = new CapexVM
        {
            Plano = plano,
            Planos = planos,
            Area = area,
            PorArea = linhas
                .GroupBy(l => l.Projeto.Area)
                .OrderBy(g => g.Key)
                .Select(g => new ResumoAreaCapexVM
                {
                    Area = g.Key,
                    NumProjetos = g.Count(),
                    Orcamento = g.Sum(l => l.Execucao.Orcamento),
                    Adjudicado = g.Sum(l => l.Execucao.Adjudicado),
                    EmCurso = g.Sum(l => l.Execucao.EmCurso)
                })
                .ToList(),
            Linhas = linhas
                .Where(l => area == null || l.Projeto.Area == area)
                .OrderBy(l => l.Projeto.Area)
                .ThenByDescending(l => l.Execucao.Orcamento)
                .ToList()
        };
        vm.Total = new ExecucaoProjetoCapex
        {
            Orcamento = vm.Linhas.Sum(l => l.Execucao.Orcamento),
            Adjudicado = vm.Linhas.Sum(l => l.Execucao.Adjudicado),
            EmCurso = vm.Linhas.Sum(l => l.Execucao.EmCurso),
            NumCompras = vm.Linhas.Sum(l => l.Execucao.NumCompras)
        };

        return View(vm);
    }
}
