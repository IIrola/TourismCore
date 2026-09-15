using Microsoft.EntityFrameworkCore;
using Tourism.Infrastructure.Persistence;

// Applies the pending migrations and exits. It ships inside the API's image and runs as a one-shot
// container ahead of it, so the schema and the code that expects it carry the same tag and cannot
// drift apart.
//
// Why a program of our own rather than `dotnet ef migrations bundle`: the bundle reaches the
// DbContext by building the application's service provider, which runs the API's whole startup —
// against a database whose schema does not exist yet. Platform's authorization seeder queries
// tables the migrations have not created, the exception is unhandled, and the process dies before
// a single migration is applied. Pointing the bundle at the Infrastructure project does not change
// that. This does one job and says so.
//
// It is also the reason nothing migrates on startup: with several instances, every one of them
// would race to change the schema. Here the change is a step that has to finish, and can be
// watched failing, before the new code runs at all.

const string ConnectionArgument = "--connection";
const string ConnectionVariable = "ConnectionStrings__DefaultConnection";

try
{
    var connectionString = ResolveConnectionString(args);

    var options = new DbContextOptionsBuilder<TourismDbContext>()
        .UseMySql(connectionString, DatabaseServer.Version)
        .Options;

    await using var context = new TourismDbContext(options);

    await WaitForServerAsync(context);

    var pending = (await context.Database.GetPendingMigrationsAsync()).ToList();
    if (pending.Count == 0)
    {
        Console.WriteLine("Tourism: the schema is already up to date.");
        return 0;
    }

    Console.WriteLine($"Tourism: applying {pending.Count} migration(s): {string.Join(", ", pending)}");
    await context.Database.MigrateAsync();
    Console.WriteLine("Tourism: done.");
    return 0;
}
catch (Exception ex)
{
    // Non-zero is the whole contract with the deployment: the API is not allowed to start behind a
    // migration that did not finish.
    Console.Error.WriteLine($"Tourism: the migration failed. {ex.Message}");
    Console.Error.WriteLine(ex.ToString());
    return 1;
}

// The environment is how a container is configured, and --connection is how a person overrides it
// from a terminal. Deliberately no appsettings fallback: the API's own file sits next to this
// program in the image, and silently migrating whatever it happens to point at is exactly the kind
// of accident a deployment cannot afford.
static string ResolveConnectionString(string[] args)
{
    var index = Array.IndexOf(args, ConnectionArgument);
    if (index >= 0)
    {
        if (index + 1 >= args.Length || string.IsNullOrWhiteSpace(args[index + 1]))
        {
            throw new InvalidOperationException($"{ConnectionArgument} was given without a value.");
        }

        return args[index + 1];
    }

    var fromEnvironment = Environment.GetEnvironmentVariable(ConnectionVariable);
    if (!string.IsNullOrWhiteSpace(fromEnvironment))
    {
        return fromEnvironment;
    }

    throw new InvalidOperationException(
        $"No connection string. Set {ConnectionVariable}, or pass {ConnectionArgument} <value>.");
}

// A database container that is up is not yet a database server that answers. Waiting here rather
// than relying only on the orchestrator's health check keeps the migrator correct on its own.
static async Task WaitForServerAsync(DbContext context)
{
    var deadline = DateTime.UtcNow.AddSeconds(60);
    while (true)
    {
        try
        {
            await context.Database.OpenConnectionAsync();
            await context.Database.CloseConnectionAsync();
            return;
        }
        catch (Exception ex) when (DateTime.UtcNow < deadline)
        {
            Console.WriteLine($"Tourism: waiting for the database — {ex.Message}");
            await Task.Delay(TimeSpan.FromSeconds(3));
        }
    }
}
