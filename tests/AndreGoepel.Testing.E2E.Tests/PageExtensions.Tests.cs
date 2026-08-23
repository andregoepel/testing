using AngleSharp.Dom;
using AngleSharp.Html.Parser;

namespace AndreGoepel.Testing.E2E.Tests;

/// <summary>
/// Covers <see cref="PageExtensions.SubmitControlSelector"/> — the selector that makes the account flows
/// culture-agnostic (issue #12).
///
/// What this can and cannot prove: this repo cannot render a real, non-English
/// <c>/Account/Login</c>. Doing so needs <c>AndreGoepel.Marten.Identity.Blazor</c>, and that package is a
/// *consumer* of this one — referencing it here would point the ecosystem's dependency arrow backwards.
/// A live browser is equally out of reach: these are pure unit tests and CI installs no Playwright
/// browsers. So the real integration test remains each consuming repo's own <c>*.E2ETests</c> suite, as
/// the repo's CLAUDE.md already states.
///
/// What *is* achievable, and is what these tests do, is to pin the selector's behaviour against parsed
/// HTML whose structure is transcribed from the identity package's account pages — in particular the two
/// traits that decided the selector's shape:
///   * <c>Login.razor</c> puts its submit button *outside* <c>LoginForm.razor</c>'s
///     <c>&lt;form id="login-form"&gt;</c>, associating it with <c>form="login-form"</c>, so the tempting
///     <c>form button[type=submit]</c> matches nothing there;
///   * <c>PasskeySubmit.razor</c> renders <c>type="button"</c>, so it must not be mistaken for the submit.
/// Captions in the fixtures are German precisely because the selector must not care.
/// </summary>
public class PageExtensionsTests
{
    // Structure of /Account/Login as rendered by AndreGoepel.Marten.Identity.Blazor: the form comes from
    // the nested LoginForm component, the sign-in handoff form is hidden and button-less, the language
    // switcher is plain anchors, and the action bar holds the detached submit plus the passkey button.
    private const string LoginPageMarkup = """
        <div style="position: fixed; top: 1rem; right: 1rem;">
            <a class="ag-lang-btn ag-active" href="/?culture=de">DE</a>
            <a class="ag-lang-btn" href="/?culture=en">EN</a>
        </div>
        <form id="login-form" class="rz-template-form">
            <input name="Email" type="text" />
            <input name="Password" type="password" />
            <input name="RememberMe" type="checkbox" />
        </form>
        <form method="post" action="/login" style="display:none" aria-hidden="true">
            <input type="hidden" name="token" value="opaque" />
        </form>
        <div class="ag-login-actions">
            <button type="submit" form="login-form" class="rz-button">Anmelden</button>
            <button type="button" class="rz-button">Mit Passkey anmelden</button>
        </div>
        """;

    // Structure of /Account/Register: submit button inside the form, plus the same hidden handoff form.
    private const string RegisterPageMarkup = """
        <form class="rz-template-form">
            <input name="Email" type="text" />
            <input name="NewPassword" type="password" />
            <input name="ConfirmPassword" type="password" />
            <div class="ag-login-actions">
                <button type="submit" class="rz-button">Registrieren</button>
            </div>
        </form>
        <form method="post" action="/login" style="display:none" aria-hidden="true">
            <input type="hidden" name="token" value="opaque" />
        </form>
        """;

    [Fact]
    public void SubmitControlSelector_LoginPageMarkup_MatchesOnlyTheSubmitButton()
    {
        // Arrange
        var document = Parse(LoginPageMarkup);

        // Act
        var matches = document.QuerySelectorAll(PageExtensions.SubmitControlSelector);

        // Assert
        var only = Assert.Single(matches);
        Assert.Equal("Anmelden", only.TextContent);
        Assert.Equal("login-form", only.GetAttribute("form"));
    }

    [Fact]
    public void SubmitControlSelector_RegisterPageMarkup_MatchesOnlyTheSubmitButton()
    {
        // Arrange
        var document = Parse(RegisterPageMarkup);

        // Act
        var matches = document.QuerySelectorAll(PageExtensions.SubmitControlSelector);

        // Assert
        var only = Assert.Single(matches);
        Assert.Equal("Registrieren", only.TextContent);
    }

    [Fact]
    public void SubmitControlSelector_LoginPageMarkup_DoesNotMatchThePasskeyButton()
    {
        // Arrange
        var document = Parse(LoginPageMarkup);

        // Act
        var matches = document.QuerySelectorAll(PageExtensions.SubmitControlSelector);

        // Assert — PasskeySubmit.razor renders type="button", so it is structurally distinguishable.
        Assert.DoesNotContain(matches, element => element.TextContent.Contains("Passkey"));
    }

    /// <summary>
    /// The regression guard for the design decision. Scoping the selector under <c>form</c> reads as the
    /// obvious tightening, but the identity login page's submit button is a sibling of the form it
    /// submits, wired up by its <c>form="login-form"</c> attribute — so the scoped variant finds nothing
    /// and the login helper would break exactly the way the English caption did.
    /// </summary>
    [Fact]
    public void FormScopedSubmitSelector_LoginPageMarkup_MatchesNothing()
    {
        // Arrange
        var document = Parse(LoginPageMarkup);

        // Act
        var matches = QueryScoped(document, "form");

        // Assert
        Assert.Empty(matches);
    }

    [Theory]
    [InlineData("Log in")]
    [InlineData("Anmelden")]
    [InlineData("Se connecter")]
    [InlineData("ログイン")]
    public void SubmitControlSelector_AnyCaption_StillMatchesTheSubmitButton(string caption)
    {
        // Arrange — the caption is the only thing that varies; this is issue #12 in one assertion.
        var document = Parse(
            $"""<form id="login-form"></form><button type="submit" form="login-form">{caption}</button>"""
        );

        // Act
        var matches = document.QuerySelectorAll(PageExtensions.SubmitControlSelector);

        // Assert
        var only = Assert.Single(matches);
        Assert.Equal(caption, only.TextContent);
    }

    [Fact]
    public void SubmitControlSelector_LegacyInputSubmit_IsMatchedToo()
    {
        // Arrange — a statically rendered page (or a non-Radzen host) may still submit via <input>.
        var document = Parse("""<form><input type="submit" value="Absenden" /></form>""");

        // Act
        var matches = document.QuerySelectorAll(PageExtensions.SubmitControlSelector);

        // Assert
        var only = Assert.Single(matches);
        Assert.Equal("input", only.LocalName);
    }

    [Fact]
    public void ScopedSubmitSelector_TwoFormsOnOnePage_SelectsTheScopedOne()
    {
        // Arrange — the shape the ClickSubmitAsync(scope) escape hatch exists for.
        var document = Parse(
            """
            <div id="search"><form><button type="submit">Suchen</button></form></div>
            <div id="account"><form><button type="submit">Anmelden</button></form></div>
            """
        );

        // Act
        var matches = QueryScoped(document, "#account");

        // Assert
        var only = Assert.Single(matches);
        Assert.Equal("Anmelden", only.TextContent);
    }

    private static IDocument Parse(string bodyMarkup) =>
        new HtmlParser().ParseDocument($"<!doctype html><html><body>{bodyMarkup}</body></html>");

    /// <summary>
    /// Mirrors <c>page.Locator(scope).Locator(SubmitControlSelector)</c>: matches the scope first, then the
    /// submit selector *within* each match. Not the same as string-concatenating the two —
    /// <c>SubmitControlSelector</c> is a selector list, so <c>"#account " + selector</c> would leave its
    /// second term (<c>input[type='submit']</c>) unscoped.
    /// </summary>
    private static List<IElement> QueryScoped(IDocument document, string scope) =>
        [
            .. document
                .QuerySelectorAll(scope)
                .SelectMany(element =>
                    element.QuerySelectorAll(PageExtensions.SubmitControlSelector)
                ),
        ];
}
