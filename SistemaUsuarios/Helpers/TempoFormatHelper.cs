namespace SistemaUsuarios.Helpers
{
    public static class TempoFormatHelper
    {
        public static string FormatarDuracao(double segundos)
        {
            var s = (int)Math.Round(segundos);
            if (s < 60)
                return $"{s} segundo{(s != 1 ? "s" : "")}";
            if (s < 3600)
            {
                var m = s / 60;
                var r = s % 60;
                return r > 0 ? $"{m} min {r} s" : $"{m} min";
            }
            if (s < 86400)
            {
                var h = s / 3600;
                var m = (s % 3600) / 60;
                return m > 0 ? $"{h} h {m} min" : $"{h} hora{(h != 1 ? "s" : "")}";
            }
            var d = s / 86400;
            var hr = (s % 86400) / 3600;
            return hr > 0 ? $"{d} dia{(d != 1 ? "s" : "")} {hr} h" : $"{d} dia{(d != 1 ? "s" : "")}";
        }
    }
}
