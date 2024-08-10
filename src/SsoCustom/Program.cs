using AspNetCore.Identity.MongoDbCore;
using AspNetCore.Identity.MongoDbCore.Models;
using Microsoft.AspNetCore;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using MongoDB.Driver;
using OpenIddict.Abstractions;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using OpenIddict.Server.AspNetCore;
using SsoCustom.Entities;
using SsoCustom.Features.Users;

var builder = WebApplication.CreateBuilder(args);

// Configurar MongoDB
builder.Services.AddSingleton<IMongoClient>(sp =>
{
    var settings = MongoClientSettings.FromConnectionString(builder.Configuration.GetConnectionString("MongoDb"));
    return new MongoClient(settings);
});

builder.Services.AddSingleton(sp =>
{
    var client = sp.GetRequiredService<IMongoClient>();
    return client.GetDatabase(builder.Configuration.GetConnectionString("DatabaseName"));
});

// Configurar ASP.NET Core Identity con MongoDB
// builder.Services.AddIdentityMongoDbProvider<UserEntity, UserRoleEntity>(identityOptions =>
//     {
//         identityOptions.Password.RequiredLength = 6;
//         identityOptions.Password.RequireLowercase = false;
//         identityOptions.Password.RequireUppercase = false;
//         identityOptions.Password.RequireNonAlphanumeric = false;
//         identityOptions.Password.RequireDigit = false;
//     },
//     mongoIdentityOptions =>
//     {
//         mongoIdentityOptions.ConnectionString = builder.Configuration.GetConnectionString("MongoDb");
//     });

builder.Services.AddIdentity<UserEntity, UserRoleEntity>(identityOptions =>
    {
        identityOptions.Password.RequiredLength = 6;
        identityOptions.Password.RequireLowercase = false;
        identityOptions.Password.RequireUppercase = false;
        identityOptions.Password.RequireNonAlphanumeric = false;
        identityOptions.Password.RequireDigit = false; 
    })
    .AddMongoDbStores<UserEntity, UserRoleEntity, Guid>
    (builder.Configuration.GetConnectionString("MongoDb"),
        builder.Configuration.GetConnectionString("DatabaseName"))
    ;
    // {
    //     mongoDbSettings.ConnectionString, mongoDbSettings.Name
    //     identityOptions.Password.RequiredLength = 6;
    //     identityOptions.Password.RequireLowercase = false;
    //     identityOptions.Password.RequireUppercase = false;
    //     identityOptions.Password.RequireNonAlphanumeric = false;
    //     identityOptions.Password.RequireDigit = false;
    // },
    // mongoIdentityOptions =>
    // {
    //     mongoIdentityOptions.ConnectionString = builder.Configuration.GetConnectionString("MongoDb");
    // });

// Configurar OpenIddict con MongoDB
builder.Services.AddOpenIddict()
    .AddCore(options =>
    {
        options.UseMongoDb()
            .UseDatabase(new MongoClient(builder.Configuration.GetConnectionString("MongoDb")).GetDatabase(builder.Configuration.GetConnectionString("DatabaseName")))
            ;
        // options.UseMongoDb(x => x.UseDatabase(sp => sp.GetRequiredService<IMongoDatabase>()));
        // options.UseMongoDb();
            // .UseDatabase(sp => sp.GetRequiredService<IMongoDatabase>());
        // options.UseMongoDb(x => x.UseDatabase(new MongoClient(builder.Configuration.GetConnectionString("MongoDb")).GetDatabase("Users")))
            // .UseDatabase(builder.Configuration.GetConnectionString("MongoDb"))
            ;
    })
    .AddServer(options =>
    {
        options.SetTokenEndpointUris("/connect/token");

        // Enable the client credentials flow.
        options
            .AllowPasswordFlow()
            .AllowClientCredentialsFlow();

        // Register the signing and encryption credentials.
        options.AddEphemeralEncryptionKey()
               .AddEphemeralSigningKey()
               .DisableAccessTokenEncryption();

        // Register the ASP.NET Core host and configure the ASP.NET Core options.
        options.UseAspNetCore()
            .EnableTokenEndpointPassthrough();

        options.RegisterScopes(
            OpenIddictConstants.Scopes.OpenId,
            OpenIddictConstants.Scopes.Profile,
            OpenIddictConstants.Scopes.Email
        );
    })
    .AddValidation(options =>
    {
        // Import the configuration from the local OpenIddict server instance.
        options.UseLocalServer();

        // Register the ASP.NET Core host.
        options.UseAspNetCore();
    });
builder.Services.AddAuthorization();
builder.Services.AddAuthentication(options =>
{
    options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
}).AddJwtBearer(options =>
{
    options.Authority = builder.Configuration["Jwt:Authority"];
    options.Audience = builder.Configuration["Jwt:Audience"];
    options.RequireHttpsMetadata = false;
    options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true
    };
});

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.RegisterUserRoutes();
// app.MapPost("/connect/token", async (HttpContext httpContext) =>
// {
//     var request = httpContext.GetOpenIddictServerRequest() ??
//                   throw new InvalidOperationException("The OpenIddict server request cannot be retrieved.");
//
//     if (request.IsPasswordGrantType())
//     {
//         var userManager = httpContext.RequestServices.GetRequiredService<UserManager<MongoUser>>();
//
//         var user = await userManager.FindByNameAsync(request.Username);
//         if (user == null || !await userManager.CheckPasswordAsync(user, request.Password))
//         {
//             return Results.BadRequest(new OpenIddictResponse
//             {
//                 Error = OpenIddictConstants.Errors.InvalidGrant,
//                 ErrorDescription = "The username/password couple is invalid."
//             });
//         }
//
//         // Create a new ClaimsPrincipal containing the claims that
//         // will be used to create an id_token and/or an access token.
//         var principal = await httpContext.RequestServices.GetRequiredService<SignInManager<MongoUser>>()
//             .CreateUserPrincipalAsync(user);
//
//         // Set the list of scopes granted to the client application.
//         principal.SetScopes(new[]
//         {
//             // OpenIddictConstants.Permissions.Scopes.OpenId,
//             OpenIddictConstants.Permissions.Scopes.Email,
//             OpenIddictConstants.Permissions.Scopes.Profile,
//             OpenIddictConstants.Permissions.Scopes.Roles
//         }.Intersect(request.GetScopes()));
//
//         return Results.SignIn(principal, properties: null, OpenIddictServerAspNetCoreDefaults.AuthenticationScheme);
//     }
//
//     return Results.BadRequest(new OpenIddictResponse
//     {
//         Error = OpenIddictConstants.Errors.UnsupportedGrantType,
//         ErrorDescription = "The specified grant type is not supported."
//     });
// });

app.MapGet("/api/protected", [Authorize] () => Results.Ok(new { message = "This is a protected endpoint" }));

// Inicializar la base de datos
await InitializeOpenIddictAsync(app.Services);

app.Run();

static async Task InitializeOpenIddictAsync(IServiceProvider services)
{
    using var scope = services.CreateScope();
    var context = scope.ServiceProvider.GetRequiredService<IMongoDatabase>();
    var manager = scope.ServiceProvider.GetRequiredService<IOpenIddictApplicationManager>();

    if (await manager.FindByClientIdAsync("client_id") is null)
    {
        await manager.CreateAsync(new OpenIddictApplicationDescriptor
        {
            ClientId = "client_id",
            ClientSecret = "client_secret",
            Permissions =
            {
                OpenIddictConstants.Permissions.GrantTypes.Password,
                OpenIddictConstants.Permissions.Endpoints.Token,
                OpenIddictConstants.Permissions.GrantTypes.ClientCredentials,
                OpenIddictConstants.Permissions.Prefixes.Scope + "openid",
                OpenIddictConstants.Permissions.Prefixes.Scope + "profile",
                OpenIddictConstants.Permissions.Prefixes.Scope + "email",
                OpenIddictConstants.Permissions.Prefixes.Scope + "api1"
            }
        });
    }
}
