using Microsoft.CodeAnalysis;

namespace Crusaders30XX.ECS.Generators;

internal static class DiagnosticDescriptors
{
    private const string Category = "DataOrientedECS";

    public static readonly DiagnosticDescriptor ComponentMustBeStruct = Create(
        "ECSGEN001",
        "Components must be structs",
        "Component '{0}' must be a struct",
        "Data-oriented components cannot be reference types.");

    public static readonly DiagnosticDescriptor ComponentMustBeUnmanaged = Create(
        "ECSGEN002",
        "Components must be unmanaged",
        "Component '{0}' must be unmanaged; managed field '{1}' prevents contiguous storage",
        "Data-oriented components cannot contain managed state.");

    public static readonly DiagnosticDescriptor TagMustBeStruct = Create(
        "ECSGEN003",
        "Tags must be structs",
        "Tag '{0}' must be a struct",
        "Data-oriented tags cannot be reference types.");

    public static readonly DiagnosticDescriptor TagMustBeEmpty = Create(
        "ECSGEN004",
        "Tags must be empty",
        "Tag '{0}' must be empty; instance field '{1}' belongs in a component",
        "Tags are signature bits and do not allocate columns.");

    public static readonly DiagnosticDescriptor AmbiguousComponentKind = Create(
        "ECSGEN005",
        "A type cannot be both a component and a tag",
        "Type '{0}' implements both IComponent and ITag",
        "A registered ECS type must have exactly one storage kind.");

    public static readonly DiagnosticDescriptor TypeLimitExceeded = Create(
        "ECSGEN006",
        "The ECS type limit was exceeded",
        "The compilation registers {0} component and tag types, exceeding the 512-type limit",
        "ComponentSignature contains exactly 512 bits shared by components and tags.");

    public static readonly DiagnosticDescriptor DuplicateStableId = Create(
        "ECSGEN007",
        "Stable descriptor ID is duplicated",
        "Stable query descriptor ID '{0}' is also used by '{1}'",
        "Stable descriptor IDs must uniquely identify generated declarations.");

    public static readonly DiagnosticDescriptor QueryMustBePartialStruct = Create(
        "ECSGEN008",
        "Query declaration must be a partial struct",
        "Query declaration '{0}' must be a top-level, non-generic partial struct",
        "The generator adds strongly typed query members to the declaration.");

    public static readonly DiagnosticDescriptor InvalidQueryArity = Create(
        "ECSGEN009",
        "Query arity must be between one and eight",
        "Query declaration '{0}' returns {1} component types; supported arity is one through eight",
        "Split queries instead of adding per-row lookups for more than eight returned components.");

    public static readonly DiagnosticDescriptor InvalidQueryType = Create(
        "ECSGEN010",
        "Query declaration contains an invalid type",
        "Query declaration '{0}' uses '{1}' as {2}, but that position requires {3}",
        "Returned query values are components; filters may contain components or tags.");

    public static readonly DiagnosticDescriptor DuplicateQueryType = Create(
        "ECSGEN011",
        "Query returns a component more than once",
        "Query declaration '{0}' returns component '{1}' more than once",
        "Each returned component column may appear only once.");

    public static readonly DiagnosticDescriptor GenericEcsTypeNotSupported = Create(
        "ECSGEN012",
        "Open generic ECS types are not supported",
        "ECS type '{0}' is generic and cannot receive one build-local runtime ID",
        "Register concrete, non-generic component and tag structs.");

    public static readonly DiagnosticDescriptor InaccessibleEcsType = Create(
        "ECSGEN013",
        "ECS type is inaccessible to generated registration",
        "ECS type '{0}' or one of its containing types is private or protected",
        "Generated registration code must be able to reference every ECS type.");

    public static readonly DiagnosticDescriptor InvalidStableId = Create(
        "ECSGEN014",
        "Stable descriptor ID is invalid",
        "Query declaration '{0}' must specify a non-empty stable descriptor ID",
        "Stable IDs are used by diagnostics and generated debug metadata.");

    private static DiagnosticDescriptor Create(string id, string title, string message, string description)
    {
        return new DiagnosticDescriptor(
            id,
            title,
            message,
            Category,
            DiagnosticSeverity.Error,
            isEnabledByDefault: true,
            description);
    }
}
