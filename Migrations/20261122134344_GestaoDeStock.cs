using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComparacaoPropostas.Migrations
{
    /// <inheritdoc />
    public partial class GestaoDeStock : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EntradasStock",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Tipo = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Area = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Numero = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Data = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Fornecedor = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Contrato = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Contrato1Mais1 = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Fatura = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    PurchaseOrder = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    ProjetoFornecedor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ProjetoPlanoId = table.Column<int>(type: "int", nullable: true),
                    PedidoPropostaId = table.Column<int>(type: "int", nullable: true),
                    Localizacao = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Observacoes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    NomeFicheiro = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EntradasStock", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EntradasStock_Pedidos_PedidoPropostaId",
                        column: x => x.PedidoPropostaId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EntradasStock_ProjetosPlano_ProjetoPlanoId",
                        column: x => x.ProjetoPlanoId,
                        principalTable: "ProjetosPlano",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MateriaisStock",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Codigo = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: false),
                    Modelo = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Unidade = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Fabricante = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    StockMinimo = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MateriaisStock", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "MovimentosStock",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaterialStockId = table.Column<int>(type: "int", nullable: false),
                    Area = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Quantidade = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Data = table.Column<DateTime>(type: "datetime2", nullable: false),
                    EntradaStockId = table.Column<int>(type: "int", nullable: true),
                    ProjetoPlanoId = table.Column<int>(type: "int", nullable: true),
                    Caixa = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Embalagem = table.Column<string>(type: "nvarchar(60)", maxLength: 60, nullable: true),
                    Site = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Destino = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Responsavel = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Observacoes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MovimentosStock", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MovimentosStock_EntradasStock_EntradaStockId",
                        column: x => x.EntradaStockId,
                        principalTable: "EntradasStock",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MovimentosStock_MateriaisStock_MaterialStockId",
                        column: x => x.MaterialStockId,
                        principalTable: "MateriaisStock",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MovimentosStock_ProjetosPlano_ProjetoPlanoId",
                        column: x => x.ProjetoPlanoId,
                        principalTable: "ProjetosPlano",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "NumerosSerieStock",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    MaterialStockId = table.Column<int>(type: "int", nullable: false),
                    Serie = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Area = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Caixa = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    EntradaStockId = table.Column<int>(type: "int", nullable: true),
                    MovimentoSaidaId = table.Column<int>(type: "int", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumerosSerieStock", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NumerosSerieStock_EntradasStock_EntradaStockId",
                        column: x => x.EntradaStockId,
                        principalTable: "EntradasStock",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NumerosSerieStock_MateriaisStock_MaterialStockId",
                        column: x => x.MaterialStockId,
                        principalTable: "MateriaisStock",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_NumerosSerieStock_MovimentosStock_MovimentoSaidaId",
                        column: x => x.MovimentoSaidaId,
                        principalTable: "MovimentosStock",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_EntradasStock_Contrato1Mais1",
                table: "EntradasStock",
                column: "Contrato1Mais1");

            migrationBuilder.CreateIndex(
                name: "IX_EntradasStock_PedidoPropostaId",
                table: "EntradasStock",
                column: "PedidoPropostaId");

            migrationBuilder.CreateIndex(
                name: "IX_EntradasStock_ProjetoPlanoId",
                table: "EntradasStock",
                column: "ProjetoPlanoId");

            migrationBuilder.CreateIndex(
                name: "IX_MateriaisStock_Codigo",
                table: "MateriaisStock",
                column: "Codigo",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosStock_EntradaStockId",
                table: "MovimentosStock",
                column: "EntradaStockId");

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosStock_MaterialStockId_Area",
                table: "MovimentosStock",
                columns: new[] { "MaterialStockId", "Area" });

            migrationBuilder.CreateIndex(
                name: "IX_MovimentosStock_ProjetoPlanoId",
                table: "MovimentosStock",
                column: "ProjetoPlanoId");

            migrationBuilder.CreateIndex(
                name: "IX_NumerosSerieStock_EntradaStockId",
                table: "NumerosSerieStock",
                column: "EntradaStockId");

            migrationBuilder.CreateIndex(
                name: "IX_NumerosSerieStock_MaterialStockId_Serie",
                table: "NumerosSerieStock",
                columns: new[] { "MaterialStockId", "Serie" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NumerosSerieStock_MovimentoSaidaId",
                table: "NumerosSerieStock",
                column: "MovimentoSaidaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "NumerosSerieStock");

            migrationBuilder.DropTable(
                name: "MovimentosStock");

            migrationBuilder.DropTable(
                name: "EntradasStock");

            migrationBuilder.DropTable(
                name: "MateriaisStock");
        }
    }
}
