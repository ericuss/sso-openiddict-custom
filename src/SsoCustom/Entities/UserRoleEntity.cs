using AspNetCore.Identity.MongoDbCore.Models;

namespace SsoCustom.Entities;

public class UserRoleEntity : MongoIdentityRole
{
    public UserRoleEntity() : base() { }

    public UserRoleEntity(string roleName) : base(roleName) { }
}