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
using SsoCustom.Infrastructure.Extensions;

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

builder.Services.ConfigureAuth(builder.Configuration);

var app = builder.Build();

app.UseAuthentication();
app.UseAuthorization();

app.RegisterUserRoutes();

app.MapGet("/api/protected", [Authorize] () => Results.Ok(new { message = "This is a protected endpoint" }));

// Inicializar la base de datos
await AuthExtensions.InitializeOpenIddictAsync(app.Services);

app.Run();

