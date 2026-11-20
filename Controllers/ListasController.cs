using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.Models.Entities.Enums;
using ComparacaoPropostas.Services;
using ComparacaoPropostas.ViewModels.Listas;

namespace ComparacaoPropostas.Controllers;

// Gestão das listas de escolha dos projetos do plano (Ilha, Tipo, Coordenação, Rubrica…).
public class ListasController : Controller
{
    private readonly AppDbContext _db;

    public ListasController(AppDbContext db) => _db = db;

    public IActionResult Index(ListaPlano lista = ListaPlano.Ilha)
    {
        var usos = ContarUsos();
        var todos = _db.ValoresLista.AsNoTracking().ToList();

        var vm = new ListasVM
        {
            Lista = lista,
            Contagens = Enum.GetValues<ListaPlano>().ToDictionary(l => l, l => todos.Count(v => v.Lista == l)),
            CamposQueUsam = ListasPlano.Campos.Where(c => c.Value == lista).Select(c => c.Key).ToList(),
            Valores = todos
                .Where(v => v.Lista == lista)
                .OrderBy(v => v.Ordem).ThenBy(v => v.Valor)
                .Select(v => new LinhaValorListaVM { Valor = v, Usos = usos.GetValueOrDefault((lista, v.Valor.ToLowerInvariant())) })
                .ToList()
        };
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Adicionar(ListaPlano lista, string? valor)
    {
        valor = valor?.Trim();
        if (string.IsNullOrEmpty(valor))
        {
            TempData["EmailWarning"] = "Escreva o valor a acrescentar.";
        }
        else if (valor.Length > 200)
        {
            TempData["EmailWarning"] = "O valor não pode ter mais de 200 caracteres.";
        }
        else if (_db.ValoresLista.Any(v => v.Lista == lista && v.Valor == valor))
        {
            TempData["EmailWarning"] = $"\"{valor}\" já existe nesta lista.";
        }
        else
        {
            var ordem = (_db.ValoresLista.Where(v => v.Lista == lista).Max(v => (int?)v.Ordem) ?? 0) + 10;
            _db.ValoresLista.Add(new ValorLista { Lista = lista, Valor = valor, Ordem = ordem, Ativo = true });
            _db.SaveChanges();
            TempData["Sucesso"] = $"\"{valor}\" acrescentado.";
        }
        return RedirectToAction(nameof(Index), new { lista });
    }

    // Renomear / reordenar. Ao renomear, os projetos que usam o valor antigo passam a usar o novo.
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Editar(int id, string? valor, int ordem)
    {
        var item = _db.ValoresLista.Find(id);
        if (item == null) return NotFound();

        valor = valor?.Trim();
        if (string.IsNullOrEmpty(valor) || valor.Length > 200)
        {
            TempData["EmailWarning"] = "O valor não pode ficar vazio nem ter mais de 200 caracteres.";
            return RedirectToAction(nameof(Index), new { lista = item.Lista });
        }
        if (!string.Equals(valor, item.Valor, StringComparison.Ordinal)
            && _db.ValoresLista.Any(v => v.Lista == item.Lista && v.Id != id && v.Valor == valor))
        {
            TempData["EmailWarning"] = $"\"{valor}\" já existe nesta lista.";
            return RedirectToAction(nameof(Index), new { lista = item.Lista });
        }

        var antigo = item.Valor;
        var atualizados = 0;
        if (!string.Equals(antigo, valor, StringComparison.Ordinal))
        {
            foreach (var projeto in _db.ProjetosPlano.ToList())
                if (ListasPlano.Renomear(projeto, item.Lista, antigo, valor)) atualizados++;
        }

        item.Valor = valor;
        item.Ordem = ordem;
        _db.SaveChanges();

        TempData["Sucesso"] = atualizados > 0
            ? $"Valor atualizado; {atualizados} projeto(s) passaram a usar \"{valor}\"."
            : "Valor atualizado.";
        return RedirectToAction(nameof(Index), new { lista = item.Lista });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Alternar(int id)
    {
        var item = _db.ValoresLista.Find(id);
        if (item == null) return NotFound();

        item.Ativo = !item.Ativo;
        _db.SaveChanges();
        TempData["Sucesso"] = item.Ativo ? $"\"{item.Valor}\" volta a aparecer para escolha." : $"\"{item.Valor}\" deixa de aparecer para escolha.";
        return RedirectToAction(nameof(Index), new { lista = item.Lista });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult Delete(int id)
    {
        var item = _db.ValoresLista.Find(id);
        if (item == null) return NotFound();

        var usos = ContarUsos().GetValueOrDefault((item.Lista, item.Valor.ToLowerInvariant()));
        if (usos > 0)
        {
            TempData["EmailWarning"] = $"\"{item.Valor}\" é usado em {usos} projeto(s): desative-o em vez de o apagar.";
        }
        else
        {
            _db.ValoresLista.Remove(item);
            _db.SaveChanges();
            TempData["Sucesso"] = $"\"{item.Valor}\" apagado.";
        }
        return RedirectToAction(nameof(Index), new { lista = item.Lista });
    }

    // Acrescenta às listas os valores que os projetos já usam e ainda não estão nas listas
    // (ex.: projetos importados antes de as listas existirem).
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AcrescentarUsados(ListaPlano lista)
    {
        var novos = ListasPlano.GarantirValores(_db, _db.ProjetosPlano.AsNoTracking().ToList());
        _db.SaveChanges();
        TempData["Sucesso"] = novos > 0 ? $"{novos} valor(es) usados nos projetos acrescentado(s) às listas." : "Todas as listas já têm os valores usados nos projetos.";
        return RedirectToAction(nameof(Index), new { lista });
    }

    private Dictionary<(ListaPlano, string), int> ContarUsos()
        => _db.ProjetosPlano.AsNoTracking().ToList()
            .SelectMany(p => ListasPlano.ValoresUsados(p).Distinct())
            .GroupBy(u => (u.Lista, u.Valor.ToLowerInvariant()))
            .ToDictionary(g => g.Key, g => g.Count());
}
