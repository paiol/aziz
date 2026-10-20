using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComparacaoPropostas.Migrations
{
    /// <inheritdoc />
    public partial class AddAirtableSync : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AirtableLinks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TabelaId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Chave = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    RecordId = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    ProcessoId = table.Column<int>(type: "int", nullable: true),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AirtableLinks", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AirtableSyncPendentes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    EntidadeId = table.Column<int>(type: "int", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Tentativas = table.Column<int>(type: "int", nullable: false),
                    ProximaTentativaEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Erro = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ProcessadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AirtableSyncPendentes", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AirtableLinks_ProcessoId",
                table: "AirtableLinks",
                column: "ProcessoId");

            migrationBuilder.CreateIndex(
                name: "IX_AirtableLinks_TabelaId_Chave",
                table: "AirtableLinks",
                columns: new[] { "TabelaId", "Chave" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AirtableSyncPendentes_ProcessadoEm_ProximaTentativaEm",
                table: "AirtableSyncPendentes",
                columns: new[] { "ProcessadoEm", "ProximaTentativaEm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AirtableLinks");

            migrationBuilder.DropTable(
                name: "AirtableSyncPendentes");
        }
    }
}
