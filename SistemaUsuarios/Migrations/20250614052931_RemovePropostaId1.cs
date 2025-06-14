using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaUsuarios.Migrations
{
    /// <inheritdoc />
    public partial class RemovePropostaId1 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "PropostaId1",
                table: "PropostaVisualizacoes",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_PropostaVisualizacoes_PropostaId1",
                table: "PropostaVisualizacoes",
                column: "PropostaId1");

            migrationBuilder.AddForeignKey(
                name: "FK_PropostaVisualizacoes_Propostas_PropostaId1",
                table: "PropostaVisualizacoes",
                column: "PropostaId1",
                principalTable: "Propostas",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PropostaVisualizacoes_Propostas_PropostaId1",
                table: "PropostaVisualizacoes");

            migrationBuilder.DropIndex(
                name: "IX_PropostaVisualizacoes_PropostaId1",
                table: "PropostaVisualizacoes");

            migrationBuilder.DropColumn(
                name: "PropostaId1",
                table: "PropostaVisualizacoes");
        }
    }
}
