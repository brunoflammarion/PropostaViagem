using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaUsuarios.Migrations
{
    /// <inheritdoc />
    public partial class AddLeadSoftDeleteAndHistory : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "ExcluidoEm",
                table: "Leads",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "ExcluidoPorUsuarioId",
                table: "Leads",
                type: "uniqueidentifier",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsDeleted",
                table: "Leads",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "UpdatedAt",
                table: "Leads",
                type: "datetime2",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LeadHistoricos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LeadId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgenciaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    TipoAcao = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    CampoAlterado = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    ValorAnterior = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    ValorNovo = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Observacao = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    DataHora = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadHistoricos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadHistoricos_Leads_LeadId",
                        column: x => x.LeadId,
                        principalTable: "Leads",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LeadHistoricos_AgenciaId_DataHora",
                table: "LeadHistoricos",
                columns: new[] { "AgenciaId", "DataHora" });

            migrationBuilder.CreateIndex(
                name: "IX_LeadHistoricos_LeadId",
                table: "LeadHistoricos",
                column: "LeadId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeadHistoricos");

            migrationBuilder.DropColumn(
                name: "ExcluidoEm",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "ExcluidoPorUsuarioId",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "IsDeleted",
                table: "Leads");

            migrationBuilder.DropColumn(
                name: "UpdatedAt",
                table: "Leads");
        }
    }
}
