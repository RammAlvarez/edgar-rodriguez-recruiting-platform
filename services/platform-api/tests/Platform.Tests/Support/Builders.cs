using Platform.Core.Clients;
using Platform.Core.People;
using Platform.Core.Users;
using Platform.Infrastructure.Persistence;

namespace Platform.Tests.Support;

public static class Builders
{
    public static async Task<User> AddUser(this PlatformDbContext db, UserRole role, string name = "Usuario",
        bool active = true, Guid? clientId = null, IEnumerable<UserPermission>? grants = null)
    {
        var user = new User
        {
            AuthUid = $"auth-{Guid.NewGuid():N}",
            Email = $"{Guid.NewGuid():N}@example.test",
            DisplayName = name,
            Role = role,
            IsActive = active,
            ClientId = clientId,
            Permissions = grants?.ToList() ?? [],
        };
        db.Add(user);
        await db.SaveChangesAsync();
        return user;
    }

    public static async Task<Client> AddClient(this PlatformDbContext db, string shortName = "ECOVIA")
    {
        var client = new Client { ShortName = shortName };
        db.Add(client);
        await db.SaveChangesAsync();
        return client;
    }

    public static async Task<Person> AddPerson(this PlatformDbContext db, string firstNames = "ANA", string lastNamePaternal = "PÉREZ",
        string? lastNameMaternal = "LÓPEZ")
    {
        var person = new Person { FirstNames = firstNames, LastNamePaternal = lastNamePaternal, LastNameMaternal = lastNameMaternal };
        db.Add(person);
        await db.SaveChangesAsync();
        return person;
    }
}
