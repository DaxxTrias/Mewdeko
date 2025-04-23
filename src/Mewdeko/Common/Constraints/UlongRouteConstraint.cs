using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;

namespace Mewdeko.Common.Constraints;

/// <summary>
/// Route constraint for ulong parameters
/// </summary>
public class UlongRouteConstraint : IRouteConstraint
{
    /// <summary>
    /// Determines whether the route parameter satisfies the constraint
    /// </summary>
    /// <param name="httpContext">The HTTP context</param>
    /// <param name="route">The route the constraint is associated with</param>
    /// <param name="routeKey">The name of the parameter being checked</param>
    /// <param name="values">The route values collection</param>
    /// <param name="routeDirection">The route direction</param>
    /// <returns>True if the parameter satisfies the constraint, otherwise false</returns>
    public bool Match(HttpContext? httpContext, IRouter? route, string routeKey,
        RouteValueDictionary values, RouteDirection routeDirection)
    {
        if (!values.TryGetValue(routeKey, out var value))
            return false;

        var valueString = value?.ToString();
        return !string.IsNullOrEmpty(valueString) && ulong.TryParse(valueString, out _);
    }
}