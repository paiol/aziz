using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;

namespace ComparacaoPropostas.Helper;

// Escolha e validação do projeto Capex a que uma compra (Pedido) ou uma obra (ProjetoObra) pertence.
// Só os projetos aceites no Capex podem receber compras ou obras novas.
public static class CapexProjetosHelper
{
    public static bool Aceite(ProjetoPlano p) => p.EstadoAprovacao == EstadoAprovacaoProjeto.Aceite && p.Caixa != CaixaPlano.ForaDoPlano;

    // Projetos aceites agrupados por plano; o projeto atual aparece sempre, mesmo que já não esteja aceite.
    // Com soObras, os projetos que parecem obra aparecem primeiro e marcados.
    public static List<SelectListItem> ListaProjetos(AppDbContext db, int? atualId, bool soObras = false)
    {
        var projetos = db.ProjetosPlano
            .Include(p => p.Plano)
            .Where(p => p.EstadoAprovacao == EstadoAprovacaoProjeto.Aceite || p.Id == atualId)
            .AsNoTracking()
            .ToList()
            .Where(p => p.Caixa != CaixaPlano.ForaDoPlano || p.Id == atualId)
            .OrderByDescending(p => p.Plano.Ano)
            .ThenBy(p => soObras && !p.EhObra)
            .ThenBy(p => p.Area)
            .ThenBy(p => p.Nome)
            .ToList();

        var grupos = projetos.Select(p => p.Plano.Designacao).Distinct().ToDictionary(d => d, d => new SelectListGroup { Name = d });
        return projetos
            .Select(p => new SelectListItem(
                $"{p.Area.ToLabel()} · {p.Nome}{(soObras && p.EhObra ? " (obra)" : "")}",
                p.Id.ToString(),
                p.Id == atualId) { Group = grupos[p.Plano.Designacao] })
            .ToList();
    }

    public static void Validar(AppDbContext db, ModelStateDictionary modelState, string campo, int? projetoPlanoId, bool obrigatorio, int? atualId, string oQue)
    {
        if (projetoPlanoId == null)
        {
            if (obrigatorio) modelState.AddModelError(campo, $"Escolha o projeto Capex a que {oQue} pertence.");
            return;
        }

        var projeto = db.ProjetosPlano.AsNoTracking().FirstOrDefault(p => p.Id == projetoPlanoId);
        if ((projeto == null || !Aceite(projeto)) && projetoPlanoId != atualId)
            modelState.AddModelError(campo, "Só é possível associar a projetos aceites no Capex.");
    }
}
