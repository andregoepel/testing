namespace AndreGoepel.Testing.E2E;

/// <summary>
/// Shared xUnit collection name for E2E suites. The package cannot declare
/// <c>[CollectionDefinition]</c> itself — <c>ICollectionFixture&lt;T&gt;</c> needs each repo's own
/// concrete <see cref="E2EAppFixture"/> subclass — so every consuming repo keeps a two-line
/// <c>[CollectionDefinition(E2ECollectionDefaults.Name)] public sealed class E2ECollection :
/// ICollectionFixture&lt;E2EAppFixture&gt;</c> next to its fixture.
/// </summary>
public static class E2ECollectionDefaults
{
    public const string Name = "e2e";
}
