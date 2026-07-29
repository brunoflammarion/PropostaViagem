namespace SistemaUsuarios.Services.Email
{
    public class EmailSettings
    {
        public string      FromName { get; set; } = "Agent Tools";
        public string      FromEmail { get; set; } = "contato@agenttools.com.br";
        public SmtpSettings Smtp    { get; set; } = new();
    }

    public class SmtpSettings
    {
        public string Host      { get; set; } = "smtp.hostinger.com";
        public int    Port      { get; set; } = 587;
        public string Username  { get; set; } = "";
        public string Password  { get; set; } = "";
        public bool   EnableSsl { get; set; } = true;
    }
}
