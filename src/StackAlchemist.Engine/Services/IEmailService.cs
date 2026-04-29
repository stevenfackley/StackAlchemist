namespace StackAlchemist.Engine.Services;

public interface IEmailService
{
    /// <summary>
    /// Sends a transactional email. Implementations must NOT throw on transport
    /// errors — email is best-effort and never blocks the main pipeline.
    /// </summary>
    Task SendAsync(string to, string subject, string html, CancellationToken ct = default);
}
