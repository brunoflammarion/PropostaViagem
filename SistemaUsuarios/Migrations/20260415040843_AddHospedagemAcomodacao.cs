using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaUsuarios.Migrations
{
    /// <inheritdoc />
    public partial class AddHospedagemAcomodacao : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Hospedagens",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DestinoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Endereco = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Latitude = table.Column<double>(type: "float", nullable: true),
                    Longitude = table.Column<double>(type: "float", nullable: true),
                    CheckIn = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CheckOut = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Categoria = table.Column<int>(type: "int", nullable: false),
                    TipoPensao = table.Column<int>(type: "int", nullable: false),
                    Reserva = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Observacoes = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Hospedagens", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Hospedagens_Destinos_DestinoId",
                        column: x => x.DestinoId,
                        principalTable: "Destinos",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Acomodacoes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    HospedagemId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(4000)", maxLength: 4000, nullable: true),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Acomodacoes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Acomodacoes_Hospedagens_HospedagemId",
                        column: x => x.HospedagemId,
                        principalTable: "Hospedagens",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AcomodacaoFotos",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AcomodacaoId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    CaminhoFoto = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    Descricao = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Ordem = table.Column<int>(type: "int", nullable: false),
                    Principal = table.Column<bool>(type: "bit", nullable: false),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AcomodacaoFotos", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AcomodacaoFotos_Acomodacoes_AcomodacaoId",
                        column: x => x.AcomodacaoId,
                        principalTable: "Acomodacoes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AcomodacaoFotos_AcomodacaoId",
                table: "AcomodacaoFotos",
                column: "AcomodacaoId");

            migrationBuilder.CreateIndex(
                name: "IX_AcomodacaoFotos_AcomodacaoId_Ordem",
                table: "AcomodacaoFotos",
                columns: new[] { "AcomodacaoId", "Ordem" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Acomodacoes_HospedagemId",
                table: "Acomodacoes",
                column: "HospedagemId");

            migrationBuilder.CreateIndex(
                name: "IX_Hospedagens_DestinoId",
                table: "Hospedagens",
                column: "DestinoId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AcomodacaoFotos");

            migrationBuilder.DropTable(
                name: "Acomodacoes");

            migrationBuilder.DropTable(
                name: "Hospedagens");
        }
    }
}
