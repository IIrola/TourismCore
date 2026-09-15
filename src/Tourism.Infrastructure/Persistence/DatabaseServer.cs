using Microsoft.EntityFrameworkCore;

namespace Tourism.Infrastructure.Persistence;

/// <summary>
/// The server version every path to the database agrees on — the application's own registration
/// and the EF tooling's design-time factory. Two declarations of the same fact drift, and this
/// one decides which SQL gets generated.
///
/// Pinned instead of <c>ServerVersion.AutoDetect</c>: auto-detection opens a connection while the
/// container is still being configured, which fails startup when the database is not up yet.
/// </summary>
public static class DatabaseServer
{
    public static readonly MariaDbServerVersion Version = new(new Version(11, 4, 0));
}
