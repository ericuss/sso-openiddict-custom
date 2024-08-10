using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using OpenIddict.Abstractions;
using OpenIddict.Server.AspNetCore;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Mvc;
using SsoCustom.Entities;
using SignInResult = Microsoft.AspNetCore.Identity.SignInResult;

namespace SsoCustom.Features.Users;

public static class Login
{
    public static async Task<IResult> Handler(
        HttpContext httpContext,
        UserManager<UserEntity> userManager,
        SignInManager<UserEntity> signInManager,
        IOpenIddictScopeManager scopeManager,
        IOpenIddictApplicationManager applicationManager)
    {
        var request = httpContext.GetOpenIddictServerRequest() ??
                      throw new InvalidOperationException("The OpenIddict server request cannot be retrieved.");

        if (!request.IsPasswordGrantType())
        {
            return Results.BadRequest(new OpenIddictResponse
            {
                Error = OpenIddictConstants.Errors.UnsupportedGrantType,
                ErrorDescription = "The specified grant type is not supported."
            });
        }

        var user = await userManager.FindByNameAsync(request.Username);
        if (user == null || !await userManager.CheckPasswordAsync(user, request.Password))
        {
            return Results.BadRequest(new OpenIddictResponse
            {
                Error = OpenIddictConstants.Errors.InvalidGrant,
                ErrorDescription = "The username/password combination is invalid."
            });
        }

        // Create the principal and set the claims.
        var principal = await signInManager.CreateUserPrincipalAsync(user);

        // Set the list of scopes granted to the client application.
        principal.SetScopes(new[]
        {
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Email,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.Roles
        }.Intersect(request.GetScopes()));
        
        // Asegurarte de que el principal tenga la claim "sub"
        if (!principal.HasClaim(c => c.Type == OpenIddictConstants.Claims.Subject))
        {
            var identity = (ClaimsIdentity)principal.Identity;
            identity.AddClaim(new Claim(OpenIddictConstants.Claims.Subject, user.Id.ToString()));
        }
        
        // Return the SignIn result with the generated token.
        return Results.SignIn(principal, properties: null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
    }
}
