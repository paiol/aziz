using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComparacaoPropostas.Migrations
{
    /// <inheritdoc />
    public partial class ReplaceCriteriosComProcessoDireto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Avaliacoes_ProcessosCriterio_ProcessoCriterioId",
                table: "Avaliacoes");

            migrationBuilder.DropTable(
                name: "ProcessosCriterio");

            migrationBuilder.DropTable(
                name: "CriteriosAvaliacao");

            migrationBuilder.RenameColumn(
                name: "ProcessoCriterioId",
                table: "Avaliacoes",
                newName: "CriterioId");

            migrationBuilder.RenameIndex(
                name: "IX_Avaliacoes_PropostaId_ProcessoCriterioId",
                table: "Avaliacoes",
                newName: "IX_Avaliacoes_PropostaId_CriterioId");

            migrationBuilder.RenameIndex(
                name: "IX_Avaliacoes_ProcessoCriterioId",
                table: "Avaliacoes",
                newName: "IX_Avaliacoes_CriterioId");

            migrationBuilder.CreateTable(
                name: "Criterios",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcessoId = table.Column<int>(type: "int", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Categoria = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Peso = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Criterios", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Criterios_Processos_ProcessoId",
                        column: x => x.ProcessoId,
                        principalTable: "Processos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Criterios_ProcessoId",
                table: "Criterios",
                column: "ProcessoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Avaliacoes_Criterios_CriterioId",
                table: "Avaliacoes",
                column: "CriterioId",
                principalTable: "Criterios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Avaliacoes_Criterios_CriterioId",
                table: "Avaliacoes");

            migrationBuilder.DropTable(
                name: "Criterios");

            migrationBuilder.RenameColumn(
                name: "CriterioId",
                table: "Avaliacoes",
                newName: "ProcessoCriterioId");

            migrationBuilder.RenameIndex(
                name: "IX_Avaliacoes_PropostaId_CriterioId",
                table: "Avaliacoes",
                newName: "IX_Avaliacoes_PropostaId_ProcessoCriterioId");

            migrationBuilder.RenameIndex(
                name: "IX_Avaliacoes_CriterioId",
                table: "Avaliacoes",
                newName: "IX_Avaliacoes_ProcessoCriterioId");

            migrationBuilder.CreateTable(
                name: "CriteriosAvaliacao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Categoria = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Dominio = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(max)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CriteriosAvaliacao", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ProcessosCriterio",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    CriterioAvaliacaoId = table.Column<int>(type: "int", nullable: false),
                    ProcessoId = table.Column<int>(type: "int", nullable: false),
                    Peso = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcessosCriterio", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcessosCriterio_CriteriosAvaliacao_CriterioAvaliacaoId",
                        column: x => x.CriterioAvaliacaoId,
                        principalTable: "CriteriosAvaliacao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProcessosCriterio_Processos_ProcessoId",
                        column: x => x.ProcessoId,
                        principalTable: "Processos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ProcessosCriterio_CriterioAvaliacaoId",
                table: "ProcessosCriterio",
                column: "CriterioAvaliacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcessosCriterio_ProcessoId_CriterioAvaliacaoId",
                table: "ProcessosCriterio",
                columns: new[] { "ProcessoId", "CriterioAvaliacaoId" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Avaliacoes_ProcessosCriterio_ProcessoCriterioId",
                table: "Avaliacoes",
                column: "ProcessoCriterioId",
                principalTable: "ProcessosCriterio",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
