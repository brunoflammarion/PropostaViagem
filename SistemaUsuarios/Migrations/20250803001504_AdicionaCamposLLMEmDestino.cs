using Microsoft.EntityFrameworkCore.Migrations;
using NetTopologySuite.Geometries;

#nullable disable

namespace SistemaUsuarios.Migrations
{
    /// <inheritdoc />
    public partial class AdicionaCamposLLMEmDestino : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ObservacoesGerais",
                table: "Propostas",
                type: "nvarchar(max)",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.AlterColumn<string>(
                name: "FotoCapa",
                table: "Propostas",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000);

            migrationBuilder.AddColumn<string>(
                name: "AtracoesLLM",
                table: "Destinos",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "CuidadosLLM",
                table: "Destinos",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "DescricaoLLM",
                table: "Destinos",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "GastronomiaLLM",
                table: "Destinos",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "InformacoesPraticasLLM",
                table: "Destinos",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<double>(
                name: "Latitude",
                table: "Destinos",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<Point>(
                name: "Localizacao",
                table: "Destinos",
                type: "geography",
                nullable: true);

            migrationBuilder.AddColumn<double>(
                name: "Longitude",
                table: "Destinos",
                type: "float",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MalaViagemLLM",
                table: "Destinos",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AtracoesLLM",
                table: "Destinos");

            migrationBuilder.DropColumn(
                name: "CuidadosLLM",
                table: "Destinos");

            migrationBuilder.DropColumn(
                name: "DescricaoLLM",
                table: "Destinos");

            migrationBuilder.DropColumn(
                name: "GastronomiaLLM",
                table: "Destinos");

            migrationBuilder.DropColumn(
                name: "InformacoesPraticasLLM",
                table: "Destinos");

            migrationBuilder.DropColumn(
                name: "Latitude",
                table: "Destinos");

            migrationBuilder.DropColumn(
                name: "Localizacao",
                table: "Destinos");

            migrationBuilder.DropColumn(
                name: "Longitude",
                table: "Destinos");

            migrationBuilder.DropColumn(
                name: "MalaViagemLLM",
                table: "Destinos");

            migrationBuilder.AlterColumn<string>(
                name: "ObservacoesGerais",
                table: "Propostas",
                type: "nvarchar(max)",
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(max)",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "FotoCapa",
                table: "Propostas",
                type: "nvarchar(1000)",
                maxLength: 1000,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "nvarchar(1000)",
                oldMaxLength: 1000,
                oldNullable: true);
        }
    }
}
