using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComparacaoPropostas.Migrations
{
    /// <inheritdoc />
    public partial class InventarioEPackingListGenerico : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // O Packing List deixou de ser só da Huawei.
            migrationBuilder.Sql("UPDATE EntradasStock SET Tipo = 'PackingList' WHERE Tipo = 'PackingListHuawei'");

            migrationBuilder.AddColumn<int>(
                name: "InventarioStockId",
                table: "MovimentosStock",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InventariosStock",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Area = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Data = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Localizacao = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Responsavel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    NomeFicheiro = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    FechadoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InventariosStock", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LinhasInventario",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    InventarioStockId = table.Column<int>(type: "int", nullable: false),
                    MaterialStockId = table.Column<int>(type: "int", nullable: true),
                    Codigo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Unidade = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    QuantidadeContada = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    QuantidadeSistema = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true),
                    Series = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Observacoes = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LinhasInventario", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LinhasInventario_InventariosStock_InventarioStockId",
                        column: x => x.InventarioStockId,
                        principalTable: "InventariosStock",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LinhasInventario_MateriaisStock_MaterialStockId",
                        column: x => x.MaterialStockId,
                        principalTable: "MateriaisStock",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosStock_InventarioStockId",
                table: "MovimentosStock",
                column: "InventarioStockId");

            migrationBuilder.CreateIndex(
                name: "IX_LinhasInventario_InventarioStockId",
                table: "LinhasInventario",
                column: "InventarioStockId");

            migrationBuilder.CreateIndex(
                name: "IX_LinhasInventario_MaterialStockId",
                table: "LinhasInventario",
                column: "MaterialStockId");

            migrationBuilder.AddForeignKey(
                name: "FK_MovimentosStock_InventariosStock_InventarioStockId",
                table: "MovimentosStock",
                column: "InventarioStockId",
                principalTable: "InventariosStock",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE EntradasStock SET Tipo = 'PackingListHuawei' WHERE Tipo = 'PackingList'");

            migrationBuilder.DropForeignKey(
                name: "FK_MovimentosStock_InventariosStock_InventarioStockId",
                table: "MovimentosStock");

            migrationBuilder.DropTable(
                name: "LinhasInventario");

            migrationBuilder.DropTable(
                name: "InventariosStock");

            migrationBuilder.DropIndex(
                name: "IX_MovimentosStock_InventarioStockId",
                table: "MovimentosStock");

            migrationBuilder.DropColumn(
                name: "InventarioStockId",
                table: "MovimentosStock");
        }
    }
}
