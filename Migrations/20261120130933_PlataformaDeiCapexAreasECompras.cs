using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComparacaoPropostas.Migrations
{
    /// <inheritdoc />
    public partial class PlataformaDeiCapexAreasECompras : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProjetosPlano_PlanoId",
                table: "ProjetosPlano");

            migrationBuilder.AddColumn<string>(
                name: "Area",
                table: "ProjetosPlano",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "DepInf");

            migrationBuilder.AddColumn<DateTime>(
                name: "DataDecisao",
                table: "ProjetosPlano",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "EstadoAprovacao",
                table: "ProjetosPlano",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Proposto");

            migrationBuilder.AddColumn<string>(
                name: "MotivoDecisao",
                table: "ProjetosPlano",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProjetoOrigemId",
                table: "ProjetosPlano",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ProjetoPlanoId",
                table: "Pedidos",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProjetosPlano_PlanoId_Area",
                table: "ProjetosPlano",
                columns: new[] { "PlanoId", "Area" });

            migrationBuilder.CreateIndex(
                name: "IX_ProjetosPlano_ProjetoOrigemId",
                table: "ProjetosPlano",
                column: "ProjetoOrigemId");

            migrationBuilder.CreateIndex(
                name: "IX_Pedidos_ProjetoPlanoId",
                table: "Pedidos",
                column: "ProjetoPlanoId");

            migrationBuilder.AddForeignKey(
                name: "FK_Pedidos_ProjetosPlano_ProjetoPlanoId",
                table: "Pedidos",
                column: "ProjetoPlanoId",
                principalTable: "ProjetosPlano",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProjetosPlano_ProjetosPlano_ProjetoOrigemId",
                table: "ProjetosPlano",
                column: "ProjetoOrigemId",
                principalTable: "ProjetosPlano",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Pedidos_ProjetosPlano_ProjetoPlanoId",
                table: "Pedidos");

            migrationBuilder.DropForeignKey(
                name: "FK_ProjetosPlano_ProjetosPlano_ProjetoOrigemId",
                table: "ProjetosPlano");

            migrationBuilder.DropIndex(
                name: "IX_ProjetosPlano_PlanoId_Area",
                table: "ProjetosPlano");

            migrationBuilder.DropIndex(
                name: "IX_ProjetosPlano_ProjetoOrigemId",
                table: "ProjetosPlano");

            migrationBuilder.DropIndex(
                name: "IX_Pedidos_ProjetoPlanoId",
                table: "Pedidos");

            migrationBuilder.DropColumn(
                name: "Area",
                table: "ProjetosPlano");

            migrationBuilder.DropColumn(
                name: "DataDecisao",
                table: "ProjetosPlano");

            migrationBuilder.DropColumn(
                name: "EstadoAprovacao",
                table: "ProjetosPlano");

            migrationBuilder.DropColumn(
                name: "MotivoDecisao",
                table: "ProjetosPlano");

            migrationBuilder.DropColumn(
                name: "ProjetoOrigemId",
                table: "ProjetosPlano");

            migrationBuilder.DropColumn(
                name: "ProjetoPlanoId",
                table: "Pedidos");

            migrationBuilder.CreateIndex(
                name: "IX_ProjetosPlano_PlanoId",
                table: "ProjetosPlano",
                column: "PlanoId");
        }
    }
}
