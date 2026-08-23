namespace AndreGoepel.Testing.E2E;

/// <summary>
/// Base class for every E2E test: one fresh browser context + page per test (so cookies never leak
/// between tests), the common account flows expressed as intent-revealing helpers, and
/// trace-capture-on-failure — unconditional, not opt-in, since a headless CI failure with no trace is
/// undebuggable.
/// </summary>
[Collection(E2ECollectionDefaults.Name)]
public abstract class E2ETestBase<TFixture>(TFixture fixture) : IAsyncLifetime
    where TFixture : E2EAppFixture
{
    protected TFixture Fixture { get; } = fixture;
    protected IBrowserContext Context { get; private set; } = default!;
    protected IPage Page { get; private set; } = default!;

    public virtual async ValueTask InitializeAsync()
    {
        Context = await Fixture.NewContextAsync();

        // Record a Playwright trace for the whole test. It is only written to disk when the
        // test fails (see DisposeAsync) — CI then uploads it as an artifact, which is the
        // only way to see what the page actually looked like on a headless runner.
        await Context.Tracing.StartAsync(
            new TracingStartOptions
            {
                Screenshots = true,
                Snapshots = true,
                Sources = true,
            }
        );

        Page = await Context.NewPageAsync();
    }

    public virtual async ValueTask DisposeAsync()
    {
        await StopTracingAsync();
        await Context.DisposeAsync();
    }

    /// <summary>
    /// Saves the trace only for a failed test (kept small: passing runs discard theirs). The file
    /// lands in PLAYWRIGHT_TRACE_DIR — set by CI so the upload step can find it — named after the
    /// test so a multi-failure run yields one openable trace per failure.
    /// </summary>
    private async Task StopTracingAsync()
    {
        var failed = TestContext.Current.TestState?.Result == TestResult.Failed;
        if (!failed)
        {
            await Context.Tracing.StopAsync();
            return;
        }

        var dir = Environment.GetEnvironmentVariable("PLAYWRIGHT_TRACE_DIR");
        dir = string.IsNullOrWhiteSpace(dir) ? "playwright-traces" : dir;
        Directory.CreateDirectory(dir);

        var name = SanitizeFileName(TestContext.Current.Test?.TestDisplayName ?? "e2e-trace");
        await Context.Tracing.StopAsync(
            new TracingStopOptions { Path = Path.Combine(dir, $"{name}.zip") }
        );
    }

    private static string SanitizeFileName(string name)
    {
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            name = name.Replace(invalid, '_');
        }

        return name;
    }

    #region Account flows

    /// <summary>
    /// Clicks the submit control of an account page (login, registration). Located structurally via
    /// <see cref="PageExtensions.ClickSubmitAsync"/> rather than by caption, so these flows drive a page
    /// rendered in any language — a test may set <c>Accept-Language</c> on its browser context and still
    /// log in. Previously this clicked the literal strings "Log in" / "Register", which made a
    /// German-rendered <c>/Account/Login</c> hang until Playwright's 30s timeout.
    /// </summary>
    /// <remarks>
    /// Structural rather than a configurable caption because the rest of these helpers is already
    /// structural: the hard-coded <c>/Account/Login</c> path and the <c>[name='Email']</c> /
    /// <c>[name='NewPassword']</c> field lookups only work against
    /// <c>AndreGoepel.Marten.Identity.Blazor</c>'s pages anyway, and every account page it ships renders
    /// exactly one submit control. Adding caption options instead would have handed every consumer a knob
    /// to keep in sync with a translation file. Override this in a host whose account pages genuinely show
    /// more than one submit control, passing a <c>scope</c> to <c>ClickSubmitAsync</c>.
    /// </remarks>
    protected virtual Task ClickAccountSubmitAsync(IPage page) => page.ClickSubmitAsync();

    /// <summary>Logs the current page's session in via the real cookie-login flow.</summary>
    protected async Task LoginAsync(string email, string password, IPage? page = null)
    {
        page ??= Page;
        await page.GotoAsync("/Account/Login");
        await page.WaitForBlazorAsync();
        await page.FillFieldAsync("Email", email);
        await page.FillFieldAsync("Password", password);
        await ClickAndLeaveLoginAsync(page);
    }

    /// <summary>
    /// Submits the login form and waits to leave the login page. A click can land in the gap between the
    /// circuit connecting and the form's submit handler attaching — it is then silently lost — so
    /// the click is retried until the cookie middleware redirects away. Exact-path equality keeps a
    /// redirect to /Account/LoginWith2fa (which *contains* /Account/Login) counting as "left".
    /// </summary>
    private async Task ClickAndLeaveLoginAsync(IPage page)
    {
        for (var attempt = 0; ; attempt++)
        {
            await ClickAccountSubmitAsync(page);
            try
            {
                await page.WaitForURLAsync(
                    url =>
                        !new Uri(url).AbsolutePath.Equals(
                            "/Account/Login",
                            StringComparison.OrdinalIgnoreCase
                        ),
                    new PageWaitForURLOptions { Timeout = 5_000 }
                );
                return;
            }
            catch (TimeoutException) when (attempt < 5)
            {
                // Submit was lost before the handler attached — click again.
            }
        }
    }

    /// <summary>Ensures the root admin exists, then logs this page in as that admin.</summary>
    protected async Task LoginAsAdminAsync(IPage? page = null)
    {
        await Fixture.ProvisionAdminAsync();
        await LoginAsync(TestData.AdminEmail, TestData.DefaultPassword, page);
    }

    /// <summary>Registers a new user and returns the generated email; the account still needs confirmation.</summary>
    protected async Task<string> RegisterAsync(string? email = null, string? password = null)
    {
        email ??= TestData.NewEmail();
        password ??= TestData.DefaultPassword;

        await Page.GotoAsync("/Account/Register");
        await Page.WaitForBlazorAsync();
        await Page.FillFieldAsync("Email", email);
        await Page.FillFieldAsync("NewPassword", password);
        await Page.FillFieldAsync("ConfirmPassword", password);
        await ClickAccountSubmitAsync(Page);
        return email;
    }

    /// <summary>Reads the confirmation link the app's configured <see cref="IEmailLinkSource"/> captured and follows it to activate the account.</summary>
    protected async Task ConfirmEmailAsync(string email)
    {
        if (Fixture.Mail is null)
        {
            throw new InvalidOperationException(
                "No IEmailLinkSource is configured on this fixture. Set MailHogResourceName or "
                    + "MailSourceFactory on the E2EAppFixtureOptions passed to the fixture's constructor."
            );
        }

        var link = await Fixture.Mail.WaitForLinkAsync(email, "Account/ConfirmEmail");
        await Page.GotoAsync(link);
        await Page.WaitForBlazorAsync();
    }

    /// <summary>Signs the current session out through the app's sign-out endpoint.</summary>
    protected async Task LogoutAsync(IPage? page = null)
    {
        page ??= Page;
        await page.GotoAsync("/Account/SignOutAndRedirect");
    }

    #endregion
}
