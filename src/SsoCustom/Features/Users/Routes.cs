using Microsoft.AspNetCore;

namespace SsoCustom.Features.Users;

public static class Routes
{
    public static IEndpointRouteBuilder RegisterUserRoutes( this IEndpointRouteBuilder endpoints)
    {
        endpoints.MapPost("/connect/token", Login.Handler);
        endpoints.MapPost("/users", CreateUser.Handler);

        return endpoints;
    }
}