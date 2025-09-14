using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using sp2023_mis421_mockinterviews.Data.Contexts;
using sp2023_mis421_mockinterviews.Models.UserDb;
using System.Security.Cryptography;
using System.Text;

AppContext.SetSwitch("Npgsql.EnableLegacyTimestampBehavior", true); // for Npgsql 6

var sqlOptions = new DbContextOptionsBuilder<UsersDbContext>()
    .UseSqlServer(Environment.GetEnvironmentVariable("SQLSERVER_CONN"))
    .Options;

var pgOptions = new DbContextOptionsBuilder<UsersDbContext>()
    .UseNpgsql(Environment.GetEnvironmentVariable("POSTGRES_CONN"))
    .Options;

await using var sql = new SqlServerDbContext(sqlOptions);
await using var pg  = new PostgresDbContext(pgOptions);

// speed-ups
pg.ChangeTracker.AutoDetectChangesEnabled = false;

// one big transaction is fine for your size; you can do per-table if you prefer
await using var tx = await pg.Database.BeginTransactionAsync();

Console.WriteLine("Migrating AspNetRoles (parents)...");
await Copy(sql.Set<IdentityRole>().AsNoTracking().OrderBy(r => r.Id),
           pg.Set<IdentityRole>(), pg);

// USERS: de-identify while copying
Console.WriteLine("Migrating AspNetUsers with de-identification...");
const int batch = 500;
for (int skip = 0; ; skip += batch)
{
    var users = await sql.Set<ApplicationUser>().AsNoTracking()
        .OrderBy(u => u.Id).Skip(skip).Take(batch).ToListAsync();

    if (users.Count == 0) break;

    foreach (var u in users)
    {
        var (first, last, suffix) = Pseudo(u.Id, u.FirstName, u.LastName);
        var email = $"{first}.{last}.{suffix}@samriddle.online".ToLowerInvariant();
        var userName = $"{first}.{last}.{suffix}@samriddle.online".ToLowerInvariant();

        var copy = new ApplicationUser
        {
            Id = u.Id, // preserve PK
            FirstName = first,
            LastName = last,
            Class = u.Class,
            Company = u.Company,
            ProfilePicture = null, // clear
            Resume = null,         // clear

            // Identity fields
            UserName = userName,                 // optional: keep or null; sign-in won't work anyway
            NormalizedUserName = userName.ToUpperInvariant(),
            Email = email,
            NormalizedEmail = email.ToUpperInvariant(),
            EmailConfirmed = false,
            PasswordHash = null,
            SecurityStamp = null,
            ConcurrencyStamp = null,
            PhoneNumber = null,
            PhoneNumberConfirmed = false,
            TwoFactorEnabled = false,
            LockoutEnd = null,
            LockoutEnabled = false,
            AccessFailedCount = 0
        };

        pg.Set<ApplicationUser>().Add(copy);
    }

    await pg.SaveChangesAsync();
    pg.ChangeTracker.Clear();
}

// children of roles/users (FK-safe order)
Console.WriteLine("Migrating RoleClaims...");
await Copy(sql.Set<IdentityRoleClaim<string>>().AsNoTracking().OrderBy(x => x.Id),
           pg.Set<IdentityRoleClaim<string>>(), pg);

Console.WriteLine("Migrating UserClaims...");
await Copy(sql.Set<IdentityUserClaim<string>>().AsNoTracking().OrderBy(x => x.Id),
           pg.Set<IdentityUserClaim<string>>(), pg);

Console.WriteLine("Migrating UserLogins...");
await Copy(sql.Set<IdentityUserLogin<string>>().AsNoTracking()
            .OrderBy(x => x.UserId).ThenBy(x => x.LoginProvider).ThenBy(x => x.ProviderKey),
           pg.Set<IdentityUserLogin<string>>(), pg);

Console.WriteLine("Migrating UserRoles...");
await Copy(sql.Set<IdentityUserRole<string>>().AsNoTracking()
            .OrderBy(x => x.UserId).ThenBy(x => x.RoleId),
           pg.Set<IdentityUserRole<string>>(), pg);

Console.WriteLine("Migrating UserTokens...");
await Copy(sql.Set<IdentityUserToken<string>>().AsNoTracking()
            .OrderBy(x => x.UserId).ThenBy(x => x.LoginProvider).ThenBy(x => x.Name),
           pg.Set<IdentityUserToken<string>>(), pg);

// if you have additional app tables: copy here in parent->child order

await tx.CommitAsync();
Console.WriteLine("Done.");

// --- helpers ---
static async Task Copy<T>(IQueryable<T> src, DbSet<T> dest, DbContext ctx) where T : class
{
    const int size = 2000;
    for (int skip = 0; ; skip += size)
    {
        var page = await src.Skip(skip).Take(size).ToListAsync();
        if (page.Count == 0) break;
        dest.AddRange(page);
        await ctx.SaveChangesAsync();
        ctx.ChangeTracker.Clear();
    }
}

static (string First, string Last, string suffix) Pseudo(string id, string? first, string? last)
{
    using var h = SHA256.Create();
    var seedBytes = h.ComputeHash(Encoding.UTF8.GetBytes($"{id}|{first}|{last}"));
    var seed = BitConverter.ToInt32(seedBytes, 0);
    var rnd = new Random(seed);

    var firsts = new[]
    {
        "Alex","Taylor","Jordan","Casey","Riley","Quinn","Morgan","Avery","Reese","Jamie",
        "Rowan","Parker","Drew","Shawn","Emerson","Hayden","Skyler","Finley","Sage","Kendall",
        "Cameron","Logan","Blake","Harper","Elliot","Dana","Micah","Charlie","Dakota","Peyton",
        "Jude","Remy","Rory","Eden","Adrian","Alexis","Bailey","Brett","Campbell","Chandler",
        "Corey","Darian","Devon","Emery","Frankie","Hollis","Jesse","Jules","Kai","Kasey",
        "Kris","Lane","Lennon","Linden","Luca","Marley","Monroe","Noel","Oakley","Phoenix",
        "Reagan","River","Rylan","Sasha","Shiloh","Sidney","Spencer","Stevie","Teagan","Toby",
        "Tristan","Val","Wren","Arden","Bellamy","Blaine","Brighton","Cody","Dallas","Ellis",
        "Gray","Indy","Jaden","Keegan","Kendrick","Laken","Leighton","Lex","Merritt","Murphy",
        "Nico","Parker","Quincy","Reign","Sutton","Tanner","Tyler","Vaughn","Willow","Zephyr"
    };

    var lasts = new[]
    {
        "Hill","Brooks","Reed","Parker","Gray","Mason","Price","Wells","Cooper","Hayes",
        "Bennett","Collins","Foster","Greer","Jensen","Kennedy","Monroe","Palmer","Sawyer","Wade",
        "Adams","Baker","Barnes","Bell","Bishop","Boone","Bowen","Brady","Bryant","Carson",
        "Chambers","Clarke","Clayton","Cole","Collins","Cruz","Dalton","Dawson","Dean","Dixon",
        "Douglas","Doyle","Drake","Dunn","Eaton","Ellis","Farrell","Fischer","Fleming","Ford",
        "Fowler","Franklin","Garner","Gibbs","Glover","Grady","Grant","Griffin","Hale","Hardy",
        "Harmon","Harper","Harris","Hart","Hendrix","Holt","Hopkins","Hudson","Hughes","Hunter",
        "Ingram","Jarvis","Keller","Lane","Lawson","Logan","Lowe","Manning","Marshall","Massey",
        "Matthews","Maxwell","McCoy","Meyer","Mills","Moody","Nash","Newman","Norton","Page",
        "Payne","Pierce","Poole","Porter","Pratt","Quinn","Ramsey","Reeves","Rhodes","Roy"
    };

    var suffix = Math.Abs(seed % 1000).ToString("000"); // 000–999


    return (firsts[rnd.Next(firsts.Length)], lasts[rnd.Next(lasts.Length)], suffix);
}