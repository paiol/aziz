using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComparacaoPropostas.Migrations
{
    /// <inheritdoc />
    public partial class AddAvaliacaoAutomatica : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "TipoAutomatico",
                table: "Criterios",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Nenhum");

            migrationBuilder.CreateTable(
                name: "MemoriaCalculoAvaliacao",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PropostaId = table.Column<int>(type: "int", nullable: false),
                    CriterioId = table.Column<int>(type: "int", nullable: false),
                    Nota = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false),
                    Justificativa = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    CalculadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MemoriaCalculoAvaliacao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MemoriaCalculoAvaliacao_Criterios_CriterioId",
                        column: x => x.CriterioId,
                        principalTable: "Criterios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MemoriaCalculoAvaliacao_Propostas_PropostaId",
                        column: x => x.PropostaId,
                        principalTable: "Propostas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MemoriaCalculoAvaliacao_CriterioId",
                table: "MemoriaCalculoAvaliacao",
                column: "CriterioId");

            migrationBuilder.CreateIndex(
                name: "IX_MemoriaCalculoAvaliacao_PropostaId_CriterioId",
                table: "MemoriaCalculoAvaliacao",
                columns: new[] { "PropostaId", "CriterioId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MemoriaCalculoAvaliacao");

            migrationBuilder.DropColumn(
                name: "TipoAutomatico",
                table: "Criterios");
        }
    }
}
