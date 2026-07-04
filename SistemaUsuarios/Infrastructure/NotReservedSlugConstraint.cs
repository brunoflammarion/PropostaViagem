using System.Text.RegularExpressions;
using SistemaUsuarios.Services;

namespace SistemaUsuarios.Infrastructure
{
    /// <summary>
    /// Route constraint that matches only valid agency slugs — alphanumeric/hyphen strings
    /// that are NOT reserved system route prefixes.
    /// </summary>
    public class NotReservedSlugConstraint : IRouteConstraint
    {
        private static readonly Regex ValidSlugPattern = new(@"^[a-z0-9][a-z0-9\-]{0,98}[a-z0-9]$|^[a-z0-9]{1,2}$",
            RegexOptions.Compiled | RegexOptions.IgnoreCase);

        public bool Match(HttpContext? httpContext, IRouter? route, string routeKey,
            RouteValueDictionary values, RouteDirection routeDirection)
        {
            if (!values.TryGetValue(routeKey, out var rawValue) || rawValue == null)
                return false;

            var slug = rawValue.ToString()!;

            if (!ValidSlugPattern.IsMatch(slug))
                return false;

            return !SlugHelper.IsReserved(slug);
        }
    }
}
