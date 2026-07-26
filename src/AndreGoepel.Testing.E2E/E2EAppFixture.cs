using Aspire.Hosting;
using Aspire.Hosting.ApplicationModel;
using Aspire.Hosting.Testing;
using Microsoft.Extensions.DependencyInjection;

namespace AndreGoepel.Testing.E2E;

/// <summary>
/// Boots the whole Aspire application graph once for the entire test collection, then launches a
/// single Chromium browser that every test shares. Subclass with a parameterless constructor so
/// xUnit's <c>ICollectionFixture&lt;T&gt;</c> can construct it:
/// <code>
/// public sealed class E2EAppFixture()
///     : AndreGoepel.Testing.E2E.E2EAppFixture(new E2EAppFixtureOptions { ... });
/// </code>
/// </summary>
public class E2EAppFixture(E2EAppFixtureOptions options) : IAsyncLifetime
{
    private DistributedApplication? _app;
    private IPlaywright? _playwright;
    private IBrowser? _browser;
    private bool _adminProvisioned;
    private readonly SemaphoreSlim _provisionGate = new(1, 1);

    /// <summary>Base address of the web app under test, e.g. <c>https://localhost:1234/</c>.</summary>
    public string AppBaseUrl { get; private set; } = default!;

    /// <summary>The single browser shared by the collection. Tests create their own context via <see cref="NewContextAsync"/>.</summary>
    public IBrowser Browser =>
        _browser ?? throw new InvalidOperationException("Browser not started.");

    /// <summary>
    /// Reads links from delivered mail, or <c>null</c> when neither <see cref="E2EAppFixtureOptions.MailHogResourceName"/>
    /// nor <see cref="E2EAppFixtureOptions.MailSourceFactory"/> was configured.
    /// </summary>
    public IEmailLinkSource? Mail { get; private set; }

    public virtual async ValueTask InitializeAsync()
    {
        var appHostBuilder = await options.CreateAppHostBuilder(options.AppHostArguments);
        _app = await appHostBuilder.BuildAsync();

        using var startupCts = new CancellationTokenSource(options.StartupTimeout);
        await _app.StartAsync(startupCts.Token);

        var notifications = _app.Services.GetRequiredService<ResourceNotificationService>();
        await notifications.WaitForResourceHealthyAsync(options.WebResourceName, startupCts.Token);

        AppBaseUrl = _app.GetEndpoint(options.WebResourceName, "https").ToString();
        Mail = CreateMailSource();

        _playwright = await Playwright.CreateAsync();
        _browser = await _playwright.Chromium.LaunchAsync(
            new BrowserTypeLaunchOptions { Headless = !IsHeaded }
        );
    }

    private IEmailLinkSource? CreateMailSource()
    {
        if (options.MailSourceFactory is not null)
        {
            return options.MailSourceFactory(AppBaseUrl);
        }

        if (options.MailHogResourceName is not null)
        {
            var mailHogApiUrl = _app!
                .GetEndpoint(options.MailHogResourceName, options.MailHogEndpointName)
                .ToString();
            return new MailHogClient(mailHogApiUrl);
        }

        return null;
    }

    /// <summary>
    /// Runs the one-time Setup flow (create root admin) exactly once per app instance, in its own
    /// throwaway context so it never disturbs a test's session. Idempotent: if the admin already
    /// exists, Setup server-redirects away and nothing is created.
    /// </summary>
    public async Task ProvisionAdminAsync()
    {
        if (_adminProvisioned)
        {
            return;
        }

        await _provisionGate.WaitAsync();
        try
        {
            if (_adminProvisioned)
            {
                return;
            }

            await using var context = await NewContextAsync();
            var page = await context.NewPageAsync();
            await page.GotoAsync("/Setup");
            await page.WaitForBlazorAsync();

            if (
                new Uri(page.Url)
                    .AbsolutePath.Trim('/')
                    .Equals("Setup", StringComparison.OrdinalIgnoreCase)
            )
            {
                // On the app's very first circuit, interactivity can attach a beat after
                // window.Blazor exists (cold JIT); a click landing in that gap silently
                // does nothing. Retry fill+submit, reloading in between — once the setup
                // succeeded server-side, the reload redirects away by itself.
                for (var attempt = 0; ; attempt++)
                {
                    await page.FillFieldAsync("Email", TestData.AdminEmail);
                    await page.FillFieldAsync("Password", TestData.DefaultPassword);
                    await page.FillFieldAsync("ConfirmPassword", TestData.DefaultPassword);
                    await page.ClickButtonAsync(options.ProvisionAdminButtonText);
                    try
                    {
                        await page.WaitForURLAsync(
                            url =>
                                !new Uri(url).AbsolutePath.Contains(
                                    "Setup",
                                    StringComparison.OrdinalIgnoreCase
                                ),
                            new PageWaitForURLOptions { Timeout = 15_000 }
                        );
                        break;
                    }
                    catch (TimeoutException) when (attempt < 7)
                    {
                        await page.ReloadAsync();
                        await page.WaitForBlazorAsync();
                        if (
                            !new Uri(page.Url)
                                .AbsolutePath.Trim('/')
                                .Equals("Setup", StringComparison.OrdinalIgnoreCase)
                        )
                        {
                            break;
                        }
                    }
                }
            }

            _adminProvisioned = true;
        }
        finally
        {
            _provisionGate.Release();
        }
    }

    /// <summary>Creates an isolated browser context (fresh cookies/storage) for a single test.</summary>
    public Task<IBrowserContext> NewContextAsync() =>
        Browser.NewContextAsync(
            new BrowserNewContextOptions
            {
                BaseURL = AppBaseUrl,
                IgnoreHTTPSErrors = true, // dev cert on the https endpoint
            }
        );

    public virtual async ValueTask DisposeAsync()
    {
        if (_browser is not null)
        {
            await _browser.DisposeAsync();
        }

        _playwright?.Dispose();

        if (Mail is IDisposable disposableMail)
        {
            disposableMail.Dispose();
        }

        if (_app is not null)
        {
            await _app.DisposeAsync();
        }
    }

    private static bool IsHeaded =>
        string.Equals(
            Environment.GetEnvironmentVariable("E2E_HEADED"),
            "true",
            StringComparison.OrdinalIgnoreCase
        );
}
