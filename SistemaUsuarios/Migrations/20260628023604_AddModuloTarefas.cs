using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaUsuarios.Migrations
{
    /// <inheritdoc />
    public partial class AddModuloTarefas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ConfiguracoesLembrete",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    TemplateCodigo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Habilitado = table.Column<bool>(type: "bit", nullable: false),
                    OffsetDias = table.Column<int>(type: "int", nullable: true),
                    MomentoReferencia = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ConfiguracoesLembrete", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConfiguracoesLembrete_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Tarefas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ClienteId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    PropostaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Titulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DataVencimento = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Tipo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Prioridade = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    Origem = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    GeradaAutomaticamente = table.Column<bool>(type: "bit", nullable: false),
                    TemplateCodigo = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    DataConclusao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataAtualizacao = table.Column<DateTime>(type: "datetime2", nullable: true),
                    IsDeleted = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tarefas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Tarefas_Clientes_ClienteId",
                        column: x => x.ClienteId,
                        principalTable: "Clientes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Tarefas_Propostas_PropostaId",
                        column: x => x.PropostaId,
                        principalTable: "Propostas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Tarefas_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ConfiguracoesLembrete_UsuarioId_TemplateCodigo",
                table: "ConfiguracoesLembrete",
                columns: new[] { "UsuarioId", "TemplateCodigo" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tarefas_ClienteId",
                table: "Tarefas",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Tarefas_PropostaId_TemplateCodigo_IsDeleted",
                table: "Tarefas",
                columns: new[] { "PropostaId", "TemplateCodigo", "IsDeleted" },
                filter: "[PropostaId] IS NOT NULL AND [TemplateCodigo] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Tarefas_UsuarioId",
                table: "Tarefas",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Tarefas_UsuarioId_DataVencimento_IsDeleted",
                table: "Tarefas",
                columns: new[] { "UsuarioId", "DataVencimento", "IsDeleted" });

            migrationBuilder.CreateIndex(
                name: "IX_Tarefas_UsuarioId_Status_IsDeleted",
                table: "Tarefas",
                columns: new[] { "UsuarioId", "Status", "IsDeleted" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfiguracoesLembrete");

            migrationBuilder.DropTable(
                name: "Tarefas");
        }
    }
}
