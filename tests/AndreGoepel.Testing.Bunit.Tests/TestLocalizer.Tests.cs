namespace AndreGoepel.Testing.Bunit.Tests;

public sealed class TestLocalizerTests
{
    [Fact]
    public void Create_ForResourceType_ResolvesEnglishString()
    {
        // Arrange
        var localizer = TestLocalizer.Create<SampleStrings>();

        // Act
        var value = localizer["Greeting"];

        // Assert
        Assert.Equal("Hello", value);
        Assert.False(value.ResourceNotFound);
    }

    [Fact]
    public void Create_UnderGermanCulture_ResolvesGermanString()
    {
        // Composed with CultureScope.UiOnly — also an integration test of the two helpers
        // together.
        // Arrange
        using var culture = CultureScope.UiOnly("de");
        var localizer = TestLocalizer.Create<SampleStrings>();

        // Act
        var value = localizer["Greeting"];

        // Assert
        Assert.Equal("Hallo", value);
    }

    [Fact]
    public void Create_MissingKey_ReturnsKeyWithResourceNotFound()
    {
        // Documents the fallback: a lookup miss doesn't throw, it echoes the key back.
        // Arrange
        var localizer = TestLocalizer.Create<SampleStrings>();

        // Act
        var value = localizer["NoSuchKey"];

        // Assert
        Assert.Equal("NoSuchKey", value.Value);
        Assert.True(value.ResourceNotFound);
    }

    [Fact]
    public void Create_NonGenericOverload_MatchesGenericOverload()
    {
        // Arrange
        var generic = TestLocalizer.Create<SampleStrings>();
        var nonGeneric = TestLocalizer.Create(typeof(SampleStrings));

        // Act / Assert
        Assert.Equal(generic["Greeting"].Value, nonGeneric["Greeting"].Value);
    }
}
