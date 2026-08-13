using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using ComparacaoPropostas.ViewModels.ItensPedido;

namespace ComparacaoPropostas.Helper;

public static class ItemPickerHelper
{
    // Cross-catalog typeahead used by the item picker: matches by name across the shared
    // ItemMaterial catalog and the 4 domain-specific tables (Energia/MBB/FBB/Core). With no
    // "termo", falls back to listing items tagged with "dominio" so the picker can show a
    // starter list for the suggested Área on focus.
    public static List<ItemBuscaResultado> Buscar(AppDbContext db, string? termo, string? dominio)
    {
        termo = (termo ?? "").Trim();
        dominio = (dominio ?? "").Trim();

        if (termo.Length < 2 && dominio.Length == 0)
            return new List<ItemBuscaResultado>();

        var resultados = new List<ItemBuscaResultado>();

        resultados.AddRange(db.ItensMaterial.Where(i => i.ItemPaiId == null).ToList()
            .Where(i => CorrespondeAoFiltro(i.NomeItem, i.Dominio, termo, dominio))
            .Take(10).Select(i => new ItemBuscaResultado { Chave = $"material:{i.Id}", Nome = i.NomeItem, Categoria = i.Categoria, Unidade = i.Unidade, Dominio = i.Dominio, Origem = "Catálogo" }));

        resultados.AddRange(db.ItensEnergia.ToList()
            .Where(i => CorrespondeAoFiltro(i.Nome, i.Dominio, termo, dominio))
            .Take(10).Select(i => new ItemBuscaResultado { Chave = $"energia:{i.Id}", Nome = i.Nome, Categoria = i.Categoria, Unidade = i.Unidade, Dominio = i.Dominio, Origem = "Energia" }));

        resultados.AddRange(db.ItensMbb.ToList()
            .Where(i => CorrespondeAoFiltro(i.Nome, i.Dominio, termo, dominio))
            .Take(10).Select(i => new ItemBuscaResultado { Chave = $"mbb:{i.Id}", Nome = i.Nome, Categoria = i.Categoria, Unidade = i.Unidade, Dominio = i.Dominio, Origem = "MBB" }));

        resultados.AddRange(db.ItensFbb.ToList()
            .Where(i => CorrespondeAoFiltro(i.Nome, i.Dominio, termo, dominio))
            .Take(10).Select(i => new ItemBuscaResultado { Chave = $"fbb:{i.Id}", Nome = i.Nome, Categoria = i.Categoria, Unidade = i.Unidade, Dominio = i.Dominio, Origem = "FBB" }));

        resultados.AddRange(db.ItensCore.ToList()
            .Where(i => CorrespondeAoFiltro(i.Nome, i.Dominio, termo, dominio))
            .Take(10).Select(i => new ItemBuscaResultado { Chave = $"core:{i.Id}", Nome = i.Nome, Categoria = i.Categoria, Unidade = i.Unidade, Dominio = i.Dominio, Origem = "Core" }));

        return resultados.OrderBy(r => r.Nome).Take(30).ToList();
    }

    // Distinct list of Categoria values across the 4 "Base de Dados" catalogs only (Energia/
    // MBB/FBB/Core) — deliberately excludes the general Itens/Materiais catalog, used to
    // populate the Categoria dropdown in the item-request picker.
    public static List<string> ObterCategorias(AppDbContext db)
    {
        var categorias = new List<string?>();
        categorias.AddRange(db.ItensEnergia.Select(i => i.Categoria));
        categorias.AddRange(db.ItensMbb.Select(i => i.Categoria));
        categorias.AddRange(db.ItensFbb.Select(i => i.Categoria));
        categorias.AddRange(db.ItensCore.Select(i => i.Categoria));

        return categorias
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Select(c => c!)
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();
    }

    // Materials from the 4 "Base de Dados" catalogs matching a chosen Categoria — feeds the
    // Material dropdown once a Categoria is picked.
    public static List<ItemBuscaResultado> ObterMateriaisPorCategoria(AppDbContext db, string categoria)
    {
        if (string.IsNullOrWhiteSpace(categoria)) return new List<ItemBuscaResultado>();

        var resultados = new List<ItemBuscaResultado>();

        resultados.AddRange(db.ItensEnergia.Where(i => i.Categoria == categoria)
            .Select(i => new ItemBuscaResultado { Chave = $"energia:{i.Id}", Nome = i.Nome, Categoria = i.Categoria, Unidade = i.Unidade, Dominio = i.Dominio, Origem = "Energia" }));

        resultados.AddRange(db.ItensMbb.Where(i => i.Categoria == categoria)
            .Select(i => new ItemBuscaResultado { Chave = $"mbb:{i.Id}", Nome = i.Nome, Categoria = i.Categoria, Unidade = i.Unidade, Dominio = i.Dominio, Origem = "MBB" }));

        resultados.AddRange(db.ItensFbb.Where(i => i.Categoria == categoria)
            .Select(i => new ItemBuscaResultado { Chave = $"fbb:{i.Id}", Nome = i.Nome, Categoria = i.Categoria, Unidade = i.Unidade, Dominio = i.Dominio, Origem = "FBB" }));

        resultados.AddRange(db.ItensCore.Where(i => i.Categoria == categoria)
            .Select(i => new ItemBuscaResultado { Chave = $"core:{i.Id}", Nome = i.Nome, Categoria = i.Categoria, Unidade = i.Unidade, Dominio = i.Dominio, Origem = "Core" }));

        return resultados.OrderBy(r => r.Nome).ToList();
    }

    private static bool CorrespondeAoFiltro(string nomeItem, string? dominioItem, string termo, string dominio)
    {
        if (termo.Length >= 2) return nomeItem.Contains(termo, StringComparison.OrdinalIgnoreCase);
        if (dominio.Length > 0) return string.Equals(dominioItem, dominio, StringComparison.OrdinalIgnoreCase);
        return false;
    }

    // Bridges a pick from any of the 4 domain catalogs into the shared ItemMaterial table
    // (found-or-created by name) so ItemPedido/ItemProposta/Excel/Comparação keep working
    // against a single catalog, regardless of which catalog the picker searched. Also returns
    // which of the 4 "Base de Dados" catalogs (Energia/MBB/FBB/Core) the pick came from, so
    // the caller can record it on ItemPedido.TipoCatalogo — null when picked from the general
    // Itens/Materiais catalog instead.
    public static (int? ItemMaterialId, string? TipoCatalogo) ResolverChaveItem(AppDbContext db, string chave)
    {
        var partes = chave.Split(':', 2);
        if (partes.Length != 2 || !int.TryParse(partes[1], out var id)) return (null, null);

        if (partes[0] == "material")
            return (db.ItensMaterial.Any(m => m.Id == id) ? id : null, null);

        string nome, tipoCatalogo;
        string? categoria, unidade, dominio;

        switch (partes[0])
        {
            case "energia":
                var e = db.ItensEnergia.Find(id);
                if (e == null) return (null, null);
                (nome, categoria, unidade, dominio, tipoCatalogo) = (e.Nome, e.Categoria, e.Unidade, e.Dominio, "Energia");
                break;
            case "mbb":
                var m = db.ItensMbb.Find(id);
                if (m == null) return (null, null);
                (nome, categoria, unidade, dominio, tipoCatalogo) = (m.Nome, m.Categoria, m.Unidade, m.Dominio, "MBB");
                break;
            case "fbb":
                var f = db.ItensFbb.Find(id);
                if (f == null) return (null, null);
                (nome, categoria, unidade, dominio, tipoCatalogo) = (f.Nome, f.Categoria, f.Unidade, f.Dominio, "FBB");
                break;
            case "core":
                var c = db.ItensCore.Find(id);
                if (c == null) return (null, null);
                (nome, categoria, unidade, dominio, tipoCatalogo) = (c.Nome, c.Categoria, c.Unidade, c.Dominio, "Core");
                break;
            default:
                return (null, null);
        }

        var existente = db.ItensMaterial.FirstOrDefault(im => im.NomeItem == nome);
        if (existente != null) return (existente.Id, tipoCatalogo);

        var novo = new ItemMaterial
        {
            NomeItem = nome,
            Categoria = categoria,
            Unidade = unidade,
            Dominio = string.IsNullOrWhiteSpace(dominio) ? tipoCatalogo : dominio
        };
        db.ItensMaterial.Add(novo);
        db.SaveChanges();
        return (novo.Id, tipoCatalogo);
    }
}
