using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaUsuarios.Migrations
{
    /// <inheritdoc />
    public partial class CriarSistemaPropostas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Telefone",
                table: "Usuarios",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(15)",
                oldMaxLength: 15);

            migrationBuilder.AlterColumn<string>(
                name: "CPF",
                table: "Usuarios",
                type: "nvarchar(11)",
                maxLength: 11,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(14)",
                oldMaxLength: 14);

            migrationBuilder.CreateTable(
                name: "Layouts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Nome = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Layouts", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Propostas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Titulo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataModificacao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DataInicio = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataFim = table.Column<DateTime>(type: "datetime2", nullable: true),
                    NumeroPassageiros = table.Column<int>(type: "int", nullable: false),
                    NumeroCriancas = table.Column<int>(type: "int", nullable: false),
                    FotoCapa = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    LayoutId = table.Column<int>(type: "int", nullable: true),
                    ObservacoesGerais = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    StatusProposta = table.Column<int>(type: "int", nullable: false),
                    LinkPublicoAtivo = table.Column<bool>(type: "bit", nullable: false),
                    DataExpiracaoLink = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Propostas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Propostas_Layouts_LayoutId",
                        column: x => x.LayoutId,
                        principalTable: "Layouts",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_Propostas_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Telefone",
                table: "Usuarios",
                column: "Telefone",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Propostas_LayoutId",
                table: "Propostas",
                column: "LayoutId");

            migrationBuilder.CreateIndex(
                name: "IX_Propostas_UsuarioId",
                table: "Propostas",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Propostas");

            migrationBuilder.DropTable(
                name: "Layouts");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_Telefone",
                table: "Usuarios");

            migrationBuilder.AlterColumn<string>(
                name: "Telefone",
                table: "Usuarios",
                type: "nvarchar(15)",
                maxLength: 15,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(11)",
                oldMaxLength: 11);

            migrationBuilder.AlterColumn<string>(
                name: "CPF",
                table: "Usuarios",
                type: "nvarchar(14)",
                maxLength: 14,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(11)",
                oldMaxLength: 11);
        }
    }
}
