namespace AndreGoepel.Testing.Bunit;

/// <summary>
/// Opt-in, chainable bUnit setup steps. Deliberately extension methods rather than a base
/// class: AndreGoepel.Marten.Identity.Blazor's page tests must run with localization NOT
/// registered, so registration can never be implicit.
/// </summary>
public static class BunitContextExtensions
{
    /// <summary>Loose JS interop, so components that call into JS during render don't throw.</summary>
    public static TContext UseLooseJSInterop<TContext>(this TContext context)
        where TContext : BunitContext
    {
        context.JSInterop.Mode = JSRuntimeMode.Loose;
        return context;
    }

    /// <summary>
    /// Registers Microsoft.Extensions.Localization so <see cref="IStringLocalizer{T}" />
    /// resolves against the component assembly's embedded .resx.
    /// </summary>
    public static TContext UseLocalization<TContext>(this TContext context)
        where TContext : BunitContext
    {
        context.Services.AddLocalization();
        return context;
    }
}
