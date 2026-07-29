using System.Net;
using System.Net.Mail;

namespace SistemaUsuarios.Services.Email
{
    public class SmtpEmailService : IEmailService
    {
        private readonly EmailSettings         _settings;
        private readonly ILogger<SmtpEmailService> _logger;

        public SmtpEmailService(EmailSettings settings, ILogger<SmtpEmailService> logger)
        {
            _settings = settings;
            _logger   = logger;
        }

        public async Task SendAsync(string toEmail, string toName, string subject, string htmlBody)
        {
            var started = DateTime.UtcNow;
            try
            {
                using var client = new SmtpClient(_settings.Smtp.Host, _settings.Smtp.Port)
                {
                    EnableSsl      = _settings.Smtp.EnableSsl,
                    Credentials    = new NetworkCredential(_settings.Smtp.Username, _settings.Smtp.Password),
                    DeliveryMethod = SmtpDeliveryMethod.Network,
                };

                using var message = new MailMessage
                {
                    From       = new MailAddress(_settings.FromEmail, _settings.FromName),
                    Subject    = subject,
                    Body       = htmlBody,
                    IsBodyHtml = true,
                };
                message.To.Add(new MailAddress(toEmail, toName));

                await client.SendMailAsync(message);

                var ms = (int)(DateTime.UtcNow - started).TotalMilliseconds;
                _logger.LogInformation("Email enviado | para={Email} assunto={Subject} ms={Ms}", toEmail, subject, ms);
            }
            catch (Exception ex)
            {
                var ms = (int)(DateTime.UtcNow - started).TotalMilliseconds;
                _logger.LogError(ex, "Falha ao enviar email | para={Email} assunto={Subject} ms={Ms}", toEmail, subject, ms);
                throw;
            }
        }
    }
}
