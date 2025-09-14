using Microsoft.EntityFrameworkCore;
using sp2023_mis421_mockinterviews.Data.Contexts;

public class SqlServerDbContext : UsersDbContext
{
    public SqlServerDbContext(DbContextOptions<sp2023_mis421_mockinterviews.Data.Contexts.UsersDbContext> options) : base(options) { }

    protected override void OnConfiguring(DbContextOptionsBuilder options)
    {
        if (!options.IsConfigured)
            options.UseSqlServer(Environment.GetEnvironmentVariable("SQLSERVER_CONN"));
    }
}