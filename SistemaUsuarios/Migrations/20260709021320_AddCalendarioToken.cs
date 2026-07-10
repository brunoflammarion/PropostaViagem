using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaUsuarios.Migrations
{
    /// <inheritdoc />
    public partial class AddCalendarioToken : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CalendarioToken",
                table: "Usuarios",
                type: "nvarchar(64)",
                maxLength: 64,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CalendarioToken",
                table: "Usuarios");
        }
    }
}
