# AndreGoepel.Testing.E2E

Shared Playwright + [.NET Aspire](https://learn.microsoft.com/dotnet/aspire) end-to-end testing infrastructure for the AndreGoepel ecosystem: boot the real Aspire app graph once per test collection, drive it with a shared Chromium browser, and get a fresh isolated context per test — with trace-capture-on-failure built in.

## Why

Three sibling repos (`marten-identity`, `finance-app`, `app-foundation`) each carried their own ~85-95% identical copy of this infrastructure. This package is the extracted, generic core; app-specific selectors and flows stay local to each repo as their own extension methods.

## Usage

Reference the package from your `*.E2ETests` project, then declare a repo-specific fixture and collection:

```csharp
public sealed class E2EAppFixture()
    : AndreGoepel.Testing.E2E.E2EAppFixture(
        new E2EAppFixtureOptions
        {
            CreateAppHostBuilder = args =>
                DistributedApplicationTestingBuilder.CreateAsync<Projects.AndreGoepel_FinanceApp_AppHost>(args),
            WebResourceName = "financeapp",
            MailHogResourceName = "mailhog",
            ProvisionAdminButtonText = "Create admin",
            AppHostArguments = ["E2E=true", "Parameters:database-password=E2e-Db-Passw0rd!"],
        }
    );

[CollectionDefinition(E2ECollectionDefaults.Name)]
public sealed class E2ECollection : ICollectionFixture<E2EAppFixture>;
```

Then write tests against `E2ETestBase<E2EAppFixture>`:

```csharp
public sealed class LoginTests(E2EAppFixture fixture) : E2ETestBase<E2EAppFixture>(fixture)
{
    [Fact]
    public async Task Login_ValidCredentials_RedirectsToDashboard()
    {
        var email = await RegisterAsync();
        await ConfirmEmailAsync(email);

        await LoginAsync(email, TestData.DefaultPassword);

        await Page.AssertOnPathAsync("/Dashboard");
    }
}
```

## Design decisions worth knowing about

### MailHog endpoint name: standardized on `"http"`

`finance-app`'s `AppHost.cs` named the MailHog HTTP API endpoint `"web"`; `app-foundation`'s named it `"http"`. `E2EAppFixtureOptions.MailHogEndpointName` defaults to `"http"` — the more semantically accurate name (it distinguishes the endpoint from MailHog's separate `"smtp"` endpoint, which `"web"` doesn't). Repos whose `AppHost.cs` still names it `"web"` need to either rename the endpoint or pass `MailHogEndpointName = "web"` explicitly until they do.

### Admin provisioning button text: no default, must be supplied

`ProvisionAdminAsync`'s Setup-page button read "Create administrator" in `marten-identity`, "Create admin" in `app-foundation`, and "Create admin & complete setup" (matched via prefix) in `finance-app`. Rather than guess a shared wording and risk a silent no-op click in whichever repo doesn't match, `E2EAppFixtureOptions.ProvisionAdminButtonText` is `required` with no default. `ClickButtonAsync` already does a non-exact ("contains") match, so a stable prefix is enough.

### Not using generics for the Aspire entry point

Rather than `E2EAppFixture<TEntryPoint>`, the fixture takes a
`Func<string[], Task<IDistributedApplicationTestingBuilder>> CreateAppHostBuilder` in its options. This keeps `E2ETestBase<TFixture>` constrainable to a single non-generic `E2EAppFixture` base type regardless of which `Projects.*` type each host's AppHost generates, and avoids every consuming repo having to spell out `E2EAppFixtureBase<Projects.AndreGoepel_FinanceApp_AppHost>` as a base-class type argument.

### Trace capture is unconditional, not opt-in

`E2ETestBase<TFixture>` always starts a Playwright trace per test and only writes it to disk when the test fails, using `PLAYWRIGHT_TRACE_DIR` (falling back to `playwright-traces`). `marten-identity` already had this; `finance-app` and `app-foundation` didn't, which meant a CI failure on a headless runner in those two repos was undebuggable. This package makes it a first-class, always-on feature of the base class.
