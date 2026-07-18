using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace SistemaUsuarios.Migrations
{
    /// <inheritdoc />
    public partial class AddAiConsumptionControl : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AiAgencyLimits",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgenciaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    LimiteMensalCusto = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    Moeda = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    LimiteMensalChamadas = table.Column<int>(type: "int", nullable: true),
                    LimiteMensalTokens = table.Column<long>(type: "bigint", nullable: true),
                    ModoControle = table.Column<int>(type: "int", nullable: false),
                    PercentualAlerta = table.Column<int>(type: "int", nullable: false),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    PermitirExcedente = table.Column<bool>(type: "bit", nullable: false),
                    ValorExcedentePermitido = table.Column<decimal>(type: "decimal(18,4)", precision: 18, scale: 4, nullable: true),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoEm = table.Column<DateTime>(type: "datetime2", nullable: false),
                    AtualizadoPorAdminId = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiAgencyLimits", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiLimitAuditLogs",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgenciaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AdministradorId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Campo = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ValorAnterior = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ValorNovo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Motivo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DataHora = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiLimitAuditLogs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiModelPricings",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Provedor = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Modelo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    PrecoInputPorMilhao = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    PrecoCachedInputPorMilhao = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    PrecoOutputPorMilhao = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    Moeda = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    VigenciaInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    VigenciaFim = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Ativo = table.Column<bool>(type: "bit", nullable: false),
                    Versao = table.Column<int>(type: "int", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiModelPricings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AiUsageRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgenciaId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UsuarioId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PropostaId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EntidadeId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    EntidadeTipo = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Funcionalidade = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    Provedor = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Modelo = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    CorrelationId = table.Column<string>(type: "nvarchar(450)", nullable: false),
                    ProviderRequestId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    InputTokens = table.Column<int>(type: "int", nullable: false),
                    CachedInputTokens = table.Column<int>(type: "int", nullable: false),
                    OutputTokens = table.Column<int>(type: "int", nullable: false),
                    ReasoningTokens = table.Column<int>(type: "int", nullable: false),
                    TotalTokens = table.Column<int>(type: "int", nullable: false),
                    PrecoInputPorMilhao = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    PrecoCachedInputPorMilhao = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    PrecoOutputPorMilhao = table.Column<decimal>(type: "decimal(18,6)", precision: 18, scale: 6, nullable: false),
                    CustoInput = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    CustoCachedInput = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    CustoOutput = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    CustoTotal = table.Column<decimal>(type: "decimal(18,8)", precision: 18, scale: 8, nullable: false),
                    Moeda = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Sucesso = table.Column<bool>(type: "bit", nullable: false),
                    Status = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    DuracaoMs = table.Column<int>(type: "int", nullable: false),
                    HttpStatusExterno = table.Column<int>(type: "int", nullable: true),
                    TipoErro = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    MensagemErroSanitizada = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    DataHoraInicio = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DataHoraFim = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CriadoEm = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AiUsageRecords", x => x.Id);
                });

            migrationBuilder.InsertData(
                table: "AiModelPricings",
                columns: new[] { "Id", "Ativo", "CriadoEm", "Modelo", "Moeda", "PrecoCachedInputPorMilhao", "PrecoInputPorMilhao", "PrecoOutputPorMilhao", "Provedor", "Versao", "VigenciaFim", "VigenciaInicio" },
                values: new object[,]
                {
                    { new Guid("11111111-0000-0000-0000-000000000001"), true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "gpt-4o-mini", "USD", 0.075m, 0.15m, 0.60m, "OpenAI", 1, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) },
                    { new Guid("11111111-0000-0000-0000-000000000002"), true, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc), "gpt-4o", "USD", 1.25m, 2.50m, 10.00m, "OpenAI", 1, null, new DateTime(2026, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc) }
                });

            migrationBuilder.CreateIndex(
                name: "IX_AiAgencyLimits_AgenciaId",
                table: "AiAgencyLimits",
                column: "AgenciaId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AiModelPricings_Provedor_Modelo_Ativo",
                table: "AiModelPricings",
                columns: new[] { "Provedor", "Modelo", "Ativo" });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageRecords_AgenciaId_DataHoraInicio",
                table: "AiUsageRecords",
                columns: new[] { "AgenciaId", "DataHoraInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageRecords_AgenciaId_Funcionalidade_DataHoraInicio",
                table: "AiUsageRecords",
                columns: new[] { "AgenciaId", "Funcionalidade", "DataHoraInicio" });

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageRecords_CorrelationId",
                table: "AiUsageRecords",
                column: "CorrelationId");

            migrationBuilder.CreateIndex(
                name: "IX_AiUsageRecords_Modelo_DataHoraInicio",
                table: "AiUsageRecords",
                columns: new[] { "Modelo", "DataHoraInicio" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AiAgencyLimits");

            migrationBuilder.DropTable(
                name: "AiLimitAuditLogs");

            migrationBuilder.DropTable(
                name: "AiModelPricings");

            migrationBuilder.DropTable(
                name: "AiUsageRecords");
        }
    }
}
