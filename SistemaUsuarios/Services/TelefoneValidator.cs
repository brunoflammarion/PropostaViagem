using System.Text.RegularExpressions;

namespace SistemaUsuarios.Services
{
    public static class TelefoneValidator
    {
        private static readonly HashSet<int> DddsValidos = new()
        {
            11, 12, 13, 14, 15, 16, 17, 18, 19,
            21, 22, 24, 27, 28,
            31, 32, 33, 34, 35, 37, 38,
            41, 42, 43, 44, 45, 46, 47, 48, 49,
            51, 53, 54, 55,
            61, 62, 63, 64, 65, 66, 67, 68, 69,
            71, 73, 74, 75, 77, 79,
            81, 82, 83, 84, 85, 86, 87, 88, 89,
            91, 92, 93, 94, 95, 96, 97, 98, 99
        };

        /// <summary>Remove formatação e código do país +55 se presente.</summary>
        public static string Normalizar(string? telefone)
        {
            if (string.IsNullOrWhiteSpace(telefone)) return "";
            var digits = Regex.Replace(telefone, @"[^\d]", "");
            if ((digits.Length == 12 || digits.Length == 13) && digits.StartsWith("55"))
                digits = digits[2..];
            return digits;
        }

        /// <summary>
        /// Valida número de celular brasileiro.
        /// Aceita entrada formatada ou crua; normaliza internamente.
        /// Rejeita: comprimento incorreto, DDD inválido, não-celular,
        ///          todos dígitos iguais e sequências ascendentes/descendentes.
        /// </summary>
        public static bool EhValido(string? telefone)
        {
            var d = Normalizar(telefone);

            // Deve ter exatamente 10 ou 11 dígitos
            if (d.Length != 10 && d.Length != 11) return false;

            // DDD válido
            if (!int.TryParse(d[..2], out var ddd)) return false;
            if (!DddsValidos.Contains(ddd)) return false;

            // Celular com 11 dígitos: nono dígito (após DDD) deve ser 9
            if (d.Length == 11 && d[2] != '9') return false;

            // Todos dígitos iguais: 00000000000, 11111111111, 99999999999 …
            if (d.Distinct().Count() == 1) return false;

            // Sequência ascendente ou descendente módulo 10
            // Ex. ascendente: 12345678901, 01234567890
            // Ex. descendente: 98765432109, 09876543210
            bool asc = true, desc = true;
            for (int i = 1; i < d.Length; i++)
            {
                int cur  = d[i]     - '0';
                int prev = d[i - 1] - '0';
                if ((cur  - prev + 10) % 10 != 1) asc  = false;
                if ((prev - cur  + 10) % 10 != 1) desc = false;
            }
            if (asc || desc) return false;

            return true;
        }
    }
}
