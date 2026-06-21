using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;

namespace OPorDie.Data;

// A SEPARATE database just for user accounts (logins, password hashes, etc.).
// Keeping it apart from AppDbContext means your card/deck data is never touched
// by the auth system. ASP.NET Core Identity manages all the tables for us.
public class UsersDbContext : IdentityDbContext
{
    public UsersDbContext(DbContextOptions<UsersDbContext> options) : base(options) { }
}
