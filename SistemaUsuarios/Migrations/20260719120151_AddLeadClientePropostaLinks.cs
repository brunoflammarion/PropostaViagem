using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaUsuarios.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadClientePropostaLinks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "ClienteId",
                table: "Leads",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "PropostaId",
                table: "Leads",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leads_ClienteId",
                table: "Leads",
                column: "ClienteId");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_PropostaId",
                table: "Leads",
                column: "PropostaId");

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Clientes_ClienteId",
                table: "Leads",
                column: "ClienteId",
                principalTable: "Clientes",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_Leads_Propostas_PropostaId",
                table: "Leads",
                column: "PropostaId",
                principalTable: "Propostas",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Clientes_ClienteId",
                table: "Leads");

            migrationBuilder.DropForeignKey(
                name: "FK_Leads_Propostas_PropostaId",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Leads_ClienteId",
                table: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Leads_PropostaId",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "ClienteId",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "PropostaId",
                table: "Leads");
        }
    }
}
