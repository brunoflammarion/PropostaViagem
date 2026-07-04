using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaUsuarios.Migrations
{
    /// <inheritdoc />
    public partial class AddUsuarioHierarquia : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TipoUsuario",
                table: "Usuarios",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.AddColumn<Guid>(
                name: "UsuarioMasterId",
                table: "Usuarios",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "UsuarioMasterId",
                table: "Propostas",
                type: "uniqueidentifier",
                nullable: true);

            // Data migration: propostas existentes herdam o criador como Master
            migrationBuilder.Sql(
                "UPDATE Propostas SET UsuarioMasterId = UsuarioId WHERE UsuarioMasterId IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_UsuarioMasterId",
                table: "Usuarios",
                column: "UsuarioMasterId");

            migrationBuilder.CreateIndex(
                name: "IX_Propostas_UsuarioMasterId",
                table: "Propostas",
                column: "UsuarioMasterId");

            migrationBuilder.AddForeignKey(
                name: "FK_Propostas_Usuarios_UsuarioMasterId",
                table: "Propostas",
                column: "UsuarioMasterId",
                principalTable: "Usuarios",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_Usuarios_Usuarios_UsuarioMasterId",
                table: "Usuarios",
                column: "UsuarioMasterId",
                principalTable: "Usuarios",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Propostas_Usuarios_UsuarioMasterId",
                table: "Propostas");

            migrationBuilder.DropForeignKey(
                name: "FK_Usuarios_Usuarios_UsuarioMasterId",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_UsuarioMasterId",
                table: "Usuarios");

            migrationBuilder.DropIndex(
                name: "IX_Propostas_UsuarioMasterId",
                table: "Propostas");

            migrationBuilder.DropColumn(
                name: "TipoUsuario",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "UsuarioMasterId",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "UsuarioMasterId",
                table: "Propostas");
        }
    }
}
