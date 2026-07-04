using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaUsuarios.Migrations
{
    /// <inheritdoc />
    public partial class AddVooBagagem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "BagagemDespachadaMedidas",
                table: "Voos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BagagemDespachadaPeso",
                table: "Voos",
                type: "decimal(18,2)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BagagemItemPessoalDescricao",
                table: "Voos",
                type: "nvarchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BagagemItemPessoalMedidas",
                table: "Voos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "BagagemMaoMedidas",
                table: "Voos",
                type: "nvarchar(100)",
                maxLength: 100,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "BagagemMaoPeso",
                table: "Voos",
                type: "decimal(18,2)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "BagagemDespachadaMedidas",
                table: "Voos");

            migrationBuilder.DropColumn(
                name: "BagagemDespachadaPeso",
                table: "Voos");

            migrationBuilder.DropColumn(
                name: "BagagemItemPessoalDescricao",
                table: "Voos");

            migrationBuilder.DropColumn(
                name: "BagagemItemPessoalMedidas",
                table: "Voos");

            migrationBuilder.DropColumn(
                name: "BagagemMaoMedidas",
                table: "Voos");

            migrationBuilder.DropColumn(
                name: "BagagemMaoPeso",
                table: "Voos");
        }
    }
}
