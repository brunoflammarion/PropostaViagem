using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaUsuarios.Migrations
{
    /// <inheritdoc />
    public partial class AddAvaliacaoCliente : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "SolicitarAvaliacaoAcomodacao",
                table: "Propostas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SolicitarAvaliacaoExperiencia",
                table: "Propostas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "SolicitarAvaliacaoHospedagem",
                table: "Propostas",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "AvaliacoesCliente",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropostaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoItem = table.Column<int>(type: "int", nullable: false),
                    ItemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nota = table.Column<int>(type: "int", nullable: false),
                    Comentario = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Favorito = table.Column<bool>(type: "bit", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AvaliacoesCliente", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AvaliacoesCliente_Propostas_PropostaId",
                        column: x => x.PropostaId,
                        principalTable: "Propostas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AvaliacoesCliente_PropostaId_TipoItem_ItemId",
                table: "AvaliacoesCliente",
                columns: new[] { "PropostaId", "TipoItem", "ItemId" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AvaliacoesCliente");

            migrationBuilder.DropColumn(
                name: "SolicitarAvaliacaoAcomodacao",
                table: "Propostas");

            migrationBuilder.DropColumn(
                name: "SolicitarAvaliacaoExperiencia",
                table: "Propostas");

            migrationBuilder.DropColumn(
                name: "SolicitarAvaliacaoHospedagem",
                table: "Propostas");
        }
    }
}
