using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaUsuarios.Migrations
{
    /// <inheritdoc />
    public partial class AddVooPassageiro : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Voos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropostaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NumeroVoo = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TipoVoo = table.Column<int>(type: "int", nullable: false),
                    Companhia = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Classe = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    Duracao = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Origem = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Destino = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    HorarioSaida = table.Column<DateTime>(type: "datetime2", nullable: true),
                    HorarioChegada = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Voos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Voos_Propostas_PropostaId",
                        column: x => x.PropostaId,
                        principalTable: "Propostas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PassageirosVoo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    VooId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Assento = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    BagagensDespachadas = table.Column<int>(type: "int", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PassageirosVoo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PassageirosVoo_Voos_VooId",
                        column: x => x.VooId,
                        principalTable: "Voos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PassageirosVoo_VooId",
                table: "PassageirosVoo",
                column: "VooId");

            migrationBuilder.CreateIndex(
                name: "IX_Voos_PropostaId",
                table: "Voos",
                column: "PropostaId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PassageirosVoo");

            migrationBuilder.DropTable(
                name: "Voos");
        }
    }
}
