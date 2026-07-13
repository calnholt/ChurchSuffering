using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Crusaders30XX.ECS.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class DataOrientedEcsGenerator : IIncrementalGenerator
{
    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterPostInitializationOutput(output =>
            output.AddSource("EcsGeneratorAttributes.g.cs", SourceText.From(AttributeSource.Text, Encoding.UTF8)));

        IncrementalValuesProvider<INamedTypeSymbol?> ecsTypes = context.SyntaxProvider.CreateSyntaxProvider(
            static (node, _) => node is TypeDeclarationSyntax declaration && declaration.BaseList is not null,
            static (syntaxContext, _) => GetEcsType(syntaxContext));

        IncrementalValuesProvider<QueryModel?> queries = context.SyntaxProvider.ForAttributeWithMetadataName(
            SymbolFacts.QueryAttributeMetadataName,
            static (node, _) => node is TypeDeclarationSyntax,
            static (attributeContext, _) => GetQuery(attributeContext));

        context.RegisterSourceOutput(ecsTypes.Collect(), static (output, symbols) =>
        {
            ImmutableArray<EcsTypeModel> models = BuildTypeModels(symbols);
            output.AddSource("GeneratedComponentRegistry.g.cs", SourceText.From(GenerateRegistry(models), Encoding.UTF8));
            output.AddSource("GeneratedSpawnBundleExtensions.g.cs", SourceText.From(GenerateSpawnExtensions(models), Encoding.UTF8));
        });

        context.RegisterSourceOutput(queries.Collect(), static (output, models) =>
        {
            foreach (QueryModel query in models
                         .Where(model => model is not null)
                         .Select(model => model!)
                         .OrderBy(model => SymbolFacts.MetadataName(model.Symbol), StringComparer.Ordinal))
            {
                if (IsValidForGeneration(query))
                {
                    output.AddSource(QueryHintName(query.Symbol), SourceText.From(GenerateQuery(query), Encoding.UTF8));
                }
            }
        });
    }

    private static INamedTypeSymbol? GetEcsType(GeneratorSyntaxContext context)
    {
        if (context.SemanticModel.GetDeclaredSymbol((TypeDeclarationSyntax)context.Node) is not INamedTypeSymbol symbol)
        {
            return null;
        }

        INamedTypeSymbol? component = context.SemanticModel.Compilation.GetTypeByMetadataName(SymbolFacts.ComponentMetadataName);
        INamedTypeSymbol? tag = context.SemanticModel.Compilation.GetTypeByMetadataName(SymbolFacts.TagMetadataName);
        return SymbolFacts.Implements(symbol, component) || SymbolFacts.Implements(symbol, tag) ? symbol : null;
    }

    private static QueryModel? GetQuery(GeneratorAttributeSyntaxContext context)
    {
        if (context.TargetSymbol is not INamedTypeSymbol symbol || context.Attributes.Length == 0)
        {
            return null;
        }

        AttributeData attribute = context.Attributes[0];
        string stableId = attribute.ConstructorArguments.Length > 0
            ? attribute.ConstructorArguments[0].Value as string ?? string.Empty
            : string.Empty;
        ImmutableArray<ITypeSymbol> returned = GetTypeArray(
            attribute.ConstructorArguments.Length > 1 ? attribute.ConstructorArguments[1] : default);

        return new QueryModel(
            symbol,
            stableId,
            returned,
            GetNamedTypeArray(attribute, "All"),
            GetNamedTypeArray(attribute, "Any"),
            GetNamedTypeArray(attribute, "None"),
            GetNamedBool(attribute, "IncludeDisabled"));
    }

    private static ImmutableArray<EcsTypeModel> BuildTypeModels(ImmutableArray<INamedTypeSymbol?> symbols)
    {
        var unique = new Dictionary<string, INamedTypeSymbol>(StringComparer.Ordinal);
        foreach (INamedTypeSymbol? symbol in symbols)
        {
            if (symbol is not null)
            {
                unique[SymbolFacts.MetadataName(symbol)] = symbol;
            }
        }

        var builder = ImmutableArray.CreateBuilder<EcsTypeModel>();
        foreach (KeyValuePair<string, INamedTypeSymbol> pair in unique.OrderBy(pair => pair.Key, StringComparer.Ordinal))
        {
            INamedTypeSymbol symbol = pair.Value;
            bool isComponent = SymbolFacts.ImplementsMetadataName(symbol, SymbolFacts.ComponentMetadataName);
            bool isTag = SymbolFacts.ImplementsMetadataName(symbol, SymbolFacts.TagMetadataName);
            if (isComponent == isTag || symbol.TypeKind != TypeKind.Struct || symbol.Arity != 0 ||
                !symbol.IsUnmanagedType || !SymbolFacts.IsGeneratorAccessible(symbol) ||
                (isTag && SymbolFacts.FirstInstanceField(symbol) is not null))
            {
                continue;
            }

            builder.Add(new EcsTypeModel(symbol, isTag));
        }

        return builder.ToImmutable();
    }

    private static string GenerateRegistry(ImmutableArray<EcsTypeModel> models)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#nullable enable");
        source.AppendLine();
        source.AppendLine("namespace Crusaders30XX.ECS.DataOriented.Generated");
        source.AppendLine("{");
        AppendQueryDescriptorTypes(source);
        source.AppendLine("    public readonly struct GeneratedComponentDescriptor");
        source.AppendLine("    {");
        source.AppendLine("        public GeneratedComponentDescriptor(int id, string metadataName, string displayName, bool isTag)");
        source.AppendLine("        {");
        source.AppendLine("            Id = id;");
        source.AppendLine("            MetadataName = metadataName;");
        source.AppendLine("            DisplayName = displayName;");
        source.AppendLine("            IsTag = isTag;");
        source.AppendLine("        }");
        source.AppendLine();
        source.AppendLine("        public int Id { get; }");
        source.AppendLine("        public string MetadataName { get; }");
        source.AppendLine("        public string DisplayName { get; }");
        source.AppendLine("        public bool IsTag { get; }");
        source.AppendLine("    }");
        source.AppendLine();
        source.AppendLine("    public static class GeneratedComponentRegistry");
        source.AppendLine("    {");
        source.AppendLine("        private static readonly GeneratedComponentDescriptor[] s_descriptors = new GeneratedComponentDescriptor[]");
        source.AppendLine("        {");
        for (int id = 0; id < models.Length; id++)
        {
            EcsTypeModel model = models[id];
            source.Append("            new GeneratedComponentDescriptor(")
                .Append(id).Append(", \"").Append(SymbolFacts.Escape(model.MetadataName)).Append("\", \"")
                .Append(SymbolFacts.Escape(model.Symbol.Name)).Append("\", ")
                .Append(model.IsTag ? "true" : "false").AppendLine("),");
        }

        source.AppendLine("        };");
        source.AppendLine();
        source.Append("        public const int Count = ").Append(models.Length).AppendLine(";");
        source.AppendLine();
        source.AppendLine("        public static global::System.ReadOnlySpan<GeneratedComponentDescriptor> Descriptors => s_descriptors;");
        source.AppendLine();
        source.AppendLine("        public static global::Crusaders30XX.ECS.DataOriented.Core.ComponentTypeRegistry Create()");
        source.AppendLine("        {");
        source.AppendLine("            var registry = new global::Crusaders30XX.ECS.DataOriented.Core.ComponentTypeRegistry();");
        for (int id = 0; id < models.Length; id++)
        {
            EcsTypeModel model = models[id];
            source.Append("            registry.").Append(model.IsTag ? "RegisterTag" : "RegisterComponent")
                .Append('<').Append(model.TypeReference).Append(">(").Append(id).AppendLine(");");
        }

        source.AppendLine("            registry.Seal();");
        source.AppendLine("            return registry;");
        source.AppendLine("        }");
        source.AppendLine();
        source.AppendLine("        public static bool TryGetDescriptor(string metadataName, out GeneratedComponentDescriptor descriptor)");
        source.AppendLine("        {");
        source.AppendLine("            for (int index = 0; index < s_descriptors.Length; index++)");
        source.AppendLine("            {");
        source.AppendLine("                if (global::System.StringComparer.Ordinal.Equals(s_descriptors[index].MetadataName, metadataName))");
        source.AppendLine("                {");
        source.AppendLine("                    descriptor = s_descriptors[index];");
        source.AppendLine("                    return true;");
        source.AppendLine("                }");
        source.AppendLine("            }");
        source.AppendLine();
        source.AppendLine("            descriptor = default;");
        source.AppendLine("            return false;");
        source.AppendLine("        }");
        source.AppendLine("    }");
        source.AppendLine("}");
        return source.ToString();
    }

    private static void AppendQueryDescriptorTypes(StringBuilder source)
    {
        for (int arity = 1; arity <= 8; arity++)
        {
            string typeParameters = string.Join(", ", Enumerable.Range(1, arity).Select(index => "T" + index));
            source.Append("    public readonly struct GeneratedQueryDescriptor<").Append(typeParameters).AppendLine(">");
            for (int index = 1; index <= arity; index++)
            {
                source.Append("        where T").Append(index)
                    .AppendLine(" : unmanaged, global::Crusaders30XX.ECS.DataOriented.Core.IComponent");
            }

            source.AppendLine("    {");
            source.Append("        public GeneratedQueryDescriptor(string stableId, ")
                .AppendLine("global::Crusaders30XX.ECS.DataOriented.Core.ComponentSignature required,");
            source.AppendLine("            global::Crusaders30XX.ECS.DataOriented.Core.ComponentSignature any,");
            source.AppendLine("            global::Crusaders30XX.ECS.DataOriented.Core.ComponentSignature none,");
            source.AppendLine("            bool includeDisabled)");
            source.AppendLine("        {");
            source.AppendLine("            StableId = stableId;");
            source.AppendLine("            Required = required;");
            source.AppendLine("            Any = any;");
            source.AppendLine("            None = none;");
            source.AppendLine("            IncludeDisabled = includeDisabled;");
            source.AppendLine("        }");
            source.AppendLine();
            source.AppendLine("        public string StableId { get; }");
            source.AppendLine("        public global::Crusaders30XX.ECS.DataOriented.Core.ComponentSignature Required { get; }");
            source.AppendLine("        public global::Crusaders30XX.ECS.DataOriented.Core.ComponentSignature Any { get; }");
            source.AppendLine("        public global::Crusaders30XX.ECS.DataOriented.Core.ComponentSignature None { get; }");
            source.AppendLine("        public bool IncludeDisabled { get; }");
            source.AppendLine("    }");
            source.AppendLine();
        }
    }

    private static string GenerateSpawnExtensions(ImmutableArray<EcsTypeModel> models)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#nullable enable");
        source.AppendLine();
        source.AppendLine("namespace Crusaders30XX.ECS.DataOriented.Generated");
        source.AppendLine("{");
        source.AppendLine("    public static class GeneratedSpawnBundleExtensions");
        source.AppendLine("    {");
        var usedTagMethodNames = new HashSet<string>(StringComparer.Ordinal);
        for (int id = 0; id < models.Length; id++)
        {
            EcsTypeModel model = models[id];
            string methodName = "With" + Identifier(model.Symbol.Name);
            if (model.IsTag && !usedTagMethodNames.Add(methodName))
            {
                methodName += id.ToString(System.Globalization.CultureInfo.InvariantCulture);
                usedTagMethodNames.Add(methodName);
            }

            if (model.IsTag)
            {
                source.Append("        public static ref global::Crusaders30XX.ECS.DataOriented.Core.SpawnBundle ")
                    .Append(methodName)
                    .AppendLine("(this ref global::Crusaders30XX.ECS.DataOriented.Core.SpawnBundle bundle)");
                source.AppendLine("        {");
                source.Append("            bundle.AddTag<").Append(model.TypeReference).AppendLine(">();");
            }
            else
            {
                source.Append("        public static ref global::Crusaders30XX.ECS.DataOriented.Core.SpawnBundle ")
                    .Append(methodName)
                    .Append("(this ref global::Crusaders30XX.ECS.DataOriented.Core.SpawnBundle bundle, in ")
                    .Append(model.TypeReference).AppendLine(" value)");
                source.AppendLine("        {");
                source.AppendLine("            bundle.Add(in value);");
            }

            source.AppendLine("            return ref bundle;");
            source.AppendLine("        }");
            source.AppendLine();
        }

        source.AppendLine("    }");
        source.AppendLine("}");
        return source.ToString();
    }

    private static bool IsValidForGeneration(QueryModel query)
    {
        return query.Symbol.TypeKind == TypeKind.Struct &&
               query.Symbol.ContainingType is null &&
               query.Symbol.Arity == 0 &&
               SymbolFacts.IsPartial(query.Symbol) &&
               !string.IsNullOrWhiteSpace(query.StableId) &&
               query.Returned.Length is >= 1 and <= 8 &&
               query.Returned.Distinct(SymbolEqualityComparer.Default).Count() == query.Returned.Length &&
               query.Returned.All(type => type is INamedTypeSymbol named &&
                   named.IsUnmanagedType &&
                   SymbolFacts.ImplementsMetadataName(named, SymbolFacts.ComponentMetadataName)) &&
               query.All.Concat(query.Any).Concat(query.None).All(type => type is INamedTypeSymbol named &&
                   (SymbolFacts.ImplementsMetadataName(named, SymbolFacts.ComponentMetadataName) ||
                    SymbolFacts.ImplementsMetadataName(named, SymbolFacts.TagMetadataName)));
    }

    private static string GenerateQuery(QueryModel query)
    {
        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#nullable enable");
        source.AppendLine();
        if (!query.Symbol.ContainingNamespace.IsGlobalNamespace)
        {
            source.Append("namespace ").Append(query.Symbol.ContainingNamespace.ToDisplayString()).AppendLine();
            source.AppendLine("{");
        }

        string indent = query.Symbol.ContainingNamespace.IsGlobalNamespace ? string.Empty : "    ";
        source.Append(indent).Append("partial ").Append(query.Symbol.IsRecord ? "record struct " : "struct ")
            .Append(Identifier(query.Symbol.Name)).AppendLine();
        source.Append(indent).AppendLine("{");
        source.Append(indent).Append("    public const string StableId = \"")
            .Append(SymbolFacts.Escape(query.StableId)).AppendLine("\";");
        source.Append(indent).Append("    public const int Arity = ").Append(query.Returned.Length).AppendLine(";");
        source.Append(indent).AppendLine("    public const bool IncludeDisabled = " + (query.IncludeDisabled ? "true;" : "false;"));
        source.AppendLine();
        source.Append(indent).Append("    public static global::Crusaders30XX.ECS.DataOriented.Generated.GeneratedQueryDescriptor<")
            .Append(string.Join(", ", query.Returned.Select(SymbolFacts.TypeReference)))
            .AppendLine("> Descriptor");
        source.Append(indent).AppendLine("    {");
        source.Append(indent).AppendLine("        get");
        source.Append(indent).AppendLine("        {");
        source.Append(indent).Append("            return new global::Crusaders30XX.ECS.DataOriented.Generated.GeneratedQueryDescriptor<")
            .Append(string.Join(", ", query.Returned.Select(SymbolFacts.TypeReference))).AppendLine(">");
        source.Append(indent).AppendLine("            (");
        source.Append(indent).AppendLine("                StableId,");
        source.Append(indent).Append("                ").Append(SignatureExpression(query.Returned.Concat(query.All))).AppendLine(",");
        source.Append(indent).Append("                ").Append(SignatureExpression(query.Any)).AppendLine(",");
        source.Append(indent).Append("                ").Append(SignatureExpression(query.None)).AppendLine(",");
        source.Append(indent).AppendLine("                IncludeDisabled);");
        source.Append(indent).AppendLine("        }");
        source.Append(indent).AppendLine("    }");
        source.Append(indent).AppendLine("}");
        if (!query.Symbol.ContainingNamespace.IsGlobalNamespace)
        {
            source.AppendLine("}");
        }

        return source.ToString();
    }

    private static ImmutableArray<ITypeSymbol> GetNamedTypeArray(AttributeData attribute, string name)
    {
        foreach (KeyValuePair<string, TypedConstant> pair in attribute.NamedArguments)
        {
            if (pair.Key == name)
            {
                return GetTypeArray(pair.Value);
            }
        }

        return ImmutableArray<ITypeSymbol>.Empty;
    }

    private static bool GetNamedBool(AttributeData attribute, string name)
    {
        foreach (KeyValuePair<string, TypedConstant> pair in attribute.NamedArguments)
        {
            if (pair.Key == name && pair.Value.Value is bool value)
            {
                return value;
            }
        }

        return false;
    }

    private static ImmutableArray<ITypeSymbol> GetTypeArray(TypedConstant constant)
    {
        if (constant.Kind != TypedConstantKind.Array)
        {
            return ImmutableArray<ITypeSymbol>.Empty;
        }

        return constant.Values.Select(value => value.Value).OfType<ITypeSymbol>().ToImmutableArray();
    }

    private static string QueryHintName(INamedTypeSymbol symbol)
    {
        var builder = new StringBuilder(SymbolFacts.MetadataName(symbol));
        for (int index = 0; index < builder.Length; index++)
        {
            if (!char.IsLetterOrDigit(builder[index]))
            {
                builder[index] = '_';
            }
        }

        return builder.Append(".Query.g.cs").ToString();
    }

    private static string Identifier(string value)
    {
        return SyntaxFacts.GetKeywordKind(value) == SyntaxKind.None ? value : "@" + value;
    }

    private static string SignatureExpression(IEnumerable<ITypeSymbol> types)
    {
        var source = new StringBuilder("global::Crusaders30XX.ECS.DataOriented.Core.ComponentSignature.Empty");
        var seen = new HashSet<string>(StringComparer.Ordinal);
        foreach (ITypeSymbol type in types)
        {
            string typeReference = SymbolFacts.TypeReference(type);
            if (!seen.Add(typeReference))
            {
                continue;
            }

            source.Append(".With(global::Crusaders30XX.ECS.DataOriented.Core.ComponentType<")
                .Append(typeReference).Append(">.Id)");
        }

        return source.ToString();
    }
}
