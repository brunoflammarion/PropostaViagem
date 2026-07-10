using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SistemaUsuarios.Data;
using SistemaUsuarios.Models;
using System.Text;

namespace SistemaUsuarios.Controllers
{
    public class CalendarioController : Controller
    {
        private readonly ApplicationDbContext _context;
        public CalendarioController(ApplicationDbContext context) => _context = context;

        [HttpGet("/calendario/feed/{token}")]
        public async Task<IActionResult> Feed(string token)
        {
            var usuario = await _context.Usuarios
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.CalendarioToken == token);

            if (usuario == null) return NotFound();

            var uid    = usuario.Id;
            var limite = DateTime.Today.AddDays(-60);

            var tarefas = await _context.Tarefas
                .Where(t => t.UsuarioId == uid && !t.IsDeleted
                    && t.Status != TarefaStatus.Cancelada
                    && t.DataVencimento >= limite)
                .AsNoTracking()
                .ToListAsync();

            var viagens = await _context.Propostas
                .Where(p => (p.UsuarioResponsavelId == uid || p.UsuarioMasterId == uid)
                    && p.StatusProposta == StatusProposta.Aprovada
                    && p.DataInicio.HasValue && p.DataFim.HasValue)
                .AsNoTracking()
                .ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("BEGIN:VCALENDAR");
            sb.AppendLine("VERSION:2.0");
            sb.AppendLine("PRODID:-//AgentTools//Agenda//PT");
            sb.AppendLine("CALSCALE:GREGORIAN");
            sb.AppendLine("METHOD:PUBLISH");
            sb.AppendLine("X-WR-CALNAME:AgentTools - Agenda");
            sb.AppendLine("X-WR-TIMEZONE:America/Sao_Paulo");
            sb.AppendLine("REFRESH-INTERVAL;VALUE=DURATION:PT1H");
            sb.AppendLine("X-PUBLISHED-TTL:PT1H");

            foreach (var t in tarefas)
            {
                var stamp   = (t.DataAtualizacao ?? t.DataCriacao).ToUniversalTime().ToString("yyyyMMddTHHmmssZ");
                var dtStart = t.DataVencimento.ToString("yyyyMMdd");
                var dtEnd   = t.DataVencimento.AddDays(1).ToString("yyyyMMdd");
                var prefix  = t.Status == TarefaStatus.Concluida ? "✓ " : "";

                sb.AppendLine("BEGIN:VEVENT");
                sb.AppendLine($"UID:tarefa-{t.Id}@agenttools");
                sb.AppendLine($"DTSTAMP:{stamp}");
                sb.AppendLine($"DTSTART;VALUE=DATE:{dtStart}");
                sb.AppendLine($"DTEND;VALUE=DATE:{dtEnd}");
                sb.AppendLine($"SUMMARY:{Esc(prefix + t.Titulo)}");
                if (!string.IsNullOrEmpty(t.Descricao))
                    sb.AppendLine($"DESCRIPTION:{Esc(t.Descricao)}");
                sb.AppendLine($"CATEGORIES:{t.Tipo ?? "Geral"}");
                sb.AppendLine("TRANSP:TRANSPARENT");
                sb.AppendLine("END:VEVENT");
            }

            foreach (var p in viagens)
            {
                var stamp   = p.DataCriacao.ToUniversalTime().ToString("yyyyMMddTHHmmssZ");
                var dtStart = p.DataInicio!.Value.ToString("yyyyMMdd");
                var dtEnd   = p.DataFim!.Value.AddDays(1).ToString("yyyyMMdd");

                sb.AppendLine("BEGIN:VEVENT");
                sb.AppendLine($"UID:viagem-{p.Id}@agenttools");
                sb.AppendLine($"DTSTAMP:{stamp}");
                sb.AppendLine($"DTSTART;VALUE=DATE:{dtStart}");
                sb.AppendLine($"DTEND;VALUE=DATE:{dtEnd}");
                sb.AppendLine($"SUMMARY:{Esc("✈ " + p.Titulo)}");
                sb.AppendLine("TRANSP:TRANSPARENT");
                sb.AppendLine("END:VEVENT");
            }

            sb.AppendLine("END:VCALENDAR");

            Response.Headers["Content-Disposition"] = "inline; filename=agenda.ics";
            return Content(sb.ToString(), "text/calendar", Encoding.UTF8);
        }

        private static string Esc(string s) =>
            s.Replace("\\", "\\\\")
             .Replace(";", "\\;")
             .Replace(",", "\\,")
             .Replace("\r\n", "\\n")
             .Replace("\n", "\\n")
             .Replace("\r", "");
    }
}
