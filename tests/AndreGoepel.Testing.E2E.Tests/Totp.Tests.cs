using System.Text.RegularExpressions;

namespace AndreGoepel.Testing.E2E.Tests;

public partial class TotpTests
{
    [Fact]
    public void NormalizeSharedKey_KeyHasSpacesAndLowercase_StripsSpacesAndUpperCases()
    {
        // Arrange
        var displayed = "jbsw y3dp ehpk 3pxp";

        // Act
        var normalized = Totp.NormalizeSharedKey(displayed);

        // Assert
        Assert.Equal("JBSWY3DPEHPK3PXP", normalized);
    }

    [Fact]
    public void NormalizeSharedKey_KeyAlreadyNormalized_IsUnchanged()
    {
        // Arrange
        var key = "JBSWY3DPEHPK3PXP";

        // Act
        var normalized = Totp.NormalizeSharedKey(key);

        // Assert
        Assert.Equal(key, normalized);
    }

    [Fact]
    public void Compute_ValidSharedKey_ReturnsSixDigitCode()
    {
        // Arrange
        var sharedKey = "jbsw y3dp ehpk 3pxp";

        // Act
        var code = Totp.Compute(sharedKey);

        // Assert
        Assert.Matches(SixDigitsRegex(), code);
    }

    [Fact]
    public void Compute_SpacedLowercaseKeyAndNormalizedKey_ProduceSameCode()
    {
        // Arrange
        var spaced = "jbsw y3dp ehpk 3pxp";
        var normalized = Totp.NormalizeSharedKey(spaced);

        // Act
        var fromSpaced = Totp.Compute(spaced);
        var fromNormalized = Totp.Compute(normalized);

        // Assert
        Assert.Equal(fromNormalized, fromSpaced);
    }

    [GeneratedRegex(@"^\d{6}$")]
    private static partial Regex SixDigitsRegex();
}
