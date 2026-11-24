using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComparacaoPropostas.Migrations
{
    /// <inheritdoc />
    public partial class CodigosFornecedorMaterial : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "CodigoProvisorio",
                table: "MateriaisStock",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "CodigosFornecedorMaterial",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaterialStockId = table.Column<int>(type: "int", nullable: false),
                    Fornecedor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    Codigo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Modelo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CodigosFornecedorMaterial", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CodigosFornecedorMaterial_MateriaisStock_MaterialStockId",
                        column: x => x.MaterialStockId,
                        principalTable: "MateriaisStock",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CodigosFornecedorMaterial_Codigo",
                table: "CodigosFornecedorMaterial",
                column: "Codigo");

            migrationBuilder.CreateIndex(
                name: "IX_CodigosFornecedorMaterial_Fornecedor_Codigo",
                table: "CodigosFornecedorMaterial",
                columns: new[] { "Fornecedor", "Codigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CodigosFornecedorMaterial_MaterialStockId",
                table: "CodigosFornecedorMaterial",
                column: "MaterialStockId");

            // Materiais que entraram só por Packing List (sem nenhuma contagem do SAP): o código é do
            // fornecedor (ex.: Part Number Huawei) — ficam sem código SAP até a DEI os associar, e o código
            // fica registado como código desse fornecedor.
            migrationBuilder.Sql(@"
UPDATE MateriaisStock SET CodigoProvisorio = 1
WHERE Id IN (SELECT mv.MaterialStockId FROM MovimentosStock mv JOIN EntradasStock e ON e.Id = mv.EntradaStockId WHERE e.Tipo = 'PackingList')
  AND Id NOT IN (SELECT MaterialStockId FROM MovimentosStock WHERE InventarioStockId IS NOT NULL);

INSERT INTO CodigosFornecedorMaterial (MaterialStockId, Fornecedor, Codigo, Modelo, Descricao, CriadoEm)
SELECT m.Id,
       LEFT(COALESCE((SELECT TOP 1 e.Fornecedor FROM MovimentosStock mv JOIN EntradasStock e ON e.Id = mv.EntradaStockId
                      WHERE mv.MaterialStockId = m.Id AND e.Fornecedor IS NOT NULL ORDER BY e.Id), m.Fabricante, N'(fornecedor não indicado)'), 150),
       m.Codigo, m.Modelo, m.Descricao, SYSUTCDATETIME()
FROM MateriaisStock m
WHERE m.CodigoProvisorio = 1
  AND NOT EXISTS (SELECT 1 FROM CodigosFornecedorMaterial c WHERE c.MaterialStockId = m.Id AND c.Codigo = m.Codigo);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CodigosFornecedorMaterial");

            migrationBuilder.DropColumn(
                name: "CodigoProvisorio",
                table: "MateriaisStock");
        }
    }
}
