using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComparacaoPropostas.Migrations
{
    /// <inheritdoc />
    public partial class AddModuloObras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AvaliacoesObra",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropostaEmpreiteiroId = table.Column<int>(type: "int", nullable: false),
                    CriterioObraId = table.Column<int>(type: "int", nullable: false),
                    Avaliador = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Nota = table.Column<int>(type: "int", nullable: false),
                    Comentario = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AvaliadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvaliacoesObra", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CriteriosObra",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjetoObraId = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Peso = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CriteriosObra", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItensMQT",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjetoObraId = table.Column<int>(type: "int", nullable: false),
                    CodigoIndexacao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Unidade = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Quantidade = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    NaoPrevisto = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensMQT", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItensPropostaEmpreiteiro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropostaEmpreiteiroId = table.Column<int>(type: "int", nullable: false),
                    ItemMQTId = table.Column<int>(type: "int", nullable: false),
                    Incluido = table.Column<bool>(type: "bit", nullable: false),
                    QuantidadeFornecida = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    PrecoUnitario = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensPropostaEmpreiteiro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensPropostaEmpreiteiro_ItensMQT_ItemMQTId",
                        column: x => x.ItemMQTId,
                        principalTable: "ItensMQT",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProjetoObraAnexos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjetoObraId = table.Column<int>(type: "int", nullable: false),
                    NomeArquivo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CaminhoArquivo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    TipoDocumento = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    DataUpload = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjetoObraAnexos", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProjetosObra",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Designacao = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Local = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Cliente = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValorEstimado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    Prazo = table.Column<int>(type: "int", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EmailsNotificacao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CriadoPor = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    PropostaVencedoraId = table.Column<int>(type: "int", nullable: true),
                    ValorAdjudicado = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    DataAdjudicacao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ResponsavelAdjudicacao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    JustificativaAdjudicacao = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjetosObra", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PropostasEmpreiteiro",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProjetoObraId = table.Column<int>(type: "int", nullable: false),
                    Empreiteiro = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    PrazoEntregaDias = table.Column<int>(type: "int", nullable: true),
                    ValidadeProposta = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Observacoes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropostasEmpreiteiro", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropostasEmpreiteiro_ProjetosObra_ProjetoObraId",
                        column: x => x.ProjetoObraId,
                        principalTable: "ProjetosObra",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AvaliacoesObra_CriterioObraId",
                table: "AvaliacoesObra",
                column: "CriterioObraId");

            migrationBuilder.CreateIndex(
                name: "IX_AvaliacoesObra_PropostaEmpreiteiroId_CriterioObraId",
                table: "AvaliacoesObra",
                columns: new[] { "PropostaEmpreiteiroId", "CriterioObraId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CriteriosObra_ProjetoObraId",
                table: "CriteriosObra",
                column: "ProjetoObraId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensMQT_ProjetoObraId",
                table: "ItensMQT",
                column: "ProjetoObraId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensPropostaEmpreiteiro_ItemMQTId",
                table: "ItensPropostaEmpreiteiro",
                column: "ItemMQTId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensPropostaEmpreiteiro_PropostaEmpreiteiroId",
                table: "ItensPropostaEmpreiteiro",
                column: "PropostaEmpreiteiroId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjetoObraAnexos_ProjetoObraId",
                table: "ProjetoObraAnexos",
                column: "ProjetoObraId");

            migrationBuilder.CreateIndex(
                name: "IX_ProjetosObra_PropostaVencedoraId",
                table: "ProjetosObra",
                column: "PropostaVencedoraId");

            migrationBuilder.CreateIndex(
                name: "IX_PropostasEmpreiteiro_ProjetoObraId",
                table: "PropostasEmpreiteiro",
                column: "ProjetoObraId");

            migrationBuilder.AddForeignKey(
                name: "FK_AvaliacoesObra_CriteriosObra_CriterioObraId",
                table: "AvaliacoesObra",
                column: "CriterioObraId",
                principalTable: "CriteriosObra",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AvaliacoesObra_PropostasEmpreiteiro_PropostaEmpreiteiroId",
                table: "AvaliacoesObra",
                column: "PropostaEmpreiteiroId",
                principalTable: "PropostasEmpreiteiro",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_CriteriosObra_ProjetosObra_ProjetoObraId",
                table: "CriteriosObra",
                column: "ProjetoObraId",
                principalTable: "ProjetosObra",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ItensMQT_ProjetosObra_ProjetoObraId",
                table: "ItensMQT",
                column: "ProjetoObraId",
                principalTable: "ProjetosObra",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ItensPropostaEmpreiteiro_PropostasEmpreiteiro_PropostaEmpreiteiroId",
                table: "ItensPropostaEmpreiteiro",
                column: "PropostaEmpreiteiroId",
                principalTable: "PropostasEmpreiteiro",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjetoObraAnexos_ProjetosObra_ProjetoObraId",
                table: "ProjetoObraAnexos",
                column: "ProjetoObraId",
                principalTable: "ProjetosObra",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjetosObra_PropostasEmpreiteiro_PropostaVencedoraId",
                table: "ProjetosObra",
                column: "PropostaVencedoraId",
                principalTable: "PropostasEmpreiteiro",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjetosObra_PropostasEmpreiteiro_PropostaVencedoraId",
                table: "ProjetosObra");

            migrationBuilder.DropTable(
                name: "AvaliacoesObra");

            migrationBuilder.DropTable(
                name: "ItensPropostaEmpreiteiro");

            migrationBuilder.DropTable(
                name: "ProjetoObraAnexos");

            migrationBuilder.DropTable(
                name: "CriteriosObra");

            migrationBuilder.DropTable(
                name: "ItensMQT");

            migrationBuilder.DropTable(
                name: "PropostasEmpreiteiro");

            migrationBuilder.DropTable(
                name: "ProjetosObra");
        }
    }
}
