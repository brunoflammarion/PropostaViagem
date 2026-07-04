using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaUsuarios.Migrations
{
    /// <inheritdoc />
    public partial class AddAgencySlugAndLeads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "NomeAgencia",
                table: "Usuarios",
                type: "nvarchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SlugAgencia",
                table: "Usuarios",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "LeadCaptureSettings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false),
                    WelcomeMessage = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ResponseTimeText = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: true),
                    ShowEmail = table.Column<bool>(type: "bit", nullable: false),
                    ShowOriginCity = table.Column<bool>(type: "bit", nullable: false),
                    ShowTravelDates = table.Column<bool>(type: "bit", nullable: false),
                    ShowAdults = table.Column<bool>(type: "bit", nullable: false),
                    ShowChildren = table.Column<bool>(type: "bit", nullable: false),
                    ShowBudget = table.Column<bool>(type: "bit", nullable: false),
                    ShowTripType = table.Column<bool>(type: "bit", nullable: false),
                    ShowAccommodationPreference = table.Column<bool>(type: "bit", nullable: false),
                    ShowNotes = table.Column<bool>(type: "bit", nullable: false),
                    ShowBestContactTime = table.Column<bool>(type: "bit", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeadCaptureSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeadCaptureSettings_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Leads",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    FullName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    WhatsApp = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Destination = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: false),
                    Email = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    OriginCity = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    TravelDates = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Adults = table.Column<int>(type: "int", nullable: true),
                    Children = table.Column<int>(type: "int", nullable: true),
                    Budget = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TripType = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AccommodationPreference = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: true),
                    Notes = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    BestContactTime = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Source = table.Column<string>(type: "nvarchar(80)", maxLength: 80, nullable: false),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Leads", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Leads_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Usuarios_SlugAgencia",
                table: "Usuarios",
                column: "SlugAgencia",
                unique: true,
                filter: "[SlugAgencia] IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_LeadCaptureSettings_UsuarioId",
                table: "LeadCaptureSettings",
                column: "UsuarioId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Leads_CreatedAt",
                table: "Leads",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_Leads_UsuarioId",
                table: "Leads",
                column: "UsuarioId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LeadCaptureSettings");

            migrationBuilder.DropTable(
                name: "Leads");

            migrationBuilder.DropIndex(
                name: "IX_Usuarios_SlugAgencia",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "NomeAgencia",
                table: "Usuarios");

            migrationBuilder.DropColumn(
                name: "SlugAgencia",
                table: "Usuarios");
        }
    }
}
