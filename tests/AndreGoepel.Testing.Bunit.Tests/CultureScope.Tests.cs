namespace AndreGoepel.Testing.Bunit.Tests;

public sealed class CultureScopeTests
{
    [Fact]
    public void Ctor_WithCulture_SetsBothCurrentAndUiCulture()
    {
        // Arrange / Act
        using var scope = new CultureScope("de");

        // Assert
        Assert.Equal("de", CultureInfo.CurrentCulture.Name);
        Assert.Equal("de", CultureInfo.CurrentUICulture.Name);
    }

    [Fact]
    public void Dispose_AfterScope_RestoresBothCultures()
    {
        // Arrange
        var originalCulture = CultureInfo.CurrentCulture;
        var originalUiCulture = CultureInfo.CurrentUICulture;

        // Act
        using (new CultureScope("fr"))
        {
            // Body intentionally empty — the scope's own dispose is under test.
        }

        // Assert
        Assert.Equal(originalCulture, CultureInfo.CurrentCulture);
        Assert.Equal(originalUiCulture, CultureInfo.CurrentUICulture);
    }

    [Fact]
    public void Dispose_Nested_RestoresToTheEnclosingScope()
    {
        // Arrange / Act / Assert
        using (new CultureScope("en"))
        {
            Assert.Equal("en", CultureInfo.CurrentCulture.Name);
            using (new CultureScope("fr"))
            {
                Assert.Equal("fr", CultureInfo.CurrentCulture.Name);
                using (new CultureScope("de"))
                {
                    Assert.Equal("de", CultureInfo.CurrentCulture.Name);
                }
                Assert.Equal("fr", CultureInfo.CurrentCulture.Name);
            }
            Assert.Equal("en", CultureInfo.CurrentCulture.Name);
        }
    }

    [Fact]
    public void UiOnly_WithCulture_LeavesCurrentCultureUntouched()
    {
        // Arrange
        var originalCulture = CultureInfo.CurrentCulture;

        // Act
        using var scope = CultureScope.UiOnly("de");

        // Assert
        Assert.Equal(originalCulture, CultureInfo.CurrentCulture);
        Assert.Equal("de", CultureInfo.CurrentUICulture.Name);
    }

    [Fact]
    public void UiOnly_Dispose_RestoresUiCultureOnly()
    {
        // Arrange
        var originalUiCulture = CultureInfo.CurrentUICulture;

        // Act
        using (CultureScope.UiOnly("de"))
        {
            // Body intentionally empty — the scope's own dispose is under test.
        }

        // Assert
        Assert.Equal(originalUiCulture, CultureInfo.CurrentUICulture);
    }

    [Fact]
    public void Ctor_WithUnknownCulture_Throws()
    {
        // Pins the GetCultureInfo behaviour so a future switch to `new CultureInfo` — which
        // tolerates names GetCultureInfo rejects — is caught by a failing test, not silently.
        // A hyphen-only string is not a well-formed BCP-47 tag under any parser, so it throws
        // regardless of ICU's leniency with other malformed-looking-but-parseable strings.
        // Arrange / Act / Assert
        Assert.Throws<CultureNotFoundException>(() => new CultureScope("-"));
    }
}
