using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaUsuarios.Migrations
{
    /// <inheritdoc />
    public partial class AddHospedagemNumeroEstrelas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NumeroEstrelas",
                table: "Hospedagens",
                type: "int",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NumeroEstrelas",
                table: "Hospedagens");
        }
    }
}
