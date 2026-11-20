using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComparacaoPropostas.Migrations
{
    /// <inheritdoc />
    public partial class LigarObrasAoCapex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProjetoPlanoId",
                table: "ProjetosObra",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjetosObra_ProjetoPlanoId",
                table: "ProjetosObra",
                column: "ProjetoPlanoId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProjetosObra_ProjetosPlano_ProjetoPlanoId",
                table: "ProjetosObra",
                column: "ProjetoPlanoId",
                principalTable: "ProjetosPlano",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProjetosObra_ProjetosPlano_ProjetoPlanoId",
                table: "ProjetosObra");

            migrationBuilder.DropIndex(
                name: "IX_ProjetosObra_ProjetoPlanoId",
                table: "ProjetosObra");

            migrationBuilder.DropColumn(
                name: "ProjetoPlanoId",
                table: "ProjetosObra");
        }
    }
}
