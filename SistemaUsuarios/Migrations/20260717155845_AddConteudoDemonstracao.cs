using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaUsuarios.Migrations
{
    /// <inheritdoc />
    public partial class AddConteudoDemonstracao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ConteudoDemonstracaoOrigemId",
                table: "Propostas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsConteudoDemonstracao",
                table: "Propostas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<Guid>(
                name: "ConteudoDemonstracaoOrigemId",
                table: "Ofertas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsConteudoDemonstracao",
                table: "Ofertas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "ConteudosDemonstracao",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoConteudo = table.Column<int>(type: "int", nullable: false),
                    EntidadeOrigemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    NomeAdministrativo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    AplicarAutomaticamente = table.Column<bool>(type: "bit", nullable: false),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CriadoPorAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AtualizadoPorAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConteudosDemonstracao", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConteudosDemonstracao_AdminsPlataforma_AtualizadoPorAdminId",
                        column: x => x.AtualizadoPorAdminId,
                        principalTable: "AdminsPlataforma",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_ConteudosDemonstracao_AdminsPlataforma_CriadoPorAdminId",
                        column: x => x.CriadoPorAdminId,
                        principalTable: "AdminsPlataforma",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ConteudosDemonstracaoAplicados",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ConteudoDemonstracaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgenciaMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EntidadeClonadaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DataAplicacao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    StatusAplicacao = table.Column<int>(type: "int", nullable: false),
                    MensagemErro = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConteudosDemonstracaoAplicados", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConteudosDemonstracaoAplicados_ConteudosDemonstracao_ConteudoDemonstracaoId",
                        column: x => x.ConteudoDemonstracaoId,
                        principalTable: "ConteudosDemonstracao",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConteudosDemonstracao_AtualizadoPorAdminId",
                table: "ConteudosDemonstracao",
                column: "AtualizadoPorAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_ConteudosDemonstracao_CriadoPorAdminId",
                table: "ConteudosDemonstracao",
                column: "CriadoPorAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_ConteudosDemonstracaoAplicados_ConteudoDemonstracaoId_AgenciaMasterId",
                table: "ConteudosDemonstracaoAplicados",
                columns: new[] { "ConteudoDemonstracaoId", "AgenciaMasterId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConteudosDemonstracaoAplicados");

            migrationBuilder.DropTable(
                name: "ConteudosDemonstracao");

            migrationBuilder.DropColumn(
                name: "ConteudoDemonstracaoOrigemId",
                table: "Propostas");

            migrationBuilder.DropColumn(
                name: "IsConteudoDemonstracao",
                table: "Propostas");

            migrationBuilder.DropColumn(
                name: "ConteudoDemonstracaoOrigemId",
                table: "Ofertas");

            migrationBuilder.DropColumn(
                name: "IsConteudoDemonstracao",
                table: "Ofertas");
        }
    }
}
