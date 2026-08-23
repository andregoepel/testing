# AndreGoepel.Testing

Shared testing infrastructure for the AndreGoepel .NET ecosystem. Two packages:

| Package | For |
| --- | --- |
| `AndreGoepel.Testing.E2E` | Playwright + .NET Aspire end-to-end suites |
| `AndreGoepel.Testing.Bunit` | bUnit component tests: culture pinning, resx localizer, context setup |

## AndreGoepel.Testing.E2E

Shared Playwright + [.NET Aspire](https://learn.microsoft.com/dotnet/aspire) end-to-end testing infrastructure for the AndreGoepel ecosystem: boot the real Aspire app graph once per test collection, drive it with a shared Chromium browser, and get a fresh isolated context per test — with trace-capture-on-failure built in.

### Why

Three sibling repos (`marten-identity`, `finance-app`, `app-foundation`) each carried their own ~85-95% identical copy of this infrastructure. This package is the extracted, generic core; app-specific selectors and flows stay local to each repo as their own extension methods.

### Usage

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

### Design decisions worth knowing about

#### MailHog endpoint name: standardized on `"http"`

`finance-app`'s `AppHost.cs` named the MailHog HTTP API endpoint `"web"`; `app-foundation`'s named it `"http"`. `E2EAppFixtureOptions.MailHogEndpointName` defaults to `"http"` — the more semantically accurate name (it distinguishes the endpoint from MailHog's separate `"smtp"` endpoint, which `"web"` doesn't). Repos whose `AppHost.cs` still names it `"web"` need to either rename the endpoint or pass `MailHogEndpointName = "web"` explicitly until they do.

#### Admin provisioning button text: no default, must be supplied

`ProvisionAdminAsync`'s Setup-page button read "Create administrator" in `marten-identity`, "Create admin" in `app-foundation`, and "Create admin & complete setup" (matched via prefix) in `finance-app`. Rather than guess a shared wording and risk a silent no-op click in whichever repo doesn't match, `E2EAppFixtureOptions.ProvisionAdminButtonText` is `required` with no default. `ClickButtonAsync` already does a non-exact ("contains") match, so a stable prefix is enough.

This is the one caption-based click left, and it stays that way: the Setup page belongs to each *host app*, not to the identity package, so there is no shared markup to key off. It is also unaffected by a test's culture — `ProvisionAdminAsync` runs in its own throwaway context created by `NewContextAsync()`, which sets no locale, so the Setup page renders in the app's default language whatever the test's own context asked for.

#### The account flows submit structurally, not by caption

`LoginAsync`/`LoginAsAdminAsync` and `RegisterAsync` used to click the literal strings `"Log in"` and `"Register"`. That made them unusable against a page rendered in another language: a `finance-app` test that set `Accept-Language: de` on its context got a 30-second `TimeoutException` waiting for `GetByRole(AriaRole.Button, new() { Name = "Log in" })`, and had to log in under English and switch culture afterwards.

They now go through `ClickAccountSubmitAsync`, which locates the control by `type="submit"` (`PageExtensions.ClickSubmitAsync`). Two things about the shape of that selector:

- **No new configuration knob.** These helpers are already structural everywhere else — the hard-coded `/Account/Login` path and the `[name='Email']` / `[name='NewPassword']` field lookups only work against `AndreGoepel.Marten.Identity.Blazor`'s pages regardless. Every account page that package ships renders exactly one submit control, so nothing needs configuring. `LoginButtonText`/`RegisterButtonText` options would only have handed every consumer a knob to keep in sync with a translation file.
- **The selector is page-wide, not `form button[type=submit]`.** `Login.razor` renders its submit button *outside* the `<form id="login-form">` that `LoginForm.razor` produces, associating the two with the HTML `form="login-form"` attribute so the button can sit beside the passkey button in the action bar. A descendant selector matches nothing there. Page-wide is unambiguous anyway: the passkey button is `type="button"`, the hidden sign-in-handoff form has no button, and the login layout's language switcher renders plain anchors. `ClickSubmitAsync(scope)` takes an optional CSS scope, and `ClickAccountSubmitAsync` is `virtual`, for a host whose account pages ever do show two.

#### Not using generics for the Aspire entry point

Rather than `E2EAppFixture<TEntryPoint>`, the fixture takes a `Func<string[], Task<IDistributedApplicationTestingBuilder>> CreateAppHostBuilder` in its options. This keeps `E2ETestBase<TFixture>` constrainable to a single non-generic `E2EAppFixture` base type regardless of which `Projects.*` type each host's AppHost generates, and avoids every consuming repo having to spell out `E2EAppFixtureBase<Projects.AndreGoepel_FinanceApp_AppHost>` as a base-class type argument.

#### Trace capture is unconditional, not opt-in

`E2ETestBase<TFixture>` always starts a Playwright trace per test and only writes it to disk when the test fails, using `PLAYWRIGHT_TRACE_DIR` (falling back to `playwright-traces`). `marten-identity` already had this; `finance-app` and `app-foundation` didn't, which meant a CI failure on a headless runner in those two repos was undebuggable. This package makes it a first-class, always-on feature of the base class.

## AndreGoepel.Testing.Bunit

### Why

Nineteen copies of the same culture save/restore across four repos (nine of them byte-identical in `marten-identity` alone), and `JSInterop.Mode = Loose` written out 49 times across five repos.

### Usage

```csharp
using AndreGoepel.Testing.Bunit;

public sealed class MyComponentTests : BunitContext
{
    public MyComponentTests() => this.UseLooseJSInterop().UseLocalization();

    [Fact]
    public void Render_German_ShowsGermanCopy()
    {
        using var culture = CultureScope.UiOnly("de");

        var cut = Render<MyComponent>();

        Assert.Contains("Willkommen!", cut.Markup);
    }
}
```

For code that composes localized strings outside a component (email bodies, PDFs):

```csharp
var localizer = TestLocalizer.Create<Strings>();
Assert.Equal("Neues Formular", localizer["NewResponse.Template"]);
```

### Design decisions worth knowing about

#### `CultureScope` restores both cultures, and `GetCultureInfo` over `new CultureInfo`

Nine byte-identical copies in `marten-identity` and a drifting trio in `customer-portal` disagreed on whether to cache the looked-up culture (`CultureInfo.GetCultureInfo`) or allocate a fresh one (`new CultureInfo`) — the cached form is the majority shape and is what `CultureScope` uses. Previous values are captured in the constructor, restored on `Dispose`.

#### `UiOnly` is a separate factory, not a flag

`CultureScope.UiOnly(string)` pins only `CurrentUICulture`, leaving `CurrentCulture` (number/date formatting) untouched — what a pure `IStringLocalizer` lookup test wants. A static factory reads as intent at the call site (`using var _ = CultureScope.UiOnly("de");`) better than a bool parameter would, and maps `app-foundation`'s six UI-only `try/finally` sites onto it directly.

#### Setup is extension methods, not a base class

`marten-identity`'s Blazor page tests must run with localization **not** registered — the library ships routable pages that consuming apps render in their own tests, and a page must never hard-require `IStringLocalizer`. A shared base class that called `AddLocalization()` in its constructor would silently break that. `UseLooseJSInterop()`/`UseLocalization()` are opt-in extension methods on `BunitContext` instead, so registration is always an explicit choice at the call site.

#### Radzen is deliberately absent

Every current consumer already references `Radzen.Blazor`, so a `UseRadzen()` extension would cost no consumer a new dependency — but it would make a general-purpose testing package depend on a specific UI component library, for a single line (`Services.AddRadzenComponents()`) with zero drift across call sites. Omitted from v1; revisit as `AndreGoepel.Testing.Bunit.Radzen` if the duplication keeps spreading.

#### The stub `HttpMessageHandler`s are not here

`finance-app` has six near-identical fake `HttpMessageHandler` classes, but they live in exactly one repo and are not bUnit tests (they test HTTP client wiring, not components) — shipping them in a package named `*.Bunit` would be a category error. That consolidation is a `finance-app`-local fix.
