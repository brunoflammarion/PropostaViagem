using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaUsuarios.Migrations
{
    /// <inheritdoc />
    public partial class AddImportacaoSessao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ImportacaoSessoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropostaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    DraftJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    SourceFiles = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "int", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ExpiradoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImportacaoSessoes", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ImportacaoSessoes");
        }
    }
}
