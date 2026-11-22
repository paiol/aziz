using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComparacaoPropostas.Migrations
{
    /// <inheritdoc />
    public partial class PlanoConcluidoEnvioScope : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ScopeEnviadoEm",
                table: "ProjetosPlano",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScopeErro",
                table: "ProjetosPlano",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "ScopePendente",
                table: "ProjetosPlano",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScopeProximaTentativa",
                table: "ProjetosPlano",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScopeRecordId",
                table: "ProjetosPlano",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScopeTentativas",
                table: "ProjetosPlano",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<DateTime>(
                name: "ConcluidoEm",
                table: "Planos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "Estado",
                table: "Planos",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ScopeEnviadoEm",
                table: "ProjetosPlano");

            migrationBuilder.DropColumn(
                name: "ScopeErro",
                table: "ProjetosPlano");

            migrationBuilder.DropColumn(
                name: "ScopePendente",
                table: "ProjetosPlano");

            migrationBuilder.DropColumn(
                name: "ScopeProximaTentativa",
                table: "ProjetosPlano");

            migrationBuilder.DropColumn(
                name: "ScopeRecordId",
                table: "ProjetosPlano");

            migrationBuilder.DropColumn(
                name: "ScopeTentativas",
                table: "ProjetosPlano");

            migrationBuilder.DropColumn(
                name: "ConcluidoEm",
                table: "Planos");

            migrationBuilder.DropColumn(
                name: "Estado",
                table: "Planos");
        }
    }
}
