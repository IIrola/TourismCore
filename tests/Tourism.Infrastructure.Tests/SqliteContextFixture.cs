using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Tourism.Infrastructure.Persistence;

namespace Tourism.Infrastructure.Tests;

/// <summary>
/// One private SQLite in-memory database per test, built from the real
/// <see cref="TourismDbContext"/> model so <c>TourismOrganizationProfileConfiguration</c>
/// (the unique index on OrganizationId, the nullable enum conversions) is exercised for real
/// rather than approximated.
///
/// The connection must stay open for the fixture's lifetime: SQLite's ":memory:" database is
/// destroyed the moment its one connection closes, which is also why it cannot be shared
/// across fixtures/tests.
/// </summary>
public sealed class SqliteContextFixture : IDisposable
{
    private readonly SqliteConnection _connection;

    public TourismDbContext Context { get; }

    public SqliteContextFixture()
    {
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var options = new DbContextOptionsBuilder<TourismDbContext>()
            .UseSqlite(_connection)
            .Options;

        Context = new TourismDbContext(options);
        Context.Database.EnsureCreated();
    }

    /// <summary>
    /// A second, independent, untracked <see cref="TourismDbContext"/> over the same
    /// in-memory database. Reading through this instead of <see cref="Context"/> proves a
    /// value actually round-trips through the mapping, rather than merely surviving because
    /// the original context still has it cached in its change tracker.
    /// </summary>
    public TourismDbContext CreateAdditionalContext()
    {
        var options = new DbContextOptionsBuilder<TourismDbContext>()
            .UseSqlite(_connection)
            .Options;
        return new TourismDbContext(options);
    }

    public void Dispose()
    {
        Context.Dispose();
        _connection.Dispose();
    }
}
