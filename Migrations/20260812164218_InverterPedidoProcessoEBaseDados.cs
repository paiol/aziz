using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComparacaoPropostas.Migrations
{
    /// <inheritdoc />
    public partial class InverterPedidoProcessoEBaseDados : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ItensPedido_Pedidos_PedidoPropostaId",
                table: "ItensPedido");

            migrationBuilder.DropForeignKey(
                name: "FK_Pedidos_Processos_ProcessoId",
                table: "Pedidos");

            migrationBuilder.DropForeignKey(
                name: "FK_Propostas_Pedidos_PedidoPropostaId",
                table: "Propostas");

            migrationBuilder.DropIndex(
                name: "IX_Propostas_PedidoPropostaId",
                table: "Propostas");

            migrationBuilder.DropIndex(
                name: "IX_Pedidos_ProcessoId",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "PedidoPropostaId",
                table: "Propostas");

            migrationBuilder.DropColumn(
                name: "ProcessoId",
                table: "Pedidos");

            migrationBuilder.RenameColumn(
                name: "TipoProcesso",
                table: "Processos",
                newName: "Fornecedor");

            migrationBuilder.RenameColumn(
                name: "Fornecedor",
                table: "Pedidos",
                newName: "TipoProposta");

            migrationBuilder.RenameColumn(
                name: "PedidoPropostaId",
                table: "ItensPedido",
                newName: "ProcessoId");

            migrationBuilder.RenameIndex(
                name: "IX_ItensPedido_PedidoPropostaId",
                table: "ItensPedido",
                newName: "IX_ItensPedido_ProcessoId");

            migrationBuilder.AddColumn<int>(
                name: "PedidoPropostaId",
                table: "Processos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Area",
                table: "Pedidos",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "PessoaCriou",
                table: "Pedidos",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.CreateTable(
                name: "ItensCore",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Categoria = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Unidade = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Dominio = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensCore", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItensEnergia",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Categoria = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Unidade = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Dominio = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensEnergia", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItensFbb",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Categoria = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Unidade = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Dominio = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensFbb", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ItensMbb",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Categoria = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Unidade = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Descricao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Dominio = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ItensMbb", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Processos_PedidoPropostaId",
                table: "Processos",
                column: "PedidoPropostaId",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_ItensPedido_Processos_ProcessoId",
                table: "ItensPedido",
                column: "ProcessoId",
                principalTable: "Processos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Processos_Pedidos_PedidoPropostaId",
                table: "Processos",
                column: "PedidoPropostaId",
                principalTable: "Pedidos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ItensPedido_Processos_ProcessoId",
                table: "ItensPedido");

            migrationBuilder.DropForeignKey(
                name: "FK_Processos_Pedidos_PedidoPropostaId",
                table: "Processos");

            migrationBuilder.DropTable(
                name: "ItensCore");

            migrationBuilder.DropTable(
                name: "ItensEnergia");

            migrationBuilder.DropTable(
                name: "ItensFbb");

            migrationBuilder.DropTable(
                name: "ItensMbb");

            migrationBuilder.DropIndex(
                name: "IX_Processos_PedidoPropostaId",
                table: "Processos");

            migrationBuilder.DropColumn(
                name: "PedidoPropostaId",
                table: "Processos");

            migrationBuilder.DropColumn(
                name: "Area",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "PessoaCriou",
                table: "Pedidos");

            migrationBuilder.RenameColumn(
                name: "Fornecedor",
                table: "Processos",
                newName: "TipoProcesso");

            migrationBuilder.RenameColumn(
                name: "TipoProposta",
                table: "Pedidos",
                newName: "Fornecedor");

            migrationBuilder.RenameColumn(
                name: "ProcessoId",
                table: "ItensPedido",
                newName: "PedidoPropostaId");

            migrationBuilder.RenameIndex(
                name: "IX_ItensPedido_ProcessoId",
                table: "ItensPedido",
                newName: "IX_ItensPedido_PedidoPropostaId");

            migrationBuilder.AddColumn<int>(
                name: "PedidoPropostaId",
                table: "Propostas",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProcessoId",
                table: "Pedidos",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Propostas_PedidoPropostaId",
                table: "Propostas",
                column: "PedidoPropostaId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_ProcessoId",
                table: "Pedidos",
                column: "ProcessoId");

            migrationBuilder.AddForeignKey(
                name: "FK_ItensPedido_Pedidos_PedidoPropostaId",
                table: "ItensPedido",
                column: "PedidoPropostaId",
                principalTable: "Pedidos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Pedidos_Processos_ProcessoId",
                table: "Pedidos",
                column: "ProcessoId",
                principalTable: "Processos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);

            migrationBuilder.AddForeignKey(
                name: "FK_Propostas_Pedidos_PedidoPropostaId",
                table: "Propostas",
                column: "PedidoPropostaId",
                principalTable: "Pedidos",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }
    }
}
