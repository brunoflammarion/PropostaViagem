using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaUsuarios.Migrations
{
    /// <inheritdoc />
    public partial class Destino : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PropostaVisualizacoes_Propostas_PropostaId1",
                table: "PropostaVisualizacoes");

            migrationBuilder.DropIndex(
                name: "IX_PropostaVisualizacoes_PropostaId1",
                table: "PropostaVisualizacoes");

            migrationBuilder.DropColumn(
                name: "PropostaId1",
                table: "PropostaVisualizacoes");

            migrationBuilder.CreateTable(
                name: "Destinos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropostaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    DataChegada = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataSaida = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Pais = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Cidade = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Destinos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Destinos_Propostas_PropostaId",
                        column: x => x.PropostaId,
                        principalTable: "Propostas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "DestinoFotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaminhoFoto = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Principal = table.Column<bool>(type: "bit", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DestinoFotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DestinoFotos_Destinos_DestinoId",
                        column: x => x.DestinoId,
                        principalTable: "Destinos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DestinoFotos_DestinoId",
                table: "DestinoFotos",
                column: "DestinoId");

            migrationBuilder.CreateIndex(
                name: "IX_DestinoFotos_DestinoId_Ordem",
                table: "DestinoFotos",
                columns: new[] { "DestinoId", "Ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Destinos_PropostaId",
                table: "Destinos",
                column: "PropostaId");

            migrationBuilder.CreateIndex(
                name: "IX_Destinos_PropostaId_Ordem",
                table: "Destinos",
                columns: new[] { "PropostaId", "Ordem" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DestinoFotos");

            migrationBuilder.DropTable(
                name: "Destinos");

            migrationBuilder.AddColumn<Guid>(
                name: "PropostaId1",
                table: "PropostaVisualizacoes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropostaVisualizacoes_PropostaId1",
                table: "PropostaVisualizacoes",
                column: "PropostaId1");

            migrationBuilder.AddForeignKey(
                name: "FK_PropostaVisualizacoes_Propostas_PropostaId1",
                table: "PropostaVisualizacoes",
                column: "PropostaId1",
                principalTable: "Propostas",
                principalColumn: "Id");
        }
    }
}
