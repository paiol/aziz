using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComparacaoPropostas.Migrations
{
    /// <inheritdoc />
    public partial class ItemPedidoLigaAPedidoProposta : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ItensPedido_Processos_ProcessoId",
                table: "ItensPedido");

            migrationBuilder.RenameColumn(
                name: "ProcessoId",
                table: "ItensPedido",
                newName: "PedidoPropostaId");

            migrationBuilder.RenameIndex(
                name: "IX_ItensPedido_ProcessoId",
                table: "ItensPedido",
                newName: "IX_ItensPedido_PedidoPropostaId");

            migrationBuilder.AddForeignKey(
                name: "FK_ItensPedido_Pedidos_PedidoPropostaId",
                table: "ItensPedido",
                column: "PedidoPropostaId",
                principalTable: "Pedidos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ItensPedido_Pedidos_PedidoPropostaId",
                table: "ItensPedido");

            migrationBuilder.RenameColumn(
                name: "PedidoPropostaId",
                table: "ItensPedido",
                newName: "ProcessoId");

            migrationBuilder.RenameIndex(
                name: "IX_ItensPedido_PedidoPropostaId",
                table: "ItensPedido",
                newName: "IX_ItensPedido_ProcessoId");

            migrationBuilder.AddForeignKey(
                name: "FK_ItensPedido_Processos_ProcessoId",
                table: "ItensPedido",
                column: "ProcessoId",
                principalTable: "Processos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
