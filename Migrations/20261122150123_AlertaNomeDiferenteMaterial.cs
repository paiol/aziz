using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComparacaoPropostas.Migrations
{
    /// <inheritdoc />
    public partial class AlertaNomeDiferenteMaterial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DivergenciasNomeMaterial",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaterialStockId = table.Column<int>(type: "int", nullable: false),
                    NomeCatalogo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    NomeRecebido = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Origem = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Referencia = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    EntradaStockId = table.Column<int>(type: "int", nullable: true),
                    InventarioStockId = table.Column<int>(type: "int", nullable: true),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ResolvidoEm = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DivergenciasNomeMaterial", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DivergenciasNomeMaterial_EntradasStock_EntradaStockId",
                        column: x => x.EntradaStockId,
                        principalTable: "EntradasStock",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DivergenciasNomeMaterial_InventariosStock_InventarioStockId",
                        column: x => x.InventarioStockId,
                        principalTable: "InventariosStock",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DivergenciasNomeMaterial_MateriaisStock_MaterialStockId",
                        column: x => x.MaterialStockId,
                        principalTable: "MateriaisStock",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DivergenciasNomeMaterial_EntradaStockId",
                table: "DivergenciasNomeMaterial",
                column: "EntradaStockId");

            migrationBuilder.CreateIndex(
                name: "IX_DivergenciasNomeMaterial_InventarioStockId",
                table: "DivergenciasNomeMaterial",
                column: "InventarioStockId");

            migrationBuilder.CreateIndex(
                name: "IX_DivergenciasNomeMaterial_MaterialStockId_Estado",
                table: "DivergenciasNomeMaterial",
                columns: new[] { "MaterialStockId", "Estado" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DivergenciasNomeMaterial");
        }
    }
}
