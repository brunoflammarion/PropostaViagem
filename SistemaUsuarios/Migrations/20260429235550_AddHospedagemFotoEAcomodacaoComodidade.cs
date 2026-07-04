using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaUsuarios.Migrations
{
    /// <inheritdoc />
    public partial class AddHospedagemFotoEAcomodacaoComodidade : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AcomodacaoComodidades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcomodacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcomodacaoComodidades", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcomodacaoComodidades_Acomodacoes_AcomodacaoId",
                        column: x => x.AcomodacaoId,
                        principalTable: "Acomodacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "HospedagemFotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HospedagemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaminhoFoto = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Principal = table.Column<bool>(type: "bit", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HospedagemFotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HospedagemFotos_Hospedagens_HospedagemId",
                        column: x => x.HospedagemId,
                        principalTable: "Hospedagens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcomodacaoComodidades_AcomodacaoId",
                table: "AcomodacaoComodidades",
                column: "AcomodacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_HospedagemFotos_HospedagemId",
                table: "HospedagemFotos",
                column: "HospedagemId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcomodacaoComodidades");

            migrationBuilder.DropTable(
                name: "HospedagemFotos");
        }
    }
}
