using Microsoft.CodeAnalysis;

namespace Crusaders30XX.ECS.Generators;

internal static class DefinitionDiagnosticDescriptors
{
    private const string Category = "DataOrientedDefinitions";

    public static readonly DiagnosticDescriptor DuplicateId = Create(
        "ECSDEF001",
        "Definition ID is duplicated",
        "Definition ID '{0}.{1}' is declared by both '{2}' and '{3}'");

    public static readonly DiagnosticDescriptor MissingId = Create(
        "ECSDEF002",
        "Definition ID is missing",
        "Complete catalog '{0}' has no module for ID '{1}'");

    public static readonly DiagnosticDescriptor InvalidModule = Create(
        "ECSDEF003",
        "Definition module shape is invalid",
        "Definition module '{0}' must be a top-level, non-generic static partial class accessible to generated code");

    public static readonly DiagnosticDescriptor InvalidDefinitionProperty = Create(
        "ECSDEF004",
        "Definition property is invalid",
        "Definition module '{0}' must declare an accessible static get-only 'Definition' property");

    public static readonly DiagnosticDescriptor InconsistentCatalogType = Create(
        "ECSDEF005",
        "Catalog value types are inconsistent",
        "Definition module '{0}' exposes {1} type '{2}', but catalog '{3}' uses '{4}'");

    public static readonly DiagnosticDescriptor InvalidHandler = Create(
        "ECSDEF006",
        "Definition handler is invalid",
        "Definition module '{0}' handler '{1}' must be an accessible non-generic static void method with exactly one ref parameter");

    public static readonly DiagnosticDescriptor InvalidIdDomain = Create(
        "ECSDEF007",
        "Definition ID domain is invalid",
        "Definition catalog ID type '{0}' must be a ushort-backed enum");

    public static readonly DiagnosticDescriptor UnknownId = Create(
        "ECSDEF008",
        "Definition ID is not declared",
        "Definition module '{0}' uses numeric ID '{1}', which is not declared by '{2}'");

    public static readonly DiagnosticDescriptor InvalidTableProperty = Create(
        "ECSDEF009",
        "Declarative table property is invalid",
        "Definition module '{0}' property '{1}' must be an accessible static ReadOnlySpan<{2}> property");

    private static DiagnosticDescriptor Create(string id, string title, string message) =>
        new(id, title, message, Category, DiagnosticSeverity.Error, isEnabledByDefault: true);
}
