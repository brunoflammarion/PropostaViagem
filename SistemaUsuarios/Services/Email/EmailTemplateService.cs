namespace SistemaUsuarios.Services.Email
{
    public class EmailTemplateService
    {
        private readonly string             _templatesPath;
        private readonly ApplicationSettings _appSettings;

        public EmailTemplateService(IWebHostEnvironment env, ApplicationSettings appSettings)
        {
            _templatesPath = Path.Combine(env.ContentRootPath, "EmailTemplates");
            _appSettings   = appSettings;
        }

        public string GetResetSenhaHtml(string nome, string resetUrl)
            => RenderFull("ResetSenha.html", new()
            {
                ["Nome"]     = nome,
                ["ResetUrl"] = resetUrl,
                ["BaseUrl"]  = _appSettings.BaseUrl,
            });

        public string GetVipBemVindoHtml(string nome, string email)
            => RenderFull("VipBemVindo.html", new()
            {
                ["Nome"]    = nome,
                ["Email"]   = email,
                ["BaseUrl"] = _appSettings.BaseUrl,
            });

        private string RenderFull(string templateName, Dictionary<string, string> vars)
        {
            var content = Render(Load(templateName), vars);
            var layout  = Load("_Layout.html");
            // Passa BaseUrl para o layout também
            layout = Render(layout, vars);
            return layout.Replace("{{CONTENT}}", content);
        }

        private string Load(string name)
            => File.ReadAllText(Path.Combine(_templatesPath, name));

        private static string Render(string html, Dictionary<string, string> vars)
        {
            foreach (var (k, v) in vars)
                html = html.Replace($"{{{{{k}}}}}", v ?? "");
            return html;
        }
    }
}
