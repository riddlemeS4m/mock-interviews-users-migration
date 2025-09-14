using Microsoft.EntityFrameworkCore;
using sp2023_mis421_mockinterviews.Data.Contexts;

public class PostgresDbContext : UsersDbContext
{
    public PostgresDbContext(DbContextOptions<sp2023_mis421_mockinterviews.Data.Contexts.UsersDbContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
            options.UseNpgsql(Environment.GetEnvironmentVariable("POSTGRES_CONN"));
    }
}