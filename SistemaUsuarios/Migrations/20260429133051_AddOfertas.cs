using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaUsuarios.Migrations
{
    /// <inheritdoc />
    public partial class AddOfertas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Ofertas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioMasterId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    Nome = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    TemplateId = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    Cor1 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Cor2 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    Cor3 = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: true),
                    ImagemPrincipalPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    LogoPath = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    SlotsJson = table.Column<string>(type: "nvarchar(2000)", maxLength: 2000, nullable: true),
                    TituloPrincipal = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Subtitulo = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DescricaoCurta = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    TextoComplementar = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    Rodape = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    Cta = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Preco = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    PrecoAnterior = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TextoAPartirDe = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    CondicaoEspecial = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Parcelamento = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TextoUrgencia = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    ValidadeOferta = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Destino = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Origem = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PeriodoViagem = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    QtdNoites = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    CompanhiaAerea = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Hotel = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    RegimeHospedagem = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    InclusoesOferta = table.Column<string>(type: "nvarchar(1000)", maxLength: 1000, nullable: true),
                    ObservacoesCurtas = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    RegrasCondicoes = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: true),
                    WhatsApp = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Telefone = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: true),
                    Email = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Instagram = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Site = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    NomeAgencia = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    SeloPromocional = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TagPromocional = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TextoInstitucional = table.Column<string>(type: "nvarchar(300)", maxLength: 300, nullable: true),
                    DataCriacao = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataModificacao = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Ofertas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Ofertas_Usuarios_UsuarioId",
                        column: x => x.UsuarioId,
                        principalTable: "Usuarios",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Ofertas_Usuarios_UsuarioMasterId",
                        column: x => x.UsuarioMasterId,
                        principalTable: "Usuarios",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateIndex(
                name: "IX_Ofertas_UsuarioId",
                table: "Ofertas",
                column: "UsuarioId");

            migrationBuilder.CreateIndex(
                name: "IX_Ofertas_UsuarioMasterId",
                table: "Ofertas",
                column: "UsuarioMasterId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Ofertas");
        }
    }
}
