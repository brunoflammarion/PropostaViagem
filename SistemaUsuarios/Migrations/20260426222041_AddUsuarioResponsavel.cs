using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaUsuarios.Migrations
{
    /// <inheritdoc />
    public partial class AddUsuarioResponsavel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "UsuarioResponsavelId",
                table: "Propostas",
                type: "uniqueidentifier",
                nullable: true);

            // Data migration: propostas existentes herdam o criador como responsável inicial
            migrationBuilder.Sql(
                "UPDATE Propostas SET UsuarioResponsavelId = UsuarioId WHERE UsuarioResponsavelId IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Propostas_UsuarioResponsavelId",
                table: "Propostas",
                column: "UsuarioResponsavelId");

            migrationBuilder.AddForeignKey(
                name: "FK_Propostas_Usuarios_UsuarioResponsavelId",
                table: "Propostas",
                column: "UsuarioResponsavelId",
                principalTable: "Usuarios",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Propostas_Usuarios_UsuarioResponsavelId",
                table: "Propostas");

            migrationBuilder.DropIndex(
                name: "IX_Propostas_UsuarioResponsavelId",
                table: "Propostas");

            migrationBuilder.DropColumn(
                name: "UsuarioResponsavelId",
                table: "Propostas");
        }
    }
}
