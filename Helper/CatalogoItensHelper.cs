using Microsoft.EntityFrameworkCore;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Helper;

public static class CatalogoItensHelper
{
    public static List<ItemMaterial> CarregarIndentado(AppDbContext db, string? tipoProcesso = null)
    {
        var query = db.ItensMaterial
            .Include(i => i.SubItens)
            .Where(i => i.ItemPaiId == null);

        if (!string.IsNullOrWhiteSpace(tipoProcesso))
            query = query.Where(i => string.IsNullOrEmpty(i.Dominio) || i.Dominio == tipoProcesso);

        var paisComFilhos = query.OrderBy(i => i.NomeItem).ToList();

        var lista = new List<ItemMaterial>();
        foreach (var pai in paisComFilhos)
        {
            lista.Add(pai);
            var filhos = pai.SubItens.AsEnumerable();
            if (!string.IsNullOrWhiteSpace(tipoProcesso))
                filhos = filhos.Where(s => string.IsNullOrEmpty(s.Dominio) || s.Dominio == tipoProcesso);
            lista.AddRange(filhos.OrderBy(s => s.NomeItem));
        }
        return lista;
    }

    public static List<string> ObterDominios(AppDbContext db)
        => db.ItensMaterial
            .Where(i => !string.IsNullOrEmpty(i.Dominio))
            .Select(i => i.Dominio!)
            .Distinct()
            .OrderBy(d => d)
            .ToList();
}
