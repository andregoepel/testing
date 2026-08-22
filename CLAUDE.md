# testing

Shared test infrastructure for the AndreGoepel ecosystem. Two packages:
Playwright + .NET Aspire end-to-end testing infrastructure, extracted from
the near-identical copies that `marten-identity`, `finance-app`, and
`app-foundation` each carried in their own `tests/*.E2ETests/Infrastructure/`
folders; and shared bUnit component-test helpers (culture pinning, a
resx-backed test localizer, opt-in context setup), extracted from
`CultureScope`/`JSInterop.Mode = Loose` duplication across `marten-identity`,
`customer-portal`, `app-foundation`, and `design-blazor`.

## Solution Projects
- `AndreGoepel.Testing.E2E` — the packable NuGet library
- `AndreGoepel.Testing.E2E.Tests` — pure unit tests for the library's
  non-I/O logic (MailHog message decoding, TOTP code generation)
- `AndreGoepel.Testing.Bunit` — the packable NuGet library: `CultureScope`,
  `TestLocalizer`, `BunitContextExtensions`
- `AndreGoepel.Testing.Bunit.Tests` — pure unit tests for the library

Consumed by `marten-identity`, `finance-app`, `app-foundation`, and any
future host app with an Aspire + Playwright E2E suite. Plain class library —
no ASP.NET Core dependency of its own, but it does depend on
`Aspire.Hosting.Testing`, `Microsoft.Playwright`, `Otp.NET`, and `xunit.v3`
as regular (non-test-SDK) package references, since those are the types the
library's own public surface is built from.

## What's in AndreGoepel.Testing.E2E vs. what stays per-repo
- `E2EAppFixture` boots the Aspire app graph once per test collection,
  launches one shared Chromium browser, and exposes `ProvisionAdminAsync`
  for the one-time `/Setup` flow. Configured per host via
  `E2EAppFixtureOptions` — see its XML docs for the two standardization
  decisions baked in (MailHog endpoint name, admin button text). A host
  subclasses it with a parameterless constructor so xUnit's
  `ICollectionFixture<T>` can construct it.
- `E2ETestBase<TFixture>` gives every test a fresh browser context per
  test, the shared account flows (`LoginAsync`, `RegisterAsync`,
  `ConfirmEmailAsync`, `LogoutAsync`, `LoginAsAdminAsync`), and
  trace-capture-on-failure — unconditionally, not opt-in. A failed test's
  trace lands in `PLAYWRIGHT_TRACE_DIR` (defaults to `playwright-traces`)
  so CI can upload it as an artifact.
- `PageExtensions` in the package is only the verbatim-identical core
  (`WaitForBlazorAsync`, `GotoAsync`, `FillFieldAsync`, `ClickButtonAsync`,
  `ClickLinkAsync`, `AssertOnPathAsync`). App-specific selectors (Radzen
  grid filters, design-system `FormField` locators, file upload helpers,
  ...) stay local to each consuming repo as their own extension methods in
  the same namespace.
- `MailHogClient`, `TestData`, `Totp`, `VirtualAuthenticator` are
  consolidated as-is — they were identical or near-identical across all
  three source repos.
- Anything that encodes one app's domain (e.g. `app-foundation`'s
  `EnsureEmailConfiguredAsync`, which drives its own Email Settings admin
  page) stays local to that repo.

## What's in AndreGoepel.Testing.Bunit vs. what stays per-repo
- `CultureScope` and `CultureScope.UiOnly` cover the culture-pinning
  duplication (19 sites, 4 repos). `TestLocalizer.Create<T>()` /
  `Create(Type)` cover the resx-backed `IStringLocalizer` factory
  duplicated inside `customer-portal`. `BunitContextExtensions` covers
  `UseLooseJSInterop()` and `UseLocalization()` — opt-in setup, never a
  base class.
- **Hard constraint: localization must never be implicitly registered.**
  `marten-identity`'s Blazor page tests derive from a plain `BunitContext`
  on purpose — the library ships routable pages that consuming apps
  render in their own tests, so a page must never hard-require
  `IStringLocalizer`. A future shared base class or convenience wrapper
  that calls `AddLocalization()` in its constructor would silently break
  that premise. `UseLocalization()` stays an explicit, opt-in call at
  every call site. The guard test is
  `UseLocalization_NotCalled_LeavesStringLocalizerUnregistered`.
- **Hard constraint: no `NSubstitute` dependency.** `customer-portal` — the
  package's largest bUnit consumer — does not reference NSubstitute. Any
  future addition to this package (e.g. an `AuthenticatedUserContext`-style
  helper) that needs a mocking library would force a new dependency onto
  every consumer; such helpers belong in a separate package instead (e.g.
  `AndreGoepel.Testing.Bunit.Identity`).
- Explicitly **not** in this package: `Services.AddRadzenComponents()` (a
  UI-framework dependency a general testing package shouldn't carry — see
  the README), a shared `LocalizedBunitContext` base class (the two
  existing per-repo bases pull in incompatible directions), and the stub
  `HttpMessageHandler` family in `finance-app` (single-repo, and not a
  bUnit concern at all).

## `[CollectionDefinition]` pattern
The package does not — and cannot — declare `[CollectionDefinition("e2e")]`
itself: `ICollectionFixture<T>` needs the consuming repo's own concrete
fixture subclass. Each repo keeps a two-line
`[CollectionDefinition(E2ECollectionDefaults.Name)] public sealed class
E2ECollection : ICollectionFixture<E2EAppFixture>` next to its fixture.

## Library Rules
- Public API surface is deliberately small: `E2EAppFixture`,
  `E2EAppFixtureOptions`, `E2ETestBase<TFixture>`, `E2ECollectionDefaults`,
  `PageExtensions`, `MailHogClient`, `IEmailLinkSource`, `TestData`,
  `Totp`, `VirtualAuthenticator`. Everything else stays `internal`.
- `IEmailLinkSource` is the seam that lets a host swap MailHog for
  something else (e.g. `marten-identity`'s sample app captures outbound
  mail itself rather than running a MailHog container) — implement it and
  pass a factory via `E2EAppFixtureOptions.MailSourceFactory`.

## Testing
- `…Testing.E2E.Tests` — pure unit tests, no I/O, no Docker. Covers
  `MailHogClient`'s message-body decoding (quoted-printable, MIME parts,
  link extraction) and `Totp`'s key normalization — the only genuinely
  unit-testable logic in a package that's otherwise Playwright/Aspire
  orchestration. `internal` members it exercises are exposed via
  `InternalsVisibleTo`.
- No E2E-of-the-E2E-helpers project: this package has no app of its own to
  boot, so there's nothing for `Aspire.Hosting.Testing` to drive here.
  Each *consuming* repo's own `*.E2ETests` suite is the real integration
  test for this package.
- `…Testing.Bunit.Tests` — pure unit tests, no I/O, no Docker. Covers
  `CultureScope` (including nested-scope unwinding and the
  `GetCultureInfo`-vs-`new CultureInfo` behaviour), `TestLocalizer`
  (against a tiny `TestSupport/SampleStrings.resx` + `.de.resx`), and
  `BunitContextExtensions` (including the opt-in-registration guard test
  above). `TestSupport/JsCallingComponent.razor` is a 5-line component
  that calls into JS on render, used to prove `UseLooseJSInterop()`
  actually prevents the throw bUnit's strict JSInterop mode would
  otherwise produce.
