namespace AndreGoepel.Testing.E2E;

/// <summary>
/// The minimal contract <see cref="E2ETestBase{TFixture}.ConfirmEmailAsync"/> needs from wherever the
/// app under test's outbound mail ends up. <see cref="MailHogClient"/> is the default implementation;
/// implement this yourself when the app captures mail some other way (e.g. in-memory, for a sample
/// that sends no real email) and wire it up via <see cref="E2EAppFixtureOptions.MailSourceFactory"/>.
/// </summary>
public interface IEmailLinkSource
{
    /// <summary>
    /// Waits for a link addressed to <paramref name="toEmail"/> and containing
    /// <paramref name="mustContain"/> (e.g. <c>Account/ConfirmEmail</c>) to become available, then
    /// returns it.
    /// </summary>
    Task<string> WaitForLinkAsync(
        string toEmail,
        string mustContain,
        TimeSpan? timeout = null,
        CancellationToken ct = default
    );
}
