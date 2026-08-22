using System.Globalization;

namespace AndreGoepel.Testing.Bunit;

/// <summary>
/// Pins <see cref="CultureInfo.CurrentCulture" /> and <see cref="CultureInfo.CurrentUICulture" />
/// for the lifetime of the scope, restoring both on dispose. Thread-pool threads are reused
/// across tests, so an unrestored culture makes an unrelated test's result depend on run order.
/// </summary>
public sealed class CultureScope : IDisposable
{
    private readonly CultureInfo _previousCulture;
    private readonly CultureInfo _previousUiCulture;
    private readonly bool _restoreCulture;

    /// <summary>Pins both <see cref="CultureInfo.CurrentCulture" /> and <see cref="CultureInfo.CurrentUICulture" />.</summary>
    public CultureScope(string culture)
        : this(culture, restoreCulture: true) { }

    /// <summary>
    /// Pins <see cref="CultureInfo.CurrentUICulture" /> only, leaving formatting culture
    /// untouched — what a pure <see cref="IStringLocalizer" /> lookup test wants.
    /// </summary>
    public static CultureScope UiOnly(string culture) => new(culture, restoreCulture: false);

    private CultureScope(string culture, bool restoreCulture)
    {
        _previousCulture = CultureInfo.CurrentCulture;
        _previousUiCulture = CultureInfo.CurrentUICulture;
        _restoreCulture = restoreCulture;

        var info = CultureInfo.GetCultureInfo(culture);
        if (restoreCulture)
        {
            CultureInfo.CurrentCulture = info;
        }
        CultureInfo.CurrentUICulture = info;
    }

    public void Dispose()
    {
        if (_restoreCulture)
        {
            CultureInfo.CurrentCulture = _previousCulture;
        }
        CultureInfo.CurrentUICulture = _previousUiCulture;
    }
}
