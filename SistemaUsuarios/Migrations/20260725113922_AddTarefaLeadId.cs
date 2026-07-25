using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaUsuarios.Migrations
{
    /// <inheritdoc />
    public partial class AddTarefaLeadId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "LeadId",
                table: "Tarefas",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tarefas_LeadId",
                table: "Tarefas",
                column: "LeadId");

            migrationBuilder.AddForeignKey(
                name: "FK_Tarefas_Leads_LeadId",
                table: "Tarefas",
                column: "LeadId",
                principalTable: "Leads",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Tarefas_Leads_LeadId",
                table: "Tarefas");

            migrationBuilder.DropIndex(
                name: "IX_Tarefas_LeadId",
                table: "Tarefas");

            migrationBuilder.DropColumn(
                name: "LeadId",
                table: "Tarefas");
        }
    }
}
