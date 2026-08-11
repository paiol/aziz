using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComparacaoPropostas.Migrations
{
    /// <inheritdoc />
    public partial class AddItemMaterialHierarquia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ItemPaiId",
                table: "ItensMaterial",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ItensMaterial_ItemPaiId",
                table: "ItensMaterial",
                column: "ItemPaiId");

            migrationBuilder.AddForeignKey(
                name: "FK_ItensMaterial_ItensMaterial_ItemPaiId",
                table: "ItensMaterial",
                column: "ItemPaiId",
                principalTable: "ItensMaterial",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ItensMaterial_ItensMaterial_ItemPaiId",
                table: "ItensMaterial");

            migrationBuilder.DropIndex(
                name: "IX_ItensMaterial_ItemPaiId",
                table: "ItensMaterial");

            migrationBuilder.DropColumn(
                name: "ItemPaiId",
                table: "ItensMaterial");
        }
    }
}
