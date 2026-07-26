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
    public static Task ClickButtonAsync(this IPage page, string text) =>
        page.GetByRole(AriaRole.Button, new() { Name = text, Exact = false }).First.ClickAsync();

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
