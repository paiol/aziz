using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComparacaoPropostas.Migrations
{
    /// <inheritdoc />
    public partial class AddStatusAlteradoEmAoProcesso : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "StatusAlteradoEm",
                table: "Processos",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            // Processos existentes não têm histórico de quando o Estado mudou pela
            // última vez — usa-se a data de criação como aproximação razoável.
            migrationBuilder.Sql("UPDATE Processos SET StatusAlteradoEm = CriadoEm;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "StatusAlteradoEm",
                table: "Processos");
        }
    }
}
