using AspNetCore.Identity.MongoDbCore.Models;

namespace SsoCustom.Entities;

public class UserEntity : MongoIdentityUser
{
    public string Firstname { get; set; }

    public string Lastname { get; set; }
}