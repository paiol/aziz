using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Services;
using ComparacaoPropostas.ViewModels.Inicio;

namespace ComparacaoPropostas.Controllers;

public class HomeController : Controller
{
    private readonly AppDbContext _db;

    public HomeController(AppDbContext db) => _db = db;

    // Página Início da Plataforma DEI.
    public IActionResult Index()
    {
        var ano = DateTime.Today.Year;
        var vm = new InicioVM { NumPlanos = _db.Planos.Count() };

        var planoId = _db.Planos.Where(p => p.Ano == ano).Select(p => (int?)p.Id).FirstOrDefault()
                      ?? _db.Planos.Where(p => p.Ano > ano).OrderBy(p => p.Ano).Select(p => (int?)p.Id).FirstOrDefault()
                      ?? _db.Planos.OrderByDescending(p => p.Ano).Select(p => (int?)p.Id).FirstOrDefault();
        if (planoId.HasValue)
        {
            var plano = _db.Planos
                .Include(p => p.Projetos).ThenInclude(pp => pp.Pedidos).ThenInclude(pe => pe.Processo)
                .Include(p => p.AcoesFormacao)
                .AsSplitQuery()
                .AsNoTracking()
                .First(p => p.Id == planoId);
            vm.Plano = plano;
            vm.ResumoPlano = PlanoCalculos.CalcularResumo(plano);

            var aceites = plano.Projetos
                .Where(p => p.EstadoAprovacao == EstadoAprovacaoProjeto.Aceite && p.Caixa != CaixaPlano.ForaDoPlano)
                .Select(p => (p.Area, Execucao: PlanoCalculos.CalcularExecucao(p)))
                .ToList();
            vm.ExecucaoCapex = new ExecucaoProjetoCapex
            {
                Orcamento = aceites.Sum(a => a.Execucao.Orcamento),
                Adjudicado = aceites.Sum(a => a.Execucao.Adjudicado),
                EmCurso = aceites.Sum(a => a.Execucao.EmCurso),
                NumCompras = aceites.Sum(a => a.Execucao.NumCompras)
            };
            vm.CapexPorArea = aceites
                .GroupBy(a => a.Area)
                .OrderBy(g => g.Key)
                .Select(g => (g.Key, g.Sum(a => a.Execucao.Orcamento), g.Sum(a => a.Execucao.Adjudicado)))
                .ToList();
        }

        vm.PedidosEmCurso = _db.Pedidos.Count(p => p.Status == StatusPedido.EmCurso);
        vm.PedidosSemProcesso = _db.Pedidos.Count(p => p.Status == StatusPedido.EmCurso && p.Processo == null);
        vm.PedidosSemProjeto = _db.Pedidos.Count(p => p.Status == StatusPedido.EmCurso && p.ProjetoPlanoId == null);
        vm.ProcessosPorEstado = _db.Processos
            .GroupBy(p => p.Status)
            .Select(g => new { g.Key, N = g.Count() })
            .ToDictionary(x => x.Key, x => x.N);

        var adjudicacoes = _db.Processos
            .Where(p => p.DataAdjudicacao != null && p.DataAdjudicacao.Value.Year == ano && p.Status != StatusProcesso.Cancelado)
            .Select(p => new { p.DataAdjudicacao, Valor = p.ValorAdjudicadoCVE ?? 0m })
            .ToList();
        vm.AdjudicadoAno = adjudicacoes.Sum(a => a.Valor);
        vm.AdjudicadoPorMes = Enumerable.Range(1, 12)
            .Select(m => (PlanoCalculos.NomesMeses[m - 1], adjudicacoes.Where(a => a.DataAdjudicacao!.Value.Month == m).Sum(a => a.Valor)))
            .ToList();

        vm.ObrasPorEstado = _db.ProjetosObra
            .GroupBy(p => p.Status)
            .Select(g => new { g.Key, N = g.Count() })
            .ToDictionary(x => x.Key, x => x.N);

        vm.NumFornecedores = _db.Fornecedores.Count();
        vm.NumItensMaterial = _db.ItensMaterial.Count();
        vm.NumAvaliadores = _db.Avaliadores.Count();

        return View(vm);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
