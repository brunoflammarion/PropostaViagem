using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SistemaUsuarios.Services
{
    public static class SlugHelper
    {
        // Segmentos reservados — correspondem a controllers existentes e prefixos de rota do sistema
        public static readonly HashSet<string> ReservedSlugs = new(StringComparer.OrdinalIgnoreCase)
        {
            "auth", "home", "landing", "proposta", "propostas",
            "usuario", "usuarios", "cliente", "clientes",
            "destino", "destinos", "destinofoto", "destinofotos",
            "hospedagem", "hospedagens", "acomodacao", "acomodacoes",
            "experiencia", "experiencias", "seguro", "seguros",
            "transporte", "transportes", "voo", "voos",
            "passageiroproposta", "clienteproposta",
            "avaliacao", "avaliacoes", "aicopilot",
            "oferta", "ofertas", "propostaanalyticsdata",
            "lead", "leads", "captacao",
            "api", "login", "logout", "sair",
            "cadastro-agentes", "cadastro", "admin", "dashboard",
            "css", "js", "lib", "assets", "images", "img",
            "uploads", "bundles", "fonts", "favicon",
            "privacy", "error", "agencia", "agencias",
            "platformadmin", "platform-admin", "admin-plataforma",
            "produto",
        };

        public static bool IsReserved(string slug) => ReservedSlugs.Contains(slug);

        public static string Generate(string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return "";

            // Remove diacritics
            var normalized = name.Normalize(NormalizationForm.FormD);
            var sb = new StringBuilder(normalized.Length);
            foreach (var c in normalized)
            {
                if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                    sb.Append(c);
            }

            var result = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();

            // Ampersand → "e"
            result = result.Replace("&", " e ");

            // Keep only alphanumeric, spaces and hyphens
            result = Regex.Replace(result, @"[^a-z0-9\s\-]", "");

            // Spaces and consecutive whitespace → single hyphen
            result = Regex.Replace(result, @"\s+", "-");

            // Collapse consecutive hyphens
            result = Regex.Replace(result, @"-{2,}", "-");

            // Trim hyphens from edges
            result = result.Trim('-');

            return result;
        }

        /// <summary>
        /// Generates a unique slug for the given name, ensuring it does not conflict with
        /// reserved slugs or existing slugs in the provided used-slugs set.
        /// If a conflict exists, appends -2, -3, etc.
        /// </summary>
        public static string GenerateUnique(string name, IEnumerable<string> existingSlugs, Guid? excludeId = null, string? excludeSlug = null)
        {
            var base_ = Generate(name);
            if (string.IsNullOrEmpty(base_)) base_ = "agencia";

            var candidate = base_;
            if (IsReserved(candidate))
                candidate = candidate + "-viagens";

            var used = new HashSet<string>(existingSlugs, StringComparer.OrdinalIgnoreCase);
            if (excludeSlug != null) used.Remove(excludeSlug);

            if (!used.Contains(candidate))
                return candidate;

            for (int i = 2; i < 1000; i++)
            {
                var numbered = $"{candidate}-{i}";
                if (!used.Contains(numbered))
                    return numbered;
            }

            return $"{base_}-{Guid.NewGuid():N8}";
        }
    }
}
