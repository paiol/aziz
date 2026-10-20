namespace ComparacaoPropostas.Services.Airtable;

/// <summary>
/// IDs das tabelas e campos da base Airtable "Comparação de Propostas" (appiAu2t9S3VoA1bO).
/// Usam-se IDs (e não nomes) para que renomear um campo/tabela no Airtable não parta a integração.
/// Se apagares um campo no Airtable, a sincronização falha com erro na página Integração Airtable.
/// </summary>
public static class AirtableSchema
{
    public const string BaseId = "appiAu2t9S3VoA1bO";

    public static class Processos
    {
        public const string Tabela = "tbljUKNRRehA6Us4N";
        public const string IdApp = "fldrcVdpzacta1xGj";
        public const string Nome = "fldkyc9gYoSSoUrgp";
        public const string Descricao = "fldnULaO1KnkxFyfB";
        public const string Status = "fldosjRhmF0qlWAyz";
        public const string PrazoFinal = "fldBUrYNWbeA4FbHt";
        public const string OrcamentoEstimado = "fldOX3XNs8PGhTCpZ";
        public const string NumeroProcesso = "flddDGlDDBINya4s9";
        public const string Area = "fldDMNN0hssLbLiya";
        public const string TipoCompra = "fld4E9G8i8sPSXWg9";
        public const string FornecedorVencedor = "fldaUzKRrojr0QDKb";
        public const string ValorAdjudicadoCve = "fldSxkWYAXLgkvCX8";
        public const string DataAdjudicacao = "flddfMqdHIzS6iY7a";
        public const string LinkApp = "fldueGis6SmCf6ivc";
    }

    public static class Criterios
    {
        public const string Tabela = "tbliom9g1RwvJqzgU";
        public const string IdApp = "fldWci8OBaHBD0PFS";
        public const string Nome = "fld4v9vOlV9sMmyzc";
        public const string Categoria = "fldRh5VijRq8615s1";
        public const string Peso = "fldSZNvFdI7dDWec2";
        public const string Descricao = "fldvMyRCjlRZHOVkF";
    }

    public static class CriteriosProcesso
    {
        public const string Tabela = "tblBZl2O9vyzgL0Q6";
        public const string IdApp = "fldSpiV2WCCsd8kIZ";
        public const string Nome = "fldEZzreWSZTygBwt";
        public const string Processo = "fldsBi1Tpw80MGy3q";
        public const string Criterio = "fldOmTmxLS2fWyuFd";
    }

    public static class Propostas
    {
        public const string Tabela = "tblZvZg0MOB7sTQiR";
        public const string IdApp = "fldJmD7el8RVngHSg";
        public const string Fornecedor = "fldjqrA9GyFhlDhpO";
        public const string ValorTotal = "fldsLrLgfmud40kdT";
        public const string PrazoEntregaDias = "fldEdnQelnRwPL2EA";
        public const string Validade = "fld8mOlVA9ThORQyV";
        public const string Status = "fldEuz5yK5P6QZWDP";
        public const string Observacoes = "fldrn6nnZB3COjmtW";
        public const string Processo = "flde49IpslfTVy88t";
        public const string Moeda = "fldk6foVjvb2sKuZE";
        public const string ValorMoedaOriginal = "fld8Ticf15eBzRSqC";
        public const string Garantia = "fldHtlrhPcvtAXJOP";
        public const string LinkApp = "fldQu7ldsz8x9BejZ";
    }

    public static class ItensMaterial
    {
        public const string Tabela = "tbll9dILt377Wc38A";
        public const string IdApp = "fldXO42BMvn9GpLzO";
        public const string Nome = "fldGMPhuuZMvT3Bi6";
        public const string Categoria = "fldsC77DW8UaMrjsH";
        public const string Unidade = "fld7yX1WUm4W0OWSY";
        public const string Descricao = "fld1BDnw6a5QhVeeb";
    }

    public static class ItensProposta
    {
        public const string Tabela = "tblafMepvqbFQc2QA";
        public const string IdApp = "fldsROAr14BK9JK5Z";
        public const string Nome = "fldSDj7VQFWziCtKO";
        public const string Incluido = "fldr7U39eGVFH8yXJ";
        public const string Quantidade = "fldop8fnhIiUima6y";
        public const string PrecoUnitario = "fldqD4P3xK8PlldDH";
        public const string Observacao = "fldwHdYbTARVxZ2ht";
        public const string Proposta = "fldGgzS5S32HumvcN";
        public const string Item = "fldhrr8Wy0NtgX51N";
    }

    public static class Avaliacoes
    {
        public const string Tabela = "tblo5BeXPw9v1xUwq";
        public const string IdApp = "fldkSLnLnevB9zyzG";
        public const string Nome = "fld7fD9Beye1NlAm7";
        public const string Proposta = "fld6tuqYvzWwdXydO";
        public const string Criterio = "fldZcDkhjxHGEKfgK";
        public const string Nota = "fld8Coomq9qR6QAJv";
        public const string Comentario = "fldnahBXCIi5uuT7s";
        public const string CriterioProcesso = "fldHvy3qgHL592Q43";
    }

    /// <summary>Campo "ID App" (chave de upsert) de cada tabela sincronizada.</summary>
    public static string CampoIdApp(string tabelaId) => tabelaId switch
    {
        Processos.Tabela => Processos.IdApp,
        Criterios.Tabela => Criterios.IdApp,
        CriteriosProcesso.Tabela => CriteriosProcesso.IdApp,
        Propostas.Tabela => Propostas.IdApp,
        ItensMaterial.Tabela => ItensMaterial.IdApp,
        ItensProposta.Tabela => ItensProposta.IdApp,
        Avaliacoes.Tabela => Avaliacoes.IdApp,
        _ => throw new ArgumentOutOfRangeException(nameof(tabelaId), tabelaId, "Tabela Airtable não sincronizada.")
    };
}
