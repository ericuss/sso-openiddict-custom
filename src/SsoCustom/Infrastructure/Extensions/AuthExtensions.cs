using Microsoft.AspNetCore.Authentication.JwtBearer;
using MongoDB.Driver;
using OpenIddict.Abstractions;
using SsoCustom.Entities;

namespace SsoCustom.Infrastructure.Extensions;

public static class AuthExtensions
{
    public static IServiceCollection ConfigureAuth(this IServiceCollection services, IConfiguration config)
    {
        services.AddIdentity<UserEntity, UserRoleEntity>(identityOptions =>
            {
                identityOptions.Password.RequiredLength = 6;
                identityOptions.Password.RequireLowercase = false;
                identityOptions.Password.RequireUppercase = false;
                identityOptions.Password.RequireNonAlphanumeric = false;
                identityOptions.Password.RequireDigit = false;
            })
            .AddMongoDbStores<UserEntity, UserRoleEntity, Guid>
            (config.GetConnectionString("MongoDb"),
                config.GetConnectionString("DatabaseName"))
            ;

// Configurar OpenIddict con MongoDB
        services.AddOpenIddict()
            .AddCore(options =>
            {
                options.UseMongoDb()
                    .UseDatabase(
                        new MongoClient(config.GetConnectionString("MongoDb")).GetDatabase(
                            config.GetConnectionString("DatabaseName")))
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
        services.AddAuthorization();
        services.AddAuthentication(options =>
        {
            options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
            options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
        }).AddJwtBearer(options =>
        {
            options.Authority = config["Jwt:Authority"];
            options.Audience = config["Jwt:Audience"];
            options.RequireHttpsMetadata = false;
            options.TokenValidationParameters = new Microsoft.IdentityModel.Tokens.TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true
            };
        });
        
        return services;
    }
    
    
    public static async Task InitializeOpenIddictAsync(IServiceProvider services)
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
}