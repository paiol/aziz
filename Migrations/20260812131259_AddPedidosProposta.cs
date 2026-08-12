using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComparacaoPropostas.Migrations
{
    /// <inheritdoc />
    public partial class AddPedidosProposta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PedidoPropostaId",
                table: "Propostas",
                type: "int",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Pedidos",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    ProcessoId = table.Column<int>(type: "int", nullable: false),
                    Fornecedor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pedidos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Pedidos_Processos_ProcessoId",
                        column: x => x.ProcessoId,
                        principalTable: "Processos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ItensPedido",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    PedidoPropostaId = table.Column<int>(type: "int", nullable: false),
                    ItemMaterialId = table.Column<int>(type: "int", nullable: false),
                    QuantidadeSolicitada = table.Column<decimal>(type: "decimal(18,3)", precision: 18, scale: 3, nullable: false),
                    Observacao = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensPedido", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ItensPedido_ItensMaterial_ItemMaterialId",
                        column: x => x.ItemMaterialId,
                        principalTable: "ItensMaterial",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ItensPedido_Pedidos_PedidoPropostaId",
                        column: x => x.PedidoPropostaId,
                        principalTable: "Pedidos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Propostas_PedidoPropostaId",
                table: "Propostas",
                column: "PedidoPropostaId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensPedido_ItemMaterialId",
                table: "ItensPedido",
                column: "ItemMaterialId");

            migrationBuilder.CreateIndex(
                name: "IX_ItensPedido_PedidoPropostaId",
                table: "ItensPedido",
                column: "PedidoPropostaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_ProcessoId",
                table: "Pedidos",
                column: "ProcessoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Propostas_Pedidos_PedidoPropostaId",
                table: "Propostas",
                column: "PedidoPropostaId",
                principalTable: "Pedidos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Propostas_Pedidos_PedidoPropostaId",
                table: "Propostas");

            migrationBuilder.DropTable(
                name: "ItensPedido");

            migrationBuilder.DropTable(
                name: "Pedidos");

            migrationBuilder.DropIndex(
                name: "IX_Propostas_PedidoPropostaId",
                table: "Propostas");

            migrationBuilder.DropColumn(
                name: "PedidoPropostaId",
                table: "Propostas");
        }
    }
}
