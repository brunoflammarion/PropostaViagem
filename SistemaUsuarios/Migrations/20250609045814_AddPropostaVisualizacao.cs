using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaUsuarios.Migrations
{
    /// <inheritdoc />
    public partial class AddPropostaVisualizacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Propostas_Usuarios_UsuarioId",
                table: "Propostas");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_CPF",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_Telefone",
                table: "Usuarios");

            migrationBuilder.CreateTable(
                name: "PropostaVisualizacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropostaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionToken = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DataHoraInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataHoraFim = table.Column<DateTime>(type: "datetime2", nullable: true),
                    TempoVisualizacaoSegundos = table.Column<int>(type: "int", nullable: true),
                    TipoDispositivo = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Navegador = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    SistemaOperacional = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    ResolucaoTela = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    IdiomaNavegador = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    UrlReferenciador = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    TipoReferenciador = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    EnderecoIP = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Pais = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Estado = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Cidade = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Latitude = table.Column<decimal>(type: "decimal(10,8)", precision: 10, scale: 8, nullable: true),
                    Longitude = table.Column<decimal>(type: "decimal(11,8)", precision: 11, scale: 8, nullable: true),
                    ScrollMaximoPercentual = table.Column<int>(type: "int", nullable: true),
                    ClicouWhatsApp = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    ClicouEmail = table.Column<bool>(type: "bit", nullable: false, defaultValue: false),
                    NumeroCliques = table.Column<int>(type: "int", nullable: false, defaultValue: 0),
                    DeviceFingerprint = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    UserAgent = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: false),
                    DadosAdicionais = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false, defaultValueSql: "GETDATE()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PropostaVisualizacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PropostaVisualizacoes_Propostas_PropostaId",
                        column: x => x.PropostaId,
                        principalTable: "Propostas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PropostaVisualizacoes_DataHoraInicio",
                table: "PropostaVisualizacoes",
                column: "DataHoraInicio");

            migrationBuilder.CreateIndex(
                name: "IX_PropostaVisualizacoes_DeviceFingerprint",
                table: "PropostaVisualizacoes",
                column: "DeviceFingerprint");

            migrationBuilder.CreateIndex(
                name: "IX_PropostaVisualizacoes_PropostaId",
                table: "PropostaVisualizacoes",
                column: "PropostaId");

            migrationBuilder.CreateIndex(
                name: "IX_PropostaVisualizacoes_SessionToken",
                table: "PropostaVisualizacoes",
                column: "SessionToken");

            migrationBuilder.AddForeignKey(
                name: "FK_Propostas_Usuarios_UsuarioId",
                table: "Propostas",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Propostas_Usuarios_UsuarioId",
                table: "Propostas");

            migrationBuilder.DropTable(
                name: "PropostaVisualizacoes");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_CPF",
                table: "Usuarios",
                column: "CPF",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_Telefone",
                table: "Usuarios",
                column: "Telefone",
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Propostas_Usuarios_UsuarioId",
                table: "Propostas",
                column: "UsuarioId",
                principalTable: "Usuarios",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
