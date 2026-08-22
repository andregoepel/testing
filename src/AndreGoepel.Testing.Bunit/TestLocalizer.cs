namespace AndreGoepel.Testing.Bunit;

/// <summary>
/// Builds a real, resx-backed <see cref="IStringLocalizer{T}" /> from a throwaway service
/// provider, for testing code that composes localized strings outside a component (email
/// bodies, PDFs).
/// </summary>
public static class TestLocalizer
{
    public static IStringLocalizer<T> Create<T>()
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization();
        return services.BuildServiceProvider().GetRequiredService<IStringLocalizer<T>>();
    }

    public static IStringLocalizer Create(Type resourceSource)
    {
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddLocalization();
        var factory = services.BuildServiceProvider().GetRequiredService<IStringLocalizerFactory>();
        return factory.Create(resourceSource);
    }
}
