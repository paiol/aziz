using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComparacaoPropostas.Migrations
{
    /// <inheritdoc />
    public partial class EvolucaoModuloAquisicao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Criar tabela Avaliadores
            migrationBuilder.CreateTable(
                name: "Avaliadores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Perfil = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false, defaultValue: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETUTCDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Avaliadores", x => x.Id);
                });

            // Inserir avaliador padrão inicial
            migrationBuilder.Sql(@"
                IF NOT EXISTS (SELECT 1 FROM Avaliadores WHERE Nome = 'Avaliador Técnico')
                BEGIN
                    INSERT INTO Avaliadores (Nome, Perfil, Email, Ativo, CriadoEm)
                    VALUES ('Avaliador Técnico', 'Comissão Técnica', 'avaliacao@empresa.cv', 1, GETUTCDATE());
                END
            ");

            // 2. Adicionar colunas a Propostas
            migrationBuilder.AddColumn<string>(
                name: "Moeda",
                table: "Propostas",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: false,
                defaultValue: "CVE");

            migrationBuilder.AddColumn<decimal>(
                name: "TaxaCambio",
                table: "Propostas",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 110.265m);

            migrationBuilder.AddColumn<string>(
                name: "Garantia",
                table: "Propostas",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            // 3. Adicionar colunas de Adjudicação e Câmbio a Processos
            migrationBuilder.AddColumn<decimal>(
                name: "TaxaCambioPadrao",
                table: "Processos",
                type: "decimal(18,4)",
                precision: 18,
                scale: 4,
                nullable: false,
                defaultValue: 110.265m);

            migrationBuilder.AddColumn<int>(
                name: "PropostaVencedoraId",
                table: "Processos",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorAdjudicado",
                table: "Processos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValorAdjudicadoMoeda",
                table: "Processos",
                type: "nvarchar(10)",
                maxLength: 10,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "ValorAdjudicadoCVE",
                table: "Processos",
                type: "decimal(18,2)",
                precision: 18,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PontuacaoAdjudicada",
                table: "Processos",
                type: "decimal(5,2)",
                precision: 5,
                scale: 2,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DataAdjudicacao",
                table: "Processos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ResponsavelAdjudicacao",
                table: "Processos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "JustificativaAdjudicacao",
                table: "Processos",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "EmailResultadoEnviadoEm",
                table: "Processos",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Processos_PropostaVencedoraId",
                table: "Processos",
                column: "PropostaVencedoraId");

            migrationBuilder.AddForeignKey(
                name: "FK_Processos_Propostas_PropostaVencedoraId",
                table: "Processos",
                column: "PropostaVencedoraId",
                principalTable: "Propostas",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // 4. Migração segura de Avaliacoes
            migrationBuilder.AddColumn<int>(
                name: "AvaliadorId",
                table: "Avaliacoes",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AvaliadoEm",
                table: "Avaliacoes",
                type: "datetime2",
                nullable: false,
                defaultValueSql: "GETUTCDATE()");

            // Vincular avaliações antigas ao avaliador inicial e normalizar notas
            migrationBuilder.Sql(@"
                DECLARE @AvaliadorPadraoId INT;
                SELECT TOP 1 @AvaliadorPadraoId = Id FROM Avaliadores WHERE Ativo = 1 ORDER BY Id ASC;

                IF @AvaliadorPadraoId IS NOT NULL
                BEGIN
                    UPDATE Avaliacoes 
                    SET AvaliadorId = @AvaliadorPadraoId 
                    WHERE AvaliadorId IS NULL;
                END

                -- Normalizar notas existentes para o intervalo 1 a 5
                UPDATE Avaliacoes 
                SET Nota = CASE 
                    WHEN Nota > 5 THEN 5 
                    WHEN Nota < 1 THEN 1 
                    ELSE ROUND(Nota, 0) 
                END
                WHERE Nota IS NOT NULL;
            ");

            // Tornar AvaliadorId obrigatório após migrar dados
            migrationBuilder.AlterColumn<int>(
                name: "AvaliadorId",
                table: "Avaliacoes",
                type: "int",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AlterColumn<int>(
                name: "Nota",
                table: "Avaliacoes",
                type: "int",
                nullable: false,
                oldClrType: typeof(decimal),
                oldType: "decimal(18,2)");

            // Remover índice antigo se existir
            migrationBuilder.Sql(@"
                IF EXISTS (SELECT 1 FROM sys.indexes WHERE name = 'IX_Avaliacoes_PropostaId_CriterioId' AND object_id = OBJECT_ID('Avaliacoes'))
                BEGIN
                    DROP INDEX IX_Avaliacoes_PropostaId_CriterioId ON Avaliacoes;
                END
            ");

            migrationBuilder.CreateIndex(
                name: "IX_Avaliacoes_PropostaId_CriterioId_AvaliadorId",
                table: "Avaliacoes",
                columns: new[] { "PropostaId", "CriterioId", "AvaliadorId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Avaliacoes_AvaliadorId",
                table: "Avaliacoes",
                column: "AvaliadorId");

            migrationBuilder.AddForeignKey(
                name: "FK_Avaliacoes_Avaliadores_AvaliadorId",
                table: "Avaliacoes",
                column: "AvaliadorId",
                principalTable: "Avaliadores",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Avaliacoes_Avaliadores_AvaliadorId",
                table: "Avaliacoes");

            migrationBuilder.DropForeignKey(
                name: "FK_Processos_Propostas_PropostaVencedoraId",
                table: "Processos");

            migrationBuilder.DropIndex(
                name: "IX_Processos_PropostaVencedoraId",
                table: "Processos");

            migrationBuilder.DropIndex(
                name: "IX_Avaliacoes_PropostaId_CriterioId_AvaliadorId",
                table: "Avaliacoes");

            migrationBuilder.DropIndex(
                name: "IX_Avaliacoes_AvaliadorId",
                table: "Avaliacoes");

            migrationBuilder.DropColumn(
                name: "AvaliadorId",
                table: "Avaliacoes");

            migrationBuilder.DropColumn(
                name: "AvaliadoEm",
                table: "Avaliacoes");

            migrationBuilder.DropColumn(
                name: "Moeda",
                table: "Propostas");

            migrationBuilder.DropColumn(
                name: "TaxaCambio",
                table: "Propostas");

            migrationBuilder.DropColumn(
                name: "Garantia",
                table: "Propostas");

            migrationBuilder.DropColumn(
                name: "TaxaCambioPadrao",
                table: "Processos");

            migrationBuilder.DropColumn(
                name: "PropostaVencedoraId",
                table: "Processos");

            migrationBuilder.DropColumn(
                name: "ValorAdjudicado",
                table: "Processos");

            migrationBuilder.DropColumn(
                name: "ValorAdjudicadoMoeda",
                table: "Processos");

            migrationBuilder.DropColumn(
                name: "ValorAdjudicadoCVE",
                table: "Processos");

            migrationBuilder.DropColumn(
                name: "PontuacaoAdjudicada",
                table: "Processos");

            migrationBuilder.DropColumn(
                name: "DataAdjudicacao",
                table: "Processos");

            migrationBuilder.DropColumn(
                name: "ResponsavelAdjudicacao",
                table: "Processos");

            migrationBuilder.DropColumn(
                name: "JustificativaAdjudicacao",
                table: "Processos");

            migrationBuilder.DropColumn(
                name: "EmailResultadoEnviadoEm",
                table: "Processos");

            migrationBuilder.DropTable(
                name: "Avaliadores");

            migrationBuilder.AlterColumn<decimal>(
                name: "Nota",
                table: "Avaliacoes",
                type: "decimal(18,2)",
                nullable: false,
                oldClrType: typeof(int),
                oldType: "int");
        }
    }
}
