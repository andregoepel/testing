namespace AndreGoepel.Testing.E2E;

/// <summary>
/// The verbatim-identical core of Blazor-Server E2E page helpers, extracted from three sibling repos.
/// App-specific selectors (Radzen grid filters, design-system <c>FormField</c> locators, file upload
/// helpers, ...) are deliberately not here — add them as your own extension methods on <see cref="IPage"/>
/// in your own repo instead of forking this class.
/// </summary>
public static class PageExtensions
{
    /// <summary>
    /// Waits until the interactive Server circuit is live. Forms submit through Blazor event handlers,
    /// so clicking before the circuit connects silently does nothing — this prevents flakes.
    /// </summary>
    public static async Task WaitForBlazorAsync(this IPage page)
    {
        await page.WaitForLoadStateAsync(LoadState.NetworkIdle);
        await page.WaitForFunctionAsync(
            "() => window.Blazor !== undefined && !document.querySelector('#components-reconnect-modal.components-reconnect-show')"
        );
    }

    /// <summary>Navigates to a relative path (resolved against the fixture base URL) and waits for interactivity.</summary>
    public static async Task GotoAsync(this IPage page, string path)
    {
        await page.GotoAsync(path);
        await page.WaitForBlazorAsync();
    }

    /// <summary>Fills an input rendered with <c>name="..."</c>.</summary>
    public static Task FillFieldAsync(this IPage page, string name, string value) =>
        page.FillAsync($"[name='{name}']", value);

    /// <summary>Clicks a button by its visible text (non-exact match, so a stable prefix is enough).</summary>
    /// <remarks>
    /// Caption-based, so it only works on a page rendered in the language the caller spelled out. For a
    /// form's primary action prefer <see cref="ClickSubmitAsync"/>, which is culture-agnostic; keep this
    /// one for secondary controls that nothing but their caption distinguishes.
    /// </remarks>
    public static Task ClickButtonAsync(this IPage page, string text) =>
        page.GetByRole(AriaRole.Button, new() { Name = text, Exact = false }).First.ClickAsync();

    /// <summary>
    /// CSS matching a form's submit control regardless of its caption.
    /// </summary>
    /// <remarks>
    /// Deliberately <b>not</b> scoped as <c>form button[type='submit']</c>. HTML lets a submit control sit
    /// outside the form it submits by carrying a <c>form="..."</c> attribute, and
    /// <c>AndreGoepel.Marten.Identity.Blazor</c>'s <c>/Account/Login</c> does exactly that: the form is
    /// rendered by a nested <c>LoginForm</c> component as <c>&lt;form id="login-form"&gt;</c>, while the
    /// submit button lives further down the page in the action bar with <c>form="login-form"</c> so it can
    /// sit beside the passkey button. A descendant selector matches nothing at all there.
    /// </remarks>
    internal const string SubmitControlSelector = "button[type='submit'], input[type='submit']";

    /// <summary>
    /// Clicks the submit control of the form on screen, found by its <c>type="submit"</c> rather than by a
    /// caption — so the same call drives the page whatever language it renders in.
    /// </summary>
    /// <param name="scope">
    /// Optional CSS narrowing the search to one region when a page shows several forms. Leave it unset on
    /// the identity package's account pages: each renders exactly one submit control (the passkey button on
    /// <c>/Account/Login</c> is <c>type="button"</c>, the hidden sign-in-handoff form carries no button, and
    /// the login layout's language switcher renders plain anchors), and scoping to the <c>form</c> element
    /// would break the login page for the reason given on <see cref="SubmitControlSelector"/>.
    /// </param>
    public static Task ClickSubmitAsync(this IPage page, string? scope = null) =>
        (
            scope is null
                ? page.Locator(SubmitControlSelector)
                : page.Locator(scope).Locator(SubmitControlSelector)
        ).First.ClickAsync();

    /// <summary>Clicks a link by its visible text.</summary>
    public static Task ClickLinkAsync(this IPage page, string text) =>
        page.GetByRole(AriaRole.Link, new() { Name = text, Exact = false }).First.ClickAsync();

    /// <summary>Asserts the current URL path matches (ignoring query string and trailing slash).</summary>
    public static async Task AssertOnPathAsync(this IPage page, string expectedPath)
    {
        try
        {
            await page.WaitForURLAsync(
                url =>
                    NormalizePath(url)
                        .Contains(expectedPath.Trim('/'), StringComparison.OrdinalIgnoreCase),
                new PageWaitForURLOptions { Timeout = 15_000 }
            );
        }
        catch (TimeoutException)
        {
            throw new TimeoutException(
                $"Expected path to contain '{expectedPath}' but was '{page.Url}'."
            );
        }
    }

    private static string NormalizePath(string url) => new Uri(url).AbsolutePath.Trim('/');
}
