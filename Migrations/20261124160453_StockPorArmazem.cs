using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComparacaoPropostas.Migrations
{
    /// <inheritdoc />
    public partial class StockPorArmazem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Armazem",
                table: "MovimentosStock",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Armazem",
                table: "InventariosStock",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Centro",
                table: "InventariosStock",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Armazem",
                table: "EntradasStock",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ArmazensStock",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Centro = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    Codigo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ArmazensStock", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosStock_MaterialStockId_Area_Armazem",
                table: "MovimentosStock",
                columns: new[] { "MaterialStockId", "Area", "Armazem" });

            migrationBuilder.CreateIndex(
                name: "IX_ArmazensStock_Codigo",
                table: "ArmazensStock",
                column: "Codigo",
                unique: true);

            // O inventário da Praia (ficheiro SAP, depósito CPRA / DPRP) foi carregado antes de haver armazéns:
            // fica ligado ao armazém DPRP, assim como os ajustes de stock que gerou.
            migrationBuilder.Sql(@"
IF EXISTS (SELECT 1 FROM InventariosStock WHERE Armazem IS NULL AND Descricao LIKE N'Armaz%Praia[_]Projeto%')
BEGIN
    IF NOT EXISTS (SELECT 1 FROM ArmazensStock WHERE Codigo = 'DPRP')
        INSERT INTO ArmazensStock (Centro, Codigo, Nome) VALUES ('CPRA', 'DPRP', 'Praia_Projeto');
    UPDATE InventariosStock SET Armazem = 'DPRP', Centro = 'CPRA', Localizacao = 'Praia_Projeto'
        WHERE Armazem IS NULL AND Descricao LIKE N'Armaz%Praia[_]Projeto%';
END
UPDATE m SET m.Armazem = i.Armazem
    FROM MovimentosStock m JOIN InventariosStock i ON m.InventarioStockId = i.Id
    WHERE m.Armazem IS NULL AND i.Armazem IS NOT NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ArmazensStock");

            migrationBuilder.DropIndex(
                name: "IX_MovimentosStock_MaterialStockId_Area_Armazem",
                table: "MovimentosStock");

            migrationBuilder.DropColumn(
                name: "Armazem",
                table: "MovimentosStock");

            migrationBuilder.DropColumn(
                name: "Armazem",
                table: "InventariosStock");

            migrationBuilder.DropColumn(
                name: "Centro",
                table: "InventariosStock");

            migrationBuilder.DropColumn(
                name: "Armazem",
                table: "EntradasStock");
        }
    }
}
