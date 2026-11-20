using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComparacaoPropostas.Migrations
{
    /// <inheritdoc />
    public partial class AddModuloPlano : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Planos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Ano = table.Column<int>(type: "int", nullable: false),
                    Designacao = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Departamento = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CapacidadeArquiteturaDias = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    CapacidadeEnergiaDias = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    CriadoPor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Planos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AcoesFormacaoPlano",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlanoId = table.Column<int>(type: "int", nullable: false),
                    Prioridade = table.Column<int>(type: "int", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Acao = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Datas = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Local = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Formato = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LigacaoProjetos = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NumeroPessoas = table.Column<int>(type: "int", nullable: true),
                    Dias = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    InscricaoPorPessoa = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    PassagemPorPessoa = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    EstadiaDia = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AjudaCustoDia = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    NoPlano = table.Column<bool>(type: "bit", nullable: false),
                    Participantes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Fonte = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Observacoes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ano = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcoesFormacaoPlano", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcoesFormacaoPlano_Planos_PlanoId",
                        column: x => x.PlanoId,
                        principalTable: "Planos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ProjetosPlano",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PlanoId = table.Column<int>(type: "int", nullable: false),
                    CapexAnoMenos2 = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CodCapexAnoAnterior = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CodCapex = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SeguimentoOrcamento = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DesignacaoRubrica = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    OrigemIniciativa = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Tipo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Coordenacao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ilha = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Classificacao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Nome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Observacoes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Capex = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Situacao = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Prioridade = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    TrimestrePrevisto = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExecucaoAnoAnterior = table.Column<decimal>(type: "decimal(9,4)", precision: 9, scale: 4, nullable: true),
                    Zona = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExecucaoProjeto = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FiscalizacaoCivil = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FiscalizacaoTracados = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ExecucaoMontagem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PerfilEquipaMontagem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FiscalizacaoMontagem = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PrestadorExternoIlha = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NecessidadeAquisicao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntidadesExecutantes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PropostasSemAdjudicacao = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AdjudicadoAnoAnterior = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    AdjudicadoAnosAnteriores = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ValorTransitar = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ValorNovo = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ValorCondicionado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    TotalOrcamentoAnoAnterior = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CustoAquisicaoSite = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CustoEquipamento = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CustoConstrucoes = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CustoServicos = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CustoDeslocacoes = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CustoTerceirizacao = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    CondicaoMotivo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MesInicio = table.Column<int>(type: "int", nullable: true),
                    MesFim = table.Column<int>(type: "int", nullable: true),
                    EsforcoArquiteturaDias = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true),
                    EsforcoEnergiaDias = table.Column<decimal>(type: "decimal(9,2)", precision: 9, scale: 2, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjetosPlano", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProjetosPlano_Planos_PlanoId",
                        column: x => x.PlanoId,
                        principalTable: "Planos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcoesFormacaoPlano_PlanoId",
                table: "AcoesFormacaoPlano",
                column: "PlanoId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjetosPlano_PlanoId",
                table: "ProjetosPlano",
                column: "PlanoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcoesFormacaoPlano");

            migrationBuilder.DropTable(
                name: "ProjetosPlano");

            migrationBuilder.DropTable(
                name: "Planos");
        }
    }
}
