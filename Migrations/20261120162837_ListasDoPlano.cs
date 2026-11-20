using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ComparacaoPropostas.Migrations
{
    /// <inheritdoc />
    public partial class ListasDoPlano : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ValoresLista",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Lista = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Valor = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ValoresLista", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "ValoresLista",
                columns: new[] { "Id", "Ativo", "Lista", "Ordem", "Valor" },
                values: new object[,]
                {
                    { 1, true, "Rubrica", 10, "CVT IMS & VoLTE new build (+SmartCare & PCRF) 5G" },
                    { 2, true, "Rubrica", 20, "Smartcare Roaming Care - POC" },
                    { 3, true, "Rubrica", 30, "Neutral Carrier Datacenter" },
                    { 4, true, "Rubrica", 40, "Projetos Data Center - Climatização DC2 e Corredor Acesso DC1" },
                    { 5, true, "Rubrica", 50, "Proj. Retificadores - subsituição em 6 sites e reforço 2 sites" },
                    { 6, true, "Rubrica", 60, "Proj. Segurança Energia - Bacia Retenção e Ext. Auto Fogo Sala A" },
                    { 7, true, "Rubrica", 70, "Proj. Baterias litio - Substitição em 3 sites" },
                    { 8, true, "Rubrica", 80, "Projetos Geradores 2026 - Novos 8 sites incluindo GlobalCVT" },
                    { 9, true, "Rubrica", 90, "Projeto Power Praia" },
                    { 10, true, "Rubrica", 100, "Projetos Geradores - Transitado 5 projetos" },
                    { 11, true, "Rubrica", 110, "Projeto Power Mindelo" },
                    { 12, true, "Rubrica", 120, "Hybrid Power - Novo 5 sites com recursos existentes" },
                    { 13, true, "Rubrica", 130, "Hybrid Power - Transitado 3 sites" },
                    { 14, true, "Rubrica", 140, "Licenças NetECO Power Room A e monitorização retificadores Eltek" },
                    { 15, true, "Rubrica", 150, "Hybrid Power - Novo 2 sites SL e BV" },
                    { 16, true, "Rubrica", 160, "BTS Móvel Eventos" },
                    { 17, true, "Rubrica", 170, "Ferramentas Fiscalização" },
                    { 18, true, "Rubrica", 180, "Proj. Edificio - Calheta S.Miguel e A.S.Filipe" },
                    { 19, true, "Rubrica", 190, "FTTH principais Cidades - novos  2026" },
                    { 20, true, "Rubrica", 200, "Projetos RAN - em curso & Eventos" },
                    { 21, true, "Rubrica", 210, "Projetos RAN - novos" },
                    { 22, true, "Tipo", 10, "ENR" },
                    { 23, true, "Tipo", 20, "EDF" },
                    { 24, true, "Tipo", 30, "RAN" },
                    { 25, true, "Tipo", 40, "PRC" },
                    { 26, true, "Coordenacao", 10, "CRN" },
                    { 27, true, "Coordenacao", 20, "CRS" },
                    { 28, true, "Coordenacao", 30, "SSM" },
                    { 29, true, "Coordenacao", 40, "CLI" },
                    { 30, true, "Coordenacao", 50, "CRF" },
                    { 31, true, "Coordenacao", 60, "NAC" },
                    { 32, true, "Ilha", 10, "SA" },
                    { 33, true, "Ilha", 20, "SV" },
                    { 34, true, "Ilha", 30, "SN" },
                    { 35, true, "Ilha", 40, "SL" },
                    { 36, true, "Ilha", 50, "BV" },
                    { 37, true, "Ilha", 60, "MO" },
                    { 38, true, "Ilha", 70, "ST-PR" },
                    { 39, true, "Ilha", 80, "ST-INT" },
                    { 40, true, "Ilha", 90, "FG" },
                    { 41, true, "Ilha", 100, "BR" },
                    { 42, true, "Ilha", 110, "NAC" },
                    { 43, true, "Classificacao", 10, "Proj. Novo Site" },
                    { 44, true, "Classificacao", 20, "Proj. Remotização" },
                    { 45, true, "Classificacao", 30, "Proj. Reengenharia" },
                    { 46, true, "Classificacao", 40, "Proj. Remanejamento" },
                    { 47, true, "Classificacao", 50, "Proj. Evento" },
                    { 48, true, "Classificacao", 60, "Proj. Upgrade" },
                    { 49, true, "Classificacao", 70, "Add Tech" },
                    { 50, true, "Classificacao", 80, "Eng./design" },
                    { 51, true, "Classificacao", 90, "Proj. Gerador" },
                    { 52, true, "Classificacao", 100, "Proj. Hybrid Power" },
                    { 53, true, "Classificacao", 110, "Proj. Baterias" },
                    { 54, true, "Classificacao", 120, "Proj. Modernização" },
                    { 55, true, "Classificacao", 130, "Proj. Retificador" },
                    { 56, true, "Classificacao", 140, "Proj. Beneficiação" },
                    { 57, true, "Classificacao", 150, "Proj. Edifício" },
                    { 58, true, "Classificacao", 160, "Proj. SAF" },
                    { 59, true, "Classificacao", 170, "Proj. Segurança" },
                    { 60, true, "Classificacao", 180, "Aquisição" },
                    { 61, true, "Classificacao", 190, "Processos" },
                    { 62, true, "Classificacao", 200, "Outros" },
                    { 63, true, "Capex", 10, "CAPEX 26" },
                    { 64, true, "Capex", 20, "COND_CAPEX 26" },
                    { 65, true, "Capex", 30, "CAPEX 27 _Transitado" },
                    { 66, true, "Capex", 40, "CAPEX 27 _Novo" },
                    { 67, true, "Capex", 50, "COND_CAPEX 27" },
                    { 68, true, "Capex", 60, "CAPEX 28" },
                    { 69, true, "OrigemIniciativa", 10, "CRN" },
                    { 70, true, "OrigemIniciativa", 20, "DOM" },
                    { 71, true, "OrigemIniciativa", 30, "DEI" },
                    { 72, true, "OrigemIniciativa", 40, "DLG" },
                    { 73, true, "Trimestre", 10, "T1" },
                    { 74, true, "Trimestre", 20, "T2" },
                    { 75, true, "Trimestre", 30, "T3" },
                    { 76, true, "Trimestre", 40, "T4" },
                    { 77, true, "Trimestre", 50, "Ano todo" },
                    { 78, true, "Zona", 10, "Urbano" },
                    { 79, true, "Zona", 20, "Rural" },
                    { 80, true, "Executante", 10, "DEI" },
                    { 81, true, "Executante", 20, "DOM" },
                    { 82, true, "Executante", 30, "Terceiro" },
                    { 83, true, "Executante", 40, "NA" },
                    { 84, true, "Executante", 50, "Chave na mão" },
                    { 85, true, "PerfilMontagem", 10, "Energia" },
                    { 86, true, "PerfilMontagem", 20, "Equipa" },
                    { 87, true, "PerfilMontagem", 30, "ST" },
                    { 88, true, "PerfilMontagem", 40, "SV" },
                    { 89, true, "SimNao", 10, "Sim" },
                    { 90, true, "SimNao", 20, "Não" },
                    { 91, true, "SimNao", 30, "NA" },
                    { 92, true, "NecessidadeAquisicao", 10, "Serv. Civil-abrigo" },
                    { 93, true, "NecessidadeAquisicao", 20, "Serv. Civil-traçado" },
                    { 94, true, "NecessidadeAquisicao", 30, "Serv. Desmont.-gerador" },
                    { 95, true, "NecessidadeAquisicao", 40, "Serv. Desmont.-PV" },
                    { 96, true, "NecessidadeAquisicao", 50, "Serv. Mont.-gerador" },
                    { 97, true, "NecessidadeAquisicao", 60, "Serv. Mont.-PV" },
                    { 98, true, "NecessidadeAquisicao", 70, "Equip. Gerador" },
                    { 99, true, "NecessidadeAquisicao", 80, "Serv. Conf. Escape" },
                    { 100, true, "NecessidadeAquisicao", 90, "Serv. Fisc. Civil" },
                    { 101, true, "NecessidadeAquisicao", 100, "Serv. Fisc. Mont." },
                    { 102, true, "NecessidadeAquisicao", 110, "NA" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ValoresLista_Lista_Valor",
                table: "ValoresLista",
                columns: new[] { "Lista", "Valor" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ValoresLista");
        }
    }
}
