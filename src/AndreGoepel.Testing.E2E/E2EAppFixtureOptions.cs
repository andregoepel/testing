using Aspire.Hosting.Testing;

namespace AndreGoepel.Testing.E2E;

/// <summary>Configures <see cref="E2EAppFixture"/> for one host application.</summary>
public sealed record E2EAppFixtureOptions
{
    /// <summary>
    /// Builds the Aspire testing host, e.g.
    /// <c>args =&gt; DistributedApplicationTestingBuilder.CreateAsync&lt;Projects.YourApp_AppHost&gt;(args)</c>.
    /// A factory (rather than a generic type parameter on the fixture itself) so <see cref="E2ETestBase{TFixture}"/>
    /// can stay constrained to the single non-generic <see cref="E2EAppFixture"/> base type regardless of
    /// which <c>Projects.*</c> type each host's AppHost generates.
    /// </summary>
    public required Func<
        string[],
        Task<IDistributedApplicationTestingBuilder>
    > CreateAppHostBuilder { get; init; }

    /// <summary>Resource name of the web app as declared in the AppHost (e.g. <c>builder.AddProject("web", ...)</c>).</summary>
    public required string WebResourceName { get; init; }

    /// <summary>
    /// Visible text of the Setup page's submit button, matched with a non-exact ("contains") match — a
    /// stable prefix like <c>"Create admin"</c> matches a longer label like <c>"Create admin &amp; complete
    /// setup"</c>. Deliberately has no default: the exact wording differs per app, and guessing wrong
    /// would make <see cref="E2EAppFixture.ProvisionAdminAsync"/> silently no-op the click instead of
    /// provisioning the admin.
    /// </summary>
    public required string ProvisionAdminButtonText { get; init; }

    /// <summary>
    /// Resource name of the MailHog container, if the app under test has one. Leave <c>null</c> when it
    /// doesn't (e.g. it captures outbound mail itself) — supply <see cref="MailSourceFactory"/> instead
    /// if <c>ConfirmEmailAsync</c> is still needed.
    /// </summary>
    public string? MailHogResourceName { get; init; }

    /// <summary>
    /// Endpoint name MailHog's HTTP API is exposed under in the AppHost (e.g.
    /// <c>builder.AddContainer("mailhog", ...).WithHttpEndpoint(name: MailHogEndpointName, ...)</c>).
    /// Canonical value across the ecosystem is <c>"http"</c> — it distinguishes the endpoint from
    /// MailHog's separate <c>"smtp"</c> endpoint, unlike the <c>"web"</c> name one repo used historically.
    /// Only takes effect when <see cref="MailHogResourceName"/> is set.
    /// </summary>
    public string MailHogEndpointName { get; init; } = "http";

    /// <summary>
    /// Builds the <see cref="IEmailLinkSource"/> exposed as <see cref="E2EAppFixture.Mail"/>, given the
    /// running app's base URL. Set this instead of <see cref="MailHogResourceName"/> when the app under
    /// test captures mail some other way than MailHog. Takes precedence over
    /// <see cref="MailHogResourceName"/> when both are set.
    /// </summary>
    public Func<string, IEmailLinkSource>? MailSourceFactory { get; init; }

    /// <summary>
    /// Extra arguments passed to <see cref="CreateAppHostBuilder"/>. Defaults to E2E mode only
    /// (<c>"E2E=true"</c>, the convention every host uses to run its database without a persistent
    /// volume) — add any secret parameters the AppHost requires (e.g. a database password) here.
    /// </summary>
    public string[] AppHostArguments { get; init; } = ["E2E=true"];

    /// <summary>How long to wait for the web resource to report healthy before giving up.</summary>
    public TimeSpan StartupTimeout { get; init; } = TimeSpan.FromMinutes(5);
}
