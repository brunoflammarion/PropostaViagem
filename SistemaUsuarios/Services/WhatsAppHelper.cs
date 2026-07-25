using System.Text.RegularExpressions;

namespace SistemaUsuarios.Services
{
    public static class WhatsAppHelper
    {
        /// <summary>
        /// Remove formatação e garante código do país brasileiro (55).
        /// Retorna null se o número não tiver dígitos suficientes.
        /// </summary>
        public static string? NormalizarTelefone(string? telefone)
        {
            if (string.IsNullOrWhiteSpace(telefone)) return null;

            var digits = Regex.Replace(telefone, @"\D", "");

            if (string.IsNullOrEmpty(digits)) return null;

            // Já tem código do país 55: 12 dígitos (55 + DDD 2 + número 8) ou 13 (55 + DDD 2 + número 9)
            if (digits.StartsWith("55") && digits.Length is 12 or 13)
                return digits;

            // Número brasileiro sem código do país: DDD 2 + número 8 ou 9 = 10 ou 11 dígitos
            if (digits.Length is 10 or 11)
                return "55" + digits;

            // Comprimento inesperado — não gerar link inválido
            return null;
        }

        /// <summary>
        /// Monta a URL wa.me com número normalizado e mensagem codificada.
        /// Retorna null se o número for inválido.
        /// </summary>
        public static string? MontarUrl(string? telefone, string? mensagem = null)
        {
            var numero = NormalizarTelefone(telefone);
            if (numero == null) return null;

            var url = $"https://wa.me/{numero}";
            if (!string.IsNullOrWhiteSpace(mensagem))
                url += "?text=" + Uri.EscapeDataString(mensagem);

            return url;
        }
    }
}
