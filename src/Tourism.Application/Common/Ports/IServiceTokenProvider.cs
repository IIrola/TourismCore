namespace Tourism.Application.Common.Ports;

/// <summary>
/// Obtains the token BIT presents when calling another service.
///
/// Platform issues these. BIT holds credentials for itself as a service — distinct from any
/// end user's credentials — and exchanges them for a short-lived token scoped to the service
/// it intends to call. Keeping the two identities apart is what stops a machine from
/// inheriting the reach of whoever it happens to be acting for.
///
/// Implementations are expected to cache: a ten-minute token fetched on every outbound call
/// would put Platform on the critical path of every evaluation.
/// </summary>
public interface IServiceTokenProvider
{
    /// <param name="audience">The service the token will be presented to.</param>
    Task<string> GetTokenAsync(string audience, CancellationToken cancellationToken = default);
}

public interface IClock
{
    DateTime UtcNow { get; }
}

public interface IUnitOfWork
{
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
