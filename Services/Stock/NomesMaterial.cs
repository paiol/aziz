using System.Globalization;
using System.Text;
using ComparacaoPropostas.Data;
using ComparacaoPropostas.Models.Entities;
using Microsoft.EntityFrameworkCore;

namespace ComparacaoPropostas.Services.Stock;

// Deteta quando o mesmo material (mesmo código = ID) aparece com outro nome — no inventário feito
// pelo armazém, num Packing List ou numa entrada — e regista um alerta para alguém decidir qual o
// nome certo. A comparação ignora maiúsculas, acentos, pontuação e espaços a mais.
public static class NomesMaterial
{
    public static string Normalizar(string? nome)
    {
        if (string.IsNullOrWhiteSpace(nome)) return "";
        var sb = new StringBuilder(nome.Length);
        var espaco = false;
        foreach (var c in nome.Normalize(NormalizationForm.FormD))
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) == UnicodeCategory.NonSpacingMark) continue;
            if (char.IsLetterOrDigit(c))
            {
                if (espaco && sb.Length > 0) sb.Append(' ');
                sb.Append(char.ToUpperInvariant(c));
                espaco = false;
            }
            else espaco = true;
        }
        return sb.ToString();
    }

    public static bool Diferem(string? a, string? b)
    {
        var na = Normalizar(a);
        var nb = Normalizar(b);
        return na.Length > 0 && nb.Length > 0 && na != nb;
    }

    // Nomes já conhecidos para o material: o do catálogo e os que já foram vistos (pendentes ou decididos).
    public static IEnumerable<string> NomesConhecidos(MaterialStock m, IEnumerable<DivergenciaNomeMaterial> divergencias)
    {
        yield return m.Descricao;
        foreach (var d in divergencias)
        {
            yield return d.NomeRecebido;
            if (d.Estado != EstadoDivergenciaNome.Pendente) yield return d.NomeCatalogo;
        }
    }

    // Há alerta a criar? Só se o nome recebido for novo para este material (e não for o próprio código).
    public static bool PrecisaAlerta(MaterialStock m, string? nomeRecebido, IEnumerable<DivergenciaNomeMaterial> divergencias)
    {
        var n = Normalizar(nomeRecebido);
        if (n.Length == 0 || n == Normalizar(m.Codigo)) return false;
        return NomesConhecidos(m, divergencias).All(c => Normalizar(c) != n);
    }

    // Regista (sem gravar) o alerta, se for preciso. Materiais acabados de criar não geram alerta.
    public static DivergenciaNomeMaterial? Registar(AppDbContext db, MaterialStock m, string? nomeRecebido, OrigemNomeMaterial origem,
        string? referencia, EntradaStock? entrada = null, InventarioStock? inventario = null)
    {
        if (m.Id == 0) return null;
        db.DivergenciasNomeMaterial.Where(d => d.MaterialStockId == m.Id).Load();
        var conhecidas = db.DivergenciasNomeMaterial.Local.Where(d => d.MaterialStockId == m.Id).ToList();
        if (!PrecisaAlerta(m, nomeRecebido, conhecidas)) return null;

        var d = new DivergenciaNomeMaterial
        {
            MaterialStockId = m.Id,
            Material = m,
            NomeCatalogo = m.Descricao,
            NomeRecebido = nomeRecebido!.Trim(),
            Origem = origem,
            Referencia = referencia?.Length > 150 ? referencia[..150] : referencia,
            Entrada = entrada,
            Inventario = inventario
        };
        db.DivergenciasNomeMaterial.Add(d);
        return d;
    }
}
