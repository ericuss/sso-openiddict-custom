using Microsoft.AspNetCore.Identity;
using SsoCustom.Entities;

namespace SsoCustom.Features.Users;

public class CreateUserDto
{
    public string Email { get; set; }
    public string Password { get; set; }
}

public static class CreateUser
{
    public static async Task<IResult> Handler(
        CreateUserDto dto,
        UserManager<UserEntity> userManager)
    {
        var user = new UserEntity()
        {
            UserName = dto.Email,
            Email = dto.Email,
        };

        var result = await userManager.CreateAsync(user, dto.Password);

        if (result.Succeeded)
        {
            return Results.Ok(new { message = "User created successfully" });
        }

        var errors = string.Join(", ", result.Errors.Select(e => e.Description));
        return Results.BadRequest(new { message = $"User creation failed: {errors}" });
    }
}