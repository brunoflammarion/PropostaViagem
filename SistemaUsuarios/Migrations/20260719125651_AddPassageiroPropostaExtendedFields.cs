using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaUsuarios.Migrations
{
    /// <inheritdoc />
    public partial class AddPassageiroPropostaExtendedFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClienteId",
                table: "PassageirosProposta",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "FaixaEtaria",
                table: "PassageirosProposta",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "FaixaIsAproximada",
                table: "PassageirosProposta",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ModoBebe",
                table: "PassageirosProposta",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ResponsavelId",
                table: "PassageirosProposta",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PassageirosProposta_ClienteId",
                table: "PassageirosProposta",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_PassageirosProposta_ResponsavelId",
                table: "PassageirosProposta",
                column: "ResponsavelId");

            migrationBuilder.AddForeignKey(
                name: "FK_PassageirosProposta_Clientes_ClienteId",
                table: "PassageirosProposta",
                column: "ClienteId",
                principalTable: "Clientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PassageirosProposta_PassageirosProposta_ResponsavelId",
                table: "PassageirosProposta",
                column: "ResponsavelId",
                principalTable: "PassageirosProposta",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PassageirosProposta_Clientes_ClienteId",
                table: "PassageirosProposta");

            migrationBuilder.DropForeignKey(
                name: "FK_PassageirosProposta_PassageirosProposta_ResponsavelId",
                table: "PassageirosProposta");

            migrationBuilder.DropIndex(
                name: "IX_PassageirosProposta_ClienteId",
                table: "PassageirosProposta");

            migrationBuilder.DropIndex(
                name: "IX_PassageirosProposta_ResponsavelId",
                table: "PassageirosProposta");

            migrationBuilder.DropColumn(
                name: "ClienteId",
                table: "PassageirosProposta");

            migrationBuilder.DropColumn(
                name: "FaixaEtaria",
                table: "PassageirosProposta");

            migrationBuilder.DropColumn(
                name: "FaixaIsAproximada",
                table: "PassageirosProposta");

            migrationBuilder.DropColumn(
                name: "ModoBebe",
                table: "PassageirosProposta");

            migrationBuilder.DropColumn(
                name: "ResponsavelId",
                table: "PassageirosProposta");
        }
    }
}
