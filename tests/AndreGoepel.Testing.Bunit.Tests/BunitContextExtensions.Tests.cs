namespace AndreGoepel.Testing.Bunit.Tests;

public sealed class BunitContextExtensionsTests
{
    [Fact]
    public void UseLooseJSInterop_OnContext_SetsLooseMode()
    {
        // Arrange
        using var context = new BunitContext();

        // Act
        context.UseLooseJSInterop();

        // Assert
        Assert.Equal(JSRuntimeMode.Loose, context.JSInterop.Mode);
    }

    [Fact]
    public void UseLooseJSInterop_RenderingComponentThatCallsJs_DoesNotThrow()
    {
        // Arrange
        using var context = new BunitContext();
        context.UseLooseJSInterop();

        // Act
        var cut = context.Render<JsCallingComponent>();

        // Assert
        Assert.NotNull(cut);
    }

    [Fact]
    public void UseLocalization_OnContext_ResolvesStringLocalizer()
    {
        // Arrange
        using var context = new BunitContext();

        // Act
        context.UseLocalization();

        // Assert
        Assert.NotNull(context.Services.GetService<IStringLocalizer<SampleStrings>>());
    }

    [Fact]
    public void UseLocalization_NotCalled_LeavesStringLocalizerUnregistered()
    {
        // Guards the constraint the package exists to preserve: registration is opt-in, never
        // implicit — a consumer whose components must run without localization (e.g. a page
        // library rendered by a host that resolves IStringLocalizer optionally) can rely on it
        // staying unregistered unless UseLocalization() is called.
        // Arrange
        using var context = new BunitContext();

        // Act / Assert
        Assert.Null(context.Services.GetService<IStringLocalizer<SampleStrings>>());
    }

    [Fact]
    public void Extensions_Chained_ReturnTheSameContext()
    {
        // Arrange
        using var context = new BunitContext();

        // Act
        var result = context.UseLooseJSInterop().UseLocalization();

        // Assert
        Assert.Same(context, result);
    }
}
