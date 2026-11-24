using ComparacaoPropostas.Models.Entities;

namespace ComparacaoPropostas.Services.Stock;

// Correspondência entre o código de um fornecedor (ex.: Part Number da Huawei) e o material SAP.
public static class CodigosFornecedor
{
    // "Huawei International Co. Limited" e "Huawei" são o mesmo fornecedor: compara a primeira palavra.
    public static bool MesmoFornecedor(string? a, string? b)
    {
        var pa = PrimeiraPalavra(a);
        var pb = PrimeiraPalavra(b);
        return pa.Length > 0 && pa == pb;
    }

    private static string PrimeiraPalavra(string? nome) =>
        NomesMaterial.Normalizar(nome).Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "";

    // Entre as correspondências com o mesmo código, escolhe a do fornecedor da entrada. Se só houver uma,
    // serve essa (o código é único). Se houver várias de outros fornecedores, não escolhe (null).
    public static CodigoFornecedorMaterial? Procurar(IEnumerable<CodigoFornecedorMaterial> mesmoCodigo, string? fornecedor)
    {
        var lista = mesmoCodigo.ToList();
        var doFornecedor = lista.Where(c => MesmoFornecedor(c.Fornecedor, fornecedor)).ToList();
        if (doFornecedor.Count > 0) return doFornecedor[0];
        return lista.Select(c => c.MaterialStockId).Distinct().Count() == 1 ? lista[0] : null;
    }
}
