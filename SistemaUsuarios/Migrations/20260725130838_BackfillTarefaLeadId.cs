using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace SistemaUsuarios.Migrations
{
    /// <inheritdoc />
    public partial class BackfillTarefaLeadId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE t
                SET t.LeadId = l.Id
                FROM Tarefas t
                INNER JOIN Leads l
                    ON l.UsuarioId = t.UsuarioId
                    AND l.IsDeleted = 0
                    AND l.FullName  = SUBSTRING(t.Titulo, 12, 200)
                WHERE t.LeadId          IS NULL
                  AND t.TemplateCodigo  = 'NOVO_LEAD'
                  AND t.IsDeleted       = 0
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                UPDATE Tarefas
                SET LeadId = NULL
                WHERE TemplateCodigo = 'NOVO_LEAD'
            ");
        }
    }
}
