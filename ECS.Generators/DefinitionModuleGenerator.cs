using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Globalization;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Crusaders30XX.ECS.Generators;

[Generator(LanguageNames.CSharp)]
public sealed class DefinitionModuleGenerator : IIncrementalGenerator
{
    private const string AttributeNamespace = "Crusaders30XX.ECS.DataOriented.Definitions.";
    private const string CatalogAttributeMetadataName = AttributeNamespace + "DefinitionCatalogAttribute";
    private const string EffectSpecMetadataName = AttributeNamespace + "EffectSpec";
    private const string ConditionSpecMetadataName = AttributeNamespace + "ConditionSpec";

    private static readonly ImmutableHashSet<string> ModuleAttributeMetadataNames = ImmutableHashSet.Create(
        StringComparer.Ordinal,
        AttributeNamespace + "CardDefinitionAttribute",
        AttributeNamespace + "EnemyDefinitionAttribute",
        AttributeNamespace + "EnemyAttackDefinitionAttribute",
        AttributeNamespace + "EquipmentDefinitionAttribute",
        AttributeNamespace + "MedalDefinitionAttribute");

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        context.RegisterSourceOutput(context.CompilationProvider, static (output, compilation) => Execute(output, compilation));
    }

    private static void Execute(SourceProductionContext output, Compilation compilation)
    {
        var candidates = new List<(INamedTypeSymbol Symbol, AttributeData Attribute)>();
        VisitNamespace(compilation.Assembly.GlobalNamespace, candidates);

        var modulesByDomain = new Dictionary<INamedTypeSymbol, List<DefinitionModuleModel>>(SymbolEqualityComparer.Default);
        foreach ((INamedTypeSymbol symbol, AttributeData attribute) in candidates)
        {
            DefinitionModuleModel? module = BuildModule(output, symbol, attribute);
            if (module is null)
            {
                continue;
            }

            if (!modulesByDomain.TryGetValue(module.IdType, out List<DefinitionModuleModel>? modules))
            {
                modules = new List<DefinitionModuleModel>();
                modulesByDomain.Add(module.IdType, modules);
            }

            modules.Add(module);
        }

        ValidateCompleteness(output, compilation, modulesByDomain);
        foreach (KeyValuePair<INamedTypeSymbol, List<DefinitionModuleModel>> pair in modulesByDomain
                     .OrderBy(pair => SymbolFacts.MetadataName(pair.Key), StringComparer.Ordinal))
        {
            List<DefinitionModuleModel> modules = pair.Value
                .OrderBy(module => module.Value)
                .ThenBy(module => SymbolFacts.MetadataName(module.Symbol), StringComparer.Ordinal)
                .ToList();
            if (!ValidateDomain(output, pair.Key, modules))
            {
                continue;
            }

            var domain = new DefinitionDomainModel(pair.Key, modules);
            output.AddSource(CatalogHintName(pair.Key), SourceText.From(GenerateCatalog(domain), Encoding.UTF8));
        }
    }

    private static void VisitNamespace(
        INamespaceSymbol namespaceSymbol,
        List<(INamedTypeSymbol Symbol, AttributeData Attribute)> candidates)
    {
        foreach (INamedTypeSymbol type in namespaceSymbol.GetTypeMembers())
        {
            VisitType(type, candidates);
        }

        foreach (INamespaceSymbol child in namespaceSymbol.GetNamespaceMembers())
        {
            VisitNamespace(child, candidates);
        }
    }

    private static void VisitType(
        INamedTypeSymbol type,
        List<(INamedTypeSymbol Symbol, AttributeData Attribute)> candidates)
    {
        foreach (AttributeData attribute in type.GetAttributes())
        {
            if (attribute.AttributeClass is not null &&
                ModuleAttributeMetadataNames.Contains(SymbolFacts.MetadataName(attribute.AttributeClass)))
            {
                candidates.Add((type, attribute));
            }
        }

        foreach (INamedTypeSymbol nested in type.GetTypeMembers())
        {
            VisitType(nested, candidates);
        }
    }

    private static DefinitionModuleModel? BuildModule(
        SourceProductionContext output,
        INamedTypeSymbol symbol,
        AttributeData attribute)
    {
        Location location = AttributeLocation(attribute, symbol);
        if (symbol.TypeKind != TypeKind.Class || !symbol.IsStatic || symbol.Arity != 0 ||
            symbol.ContainingType is not null || !SymbolFacts.IsPartial(symbol) || !SymbolFacts.IsGeneratorAccessible(symbol))
        {
            output.ReportDiagnostic(Diagnostic.Create(
                DefinitionDiagnosticDescriptors.InvalidModule,
                location,
                symbol.ToDisplayString()));
            return null;
        }

        if (attribute.ConstructorArguments.Length == 0 ||
            attribute.ConstructorArguments[0].Type is not INamedTypeSymbol idType ||
            !TryGetUShort(attribute.ConstructorArguments[0].Value, out ushort value))
        {
            output.ReportDiagnostic(Diagnostic.Create(
                DefinitionDiagnosticDescriptors.InvalidIdDomain,
                location,
                attribute.ConstructorArguments.FirstOrDefault().Type?.ToDisplayString() ?? "<unknown>"));
            return null;
        }

        if (!IsUShortEnum(idType))
        {
            output.ReportDiagnostic(Diagnostic.Create(
                DefinitionDiagnosticDescriptors.InvalidIdDomain,
                location,
                idType.ToDisplayString()));
            return null;
        }

        Dictionary<ushort, string> members = EnumMembers(idType);
        if (!members.TryGetValue(value, out string? idName))
        {
            output.ReportDiagnostic(Diagnostic.Create(
                DefinitionDiagnosticDescriptors.UnknownId,
                location,
                symbol.ToDisplayString(),
                value,
                idType.ToDisplayString()));
            return null;
        }

        IPropertySymbol? definition = FindStaticProperty(symbol, "Definition");
        if (definition is null)
        {
            output.ReportDiagnostic(Diagnostic.Create(
                DefinitionDiagnosticDescriptors.InvalidDefinitionProperty,
                location,
                symbol.ToDisplayString()));
            return null;
        }

        IPropertySymbol? upgrade = FindStaticProperty(symbol, "Upgrade");
        IPropertySymbol? effects = FindStaticProperty(symbol, "Effects");
        IPropertySymbol? conditions = FindStaticProperty(symbol, "Conditions");
        bool validTables = ValidateTableProperty(output, symbol, "Effects", effects, EffectSpecMetadataName, location) &
                           ValidateTableProperty(output, symbol, "Conditions", conditions, ConditionSpecMetadataName, location);
        if (!validTables)
        {
            return null;
        }

        string? handlerName = attribute.NamedArguments
            .Where(pair => pair.Key == "Handler")
            .Select(pair => pair.Value.Value as string)
            .FirstOrDefault();
        IMethodSymbol? handler = null;
        if (!string.IsNullOrWhiteSpace(handlerName))
        {
            handler = symbol.GetMembers(handlerName!)
                .OfType<IMethodSymbol>()
                .FirstOrDefault(IsValidHandler);
            if (handler is null)
            {
                output.ReportDiagnostic(Diagnostic.Create(
                    DefinitionDiagnosticDescriptors.InvalidHandler,
                    location,
                    symbol.ToDisplayString(),
                    handlerName));
                return null;
            }
        }

        return new DefinitionModuleModel(
            symbol,
            idType,
            value,
            idName,
            definition,
            upgrade,
            effects,
            conditions,
            handler,
            location);
    }

    private static bool ValidateTableProperty(
        SourceProductionContext output,
        INamedTypeSymbol module,
        string propertyName,
        IPropertySymbol? property,
        string itemMetadataName,
        Location location)
    {
        if (property is null)
        {
            if (!module.GetMembers(propertyName).Any())
            {
                return true;
            }

            output.ReportDiagnostic(Diagnostic.Create(
                DefinitionDiagnosticDescriptors.InvalidTableProperty,
                location,
                module.ToDisplayString(),
                propertyName,
                itemMetadataName.Substring(itemMetadataName.LastIndexOf('.') + 1)));
            return false;
        }

        if (property.Type is INamedTypeSymbol named &&
            SymbolFacts.MetadataName(named.OriginalDefinition) == "System.ReadOnlySpan`1" &&
            named.TypeArguments.Length == 1 &&
            named.TypeArguments[0] is INamedTypeSymbol item &&
            SymbolFacts.MetadataName(item) == itemMetadataName)
        {
            return true;
        }

        output.ReportDiagnostic(Diagnostic.Create(
            DefinitionDiagnosticDescriptors.InvalidTableProperty,
            location,
            module.ToDisplayString(),
            propertyName,
            itemMetadataName.Substring(itemMetadataName.LastIndexOf('.') + 1)));
        return false;
    }

    private static bool ValidateDomain(
        SourceProductionContext output,
        INamedTypeSymbol idType,
        IReadOnlyList<DefinitionModuleModel> modules)
    {
        bool valid = true;
        foreach (IGrouping<ushort, DefinitionModuleModel> duplicate in modules.GroupBy(module => module.Value)
                     .Where(group => group.Count() > 1))
        {
            DefinitionModuleModel first = duplicate.First();
            foreach (DefinitionModuleModel module in duplicate.Skip(1))
            {
                output.ReportDiagnostic(Diagnostic.Create(
                    DefinitionDiagnosticDescriptors.DuplicateId,
                    module.Location,
                    idType.Name,
                    first.IdName,
                    SymbolFacts.MetadataName(first.Symbol),
                    SymbolFacts.MetadataName(module.Symbol)));
            }

            valid = false;
        }

        ITypeSymbol definitionType = modules[0].Definition.Type;
        ITypeSymbol? upgradeType = modules.Select(module => module.Upgrade?.Type).FirstOrDefault(type => type is not null);
        ITypeSymbol? handlerContextType = modules.Select(module => module.Handler?.Parameters[0].Type).FirstOrDefault(type => type is not null);
        foreach (DefinitionModuleModel module in modules)
        {
            valid &= ValidateMatchingType(output, module, "Definition", module.Definition.Type, definitionType, idType);
            if (module.Upgrade is not null && upgradeType is not null)
            {
                valid &= ValidateMatchingType(output, module, "Upgrade", module.Upgrade.Type, upgradeType, idType);
            }

            if (module.Handler is not null && handlerContextType is not null)
            {
                valid &= ValidateMatchingType(
                    output,
                    module,
                    "handler context",
                    module.Handler.Parameters[0].Type,
                    handlerContextType,
                    idType);
            }
        }

        return valid;
    }

    private static bool ValidateMatchingType(
        SourceProductionContext output,
        DefinitionModuleModel module,
        string role,
        ITypeSymbol actual,
        ITypeSymbol expected,
        INamedTypeSymbol idType)
    {
        if (SymbolEqualityComparer.Default.Equals(actual, expected))
        {
            return true;
        }

        output.ReportDiagnostic(Diagnostic.Create(
            DefinitionDiagnosticDescriptors.InconsistentCatalogType,
            module.Location,
            module.Symbol.ToDisplayString(),
            role,
            actual.ToDisplayString(),
            idType.ToDisplayString(),
            expected.ToDisplayString()));
        return false;
    }

    private static void ValidateCompleteness(
        SourceProductionContext output,
        Compilation compilation,
        IReadOnlyDictionary<INamedTypeSymbol, List<DefinitionModuleModel>> modulesByDomain)
    {
        foreach (AttributeData attribute in compilation.Assembly.GetAttributes())
        {
            if (attribute.AttributeClass is null ||
                SymbolFacts.MetadataName(attribute.AttributeClass) != CatalogAttributeMetadataName ||
                attribute.ConstructorArguments.Length == 0 ||
                attribute.ConstructorArguments[0].Value is not INamedTypeSymbol idType)
            {
                continue;
            }

            Location location = AttributeLocation(attribute, idType);
            if (!IsUShortEnum(idType))
            {
                output.ReportDiagnostic(Diagnostic.Create(
                    DefinitionDiagnosticDescriptors.InvalidIdDomain,
                    location,
                    idType.ToDisplayString()));
                continue;
            }

            bool requireComplete = attribute.NamedArguments
                .Where(pair => pair.Key == "RequireComplete")
                .Select(pair => pair.Value.Value is bool value && value)
                .DefaultIfEmpty(true)
                .First();
            if (!requireComplete)
            {
                continue;
            }

            var defined = new HashSet<ushort>();
            if (modulesByDomain.TryGetValue(idType, out List<DefinitionModuleModel>? modules))
            {
                defined.UnionWith(modules.Select(module => module.Value));
            }

            foreach (KeyValuePair<ushort, string> member in EnumMembers(idType).OrderBy(pair => pair.Key))
            {
                if (!defined.Contains(member.Key))
                {
                    output.ReportDiagnostic(Diagnostic.Create(
                        DefinitionDiagnosticDescriptors.MissingId,
                        location,
                        idType.ToDisplayString(),
                        member.Value));
                }
            }
        }
    }

    private static string GenerateCatalog(DefinitionDomainModel domain)
    {
        IReadOnlyList<DefinitionModuleModel> modules = domain.Modules;
        string idType = SymbolFacts.TypeReference(domain.IdType);
        string definitionType = SymbolFacts.TypeReference(modules[0].Definition.Type);
        ITypeSymbol? upgradeSymbol = modules.Select(module => module.Upgrade?.Type).FirstOrDefault(type => type is not null);
        string? upgradeType = upgradeSymbol is null ? null : SymbolFacts.TypeReference(upgradeSymbol);
        ITypeSymbol? handlerContextSymbol = modules.Select(module => module.Handler?.Parameters[0].Type).FirstOrDefault(type => type is not null);
        string? handlerContextType = handlerContextSymbol is null ? null : SymbolFacts.TypeReference(handlerContextSymbol);
        int capacity = EnumMembers(domain.IdType).Keys.Select(value => (int)value).DefaultIfEmpty(-1).Max() + 1;
        string catalogName = CatalogClassName(domain.IdType);

        var source = new StringBuilder();
        source.AppendLine("// <auto-generated />");
        source.AppendLine("#nullable enable");
        source.AppendLine();
        source.AppendLine("namespace Crusaders30XX.ECS.DataOriented.Generated");
        source.AppendLine("{");
        source.Append("    public static class ").Append(catalogName).AppendLine();
        source.AppendLine("    {");
        source.Append("        private static readonly ").Append(definitionType).AppendLine("[] s_definitions;");
        if (upgradeType is not null)
        {
            source.Append("        private static readonly ").Append(upgradeType).AppendLine("[] s_upgrades;");
        }

        source.AppendLine("        private static readonly bool[] s_defined;");
        source.AppendLine("        private static readonly global::Crusaders30XX.ECS.DataOriented.Definitions.DefinitionCatalogEntry[] s_entries;");
        source.AppendLine("        private static readonly global::Crusaders30XX.ECS.DataOriented.Definitions.EffectSpec[] s_effects;");
        source.AppendLine("        private static readonly global::Crusaders30XX.ECS.DataOriented.Definitions.ConditionSpec[] s_conditions;");
        source.Append("        private static readonly global::Crusaders30XX.ECS.DataOriented.Definitions.DefinitionDebugMetadata<")
            .Append(idType).AppendLine(">[] s_debugMetadata;");
        source.AppendLine();
        source.Append("        static ").Append(catalogName).AppendLine("()");
        source.AppendLine("        {");

        for (int index = 0; index < modules.Count; index++)
        {
            DefinitionModuleModel module = modules[index];
            string moduleType = SymbolFacts.TypeReference(module.Symbol);
            source.Append("            global::System.ReadOnlySpan<global::Crusaders30XX.ECS.DataOriented.Definitions.EffectSpec> effects")
                .Append(index).Append(" = ")
                .Append(module.Effects is null
                    ? "global::System.ReadOnlySpan<global::Crusaders30XX.ECS.DataOriented.Definitions.EffectSpec>.Empty"
                    : moduleType + ".Effects")
                .AppendLine(";");
            source.Append("            global::System.ReadOnlySpan<global::Crusaders30XX.ECS.DataOriented.Definitions.ConditionSpec> conditions")
                .Append(index).Append(" = ")
                .Append(module.Conditions is null
                    ? "global::System.ReadOnlySpan<global::Crusaders30XX.ECS.DataOriented.Definitions.ConditionSpec>.Empty"
                    : moduleType + ".Conditions")
                .AppendLine(";");
        }

        source.Append("            s_definitions = new ").Append(definitionType).Append('[').Append(capacity).AppendLine("];");
        if (upgradeType is not null)
        {
            source.Append("            s_upgrades = new ").Append(upgradeType).Append('[').Append(capacity).AppendLine("];");
        }

        source.Append("            s_defined = new bool[").Append(capacity).AppendLine("];");
        source.Append("            s_entries = new global::Crusaders30XX.ECS.DataOriented.Definitions.DefinitionCatalogEntry[")
            .Append(capacity).AppendLine("];");
        source.Append("            s_effects = new global::Crusaders30XX.ECS.DataOriented.Definitions.EffectSpec[")
            .Append(string.Join(" + ", Enumerable.Range(0, modules.Count).Select(index => "effects" + index + ".Length")))
            .AppendLine("];");
        source.Append("            s_conditions = new global::Crusaders30XX.ECS.DataOriented.Definitions.ConditionSpec[")
            .Append(string.Join(" + ", Enumerable.Range(0, modules.Count).Select(index => "conditions" + index + ".Length")))
            .AppendLine("];");
        source.Append("            s_debugMetadata = new global::Crusaders30XX.ECS.DataOriented.Definitions.DefinitionDebugMetadata<")
            .Append(idType).Append(" >[").Append(modules.Count).AppendLine("];");
        source.AppendLine("            int effectOffset = 0;");
        source.AppendLine("            int conditionOffset = 0;");

        for (int index = 0; index < modules.Count; index++)
        {
            DefinitionModuleModel module = modules[index];
            string moduleType = SymbolFacts.TypeReference(module.Symbol);
            string enumValue = idType + "." + module.IdName;
            source.AppendLine();
            source.Append("            const int index").Append(index).Append(" = ").Append(module.Value).AppendLine(";");
            source.Append("            s_definitions[index").Append(index).Append("] = ").Append(moduleType).AppendLine(".Definition;");
            if (upgradeType is not null && module.Upgrade is not null)
            {
                source.Append("            s_upgrades[index").Append(index).Append("] = ").Append(moduleType).AppendLine(".Upgrade;");
            }

            source.Append("            s_defined[index").Append(index).AppendLine("] = true;");
            source.Append("            effects").Append(index)
                .AppendLine(".CopyTo(new global::System.Span<global::Crusaders30XX.ECS.DataOriented.Definitions.EffectSpec>(s_effects, effectOffset, effects" + index + ".Length));");
            source.Append("            conditions").Append(index)
                .AppendLine(".CopyTo(new global::System.Span<global::Crusaders30XX.ECS.DataOriented.Definitions.ConditionSpec>(s_conditions, conditionOffset, conditions" + index + ".Length));");
            source.Append("            s_entries[index").Append(index).Append("] = new global::Crusaders30XX.ECS.DataOriented.Definitions.DefinitionCatalogEntry(true, effectOffset, effects")
                .Append(index).Append(".Length, conditionOffset, conditions").Append(index).Append(".Length, ")
                .Append(module.Handler is null ? "false" : "true").AppendLine(");");
            source.Append("            s_debugMetadata[").Append(index).Append("] = new global::Crusaders30XX.ECS.DataOriented.Definitions.DefinitionDebugMetadata<")
                .Append(idType).Append(">(").Append(enumValue).Append(", \"")
                .Append(SymbolFacts.Escape(module.IdName)).Append("\", \"")
                .Append(SymbolFacts.Escape(SymbolFacts.MetadataName(module.Symbol))).Append("\", effectOffset, effects")
                .Append(index).Append(".Length, conditionOffset, conditions").Append(index).Append(".Length, ")
                .Append(module.Handler is null ? "false" : "true").AppendLine(");");
            source.Append("            effectOffset += effects").Append(index).AppendLine(".Length;");
            source.Append("            conditionOffset += conditions").Append(index).AppendLine(".Length;");
        }

        source.AppendLine("        }");
        source.AppendLine();
        source.Append("        public const int Capacity = ").Append(capacity).AppendLine(";");
        source.Append("        public const int DefinitionCount = ").Append(modules.Count).AppendLine(";");
        source.AppendLine();
        source.Append("        public static global::System.ReadOnlySpan<").Append(definitionType).AppendLine("> Definitions => s_definitions;");
        if (upgradeType is not null)
        {
            source.Append("        public static global::System.ReadOnlySpan<").Append(upgradeType).AppendLine("> Upgrades => s_upgrades;");
        }

        source.Append("        public static global::System.ReadOnlySpan<global::Crusaders30XX.ECS.DataOriented.Definitions.DefinitionDebugMetadata<")
            .Append(idType).AppendLine(">> DebugMetadata => s_debugMetadata;");
        source.AppendLine();
        AppendCatalogAccessors(source, idType, definitionType, upgradeType);
        if (handlerContextType is not null)
        {
            AppendDispatch(source, modules, idType, handlerContextType);
        }

        source.AppendLine("    }");
        source.AppendLine("}");
        return source.ToString();
    }

    private static void AppendCatalogAccessors(
        StringBuilder source,
        string idType,
        string definitionType,
        string? upgradeType)
    {
        source.Append("        public static bool IsDefined(").Append(idType).AppendLine(" id)");
        source.AppendLine("        {");
        source.AppendLine("            int index = (int)(ushort)id;");
        source.AppendLine("            return (uint)index < (uint)s_defined.Length && s_defined[index];");
        source.AppendLine("        }");
        source.AppendLine();
        source.Append("        public static ref readonly ").Append(definitionType).Append(" GetDefinition(").Append(idType).AppendLine(" id)");
        source.AppendLine("        {");
        source.AppendLine("            int index = CheckedIndex(id);");
        source.AppendLine("            return ref s_definitions[index];");
        source.AppendLine("        }");
        source.AppendLine();
        if (upgradeType is not null)
        {
            source.Append("        public static ref readonly ").Append(upgradeType).Append(" GetUpgrade(").Append(idType).AppendLine(" id)");
            source.AppendLine("        {");
            source.AppendLine("            int index = CheckedIndex(id);");
            source.AppendLine("            return ref s_upgrades[index];");
            source.AppendLine("        }");
            source.AppendLine();
        }

        source.Append("        public static global::System.ReadOnlySpan<global::Crusaders30XX.ECS.DataOriented.Definitions.EffectSpec> GetEffects(")
            .Append(idType).AppendLine(" id)");
        source.AppendLine("        {");
        source.AppendLine("            ref readonly global::Crusaders30XX.ECS.DataOriented.Definitions.DefinitionCatalogEntry entry = ref s_entries[CheckedIndex(id)];");
        source.AppendLine("            return new global::System.ReadOnlySpan<global::Crusaders30XX.ECS.DataOriented.Definitions.EffectSpec>(s_effects, entry.EffectOffset, entry.EffectCount);");
        source.AppendLine("        }");
        source.AppendLine();
        source.Append("        public static global::System.ReadOnlySpan<global::Crusaders30XX.ECS.DataOriented.Definitions.ConditionSpec> GetConditions(")
            .Append(idType).AppendLine(" id)");
        source.AppendLine("        {");
        source.AppendLine("            ref readonly global::Crusaders30XX.ECS.DataOriented.Definitions.DefinitionCatalogEntry entry = ref s_entries[CheckedIndex(id)];");
        source.AppendLine("            return new global::System.ReadOnlySpan<global::Crusaders30XX.ECS.DataOriented.Definitions.ConditionSpec>(s_conditions, entry.ConditionOffset, entry.ConditionCount);");
        source.AppendLine("        }");
        source.AppendLine();
        source.Append("        private static int CheckedIndex(").Append(idType).AppendLine(" id)");
        source.AppendLine("        {");
        source.AppendLine("            int index = (int)(ushort)id;");
        source.AppendLine("            if ((uint)index >= (uint)s_defined.Length || !s_defined[index])");
        source.AppendLine("            {");
        source.AppendLine("                throw new global::System.ArgumentOutOfRangeException(nameof(id), id, \"The definition ID is not present in this catalog.\");");
        source.AppendLine("            }");
        source.AppendLine();
        source.AppendLine("            return index;");
        source.AppendLine("        }");
        source.AppendLine();
    }

    private static void AppendDispatch(
        StringBuilder source,
        IReadOnlyList<DefinitionModuleModel> modules,
        string idType,
        string handlerContextType)
    {
        source.Append("        public static bool Dispatch(").Append(idType).Append(" id, ref ")
            .Append(handlerContextType).AppendLine(" context)");
        source.AppendLine("        {");
        source.AppendLine("            switch (id)");
        source.AppendLine("            {");
        foreach (DefinitionModuleModel module in modules.Where(module => module.Handler is not null))
        {
            source.Append("                case ").Append(idType).Append('.').Append(module.IdName).AppendLine(":");
            source.Append("                    ").Append(SymbolFacts.TypeReference(module.Symbol)).Append('.')
                .Append(module.Handler!.Name).AppendLine("(ref context);");
            source.AppendLine("                    return true;");
        }

        source.AppendLine("                default:");
        source.AppendLine("                    return false;");
        source.AppendLine("            }");
        source.AppendLine("        }");
        source.AppendLine();
    }

    private static IPropertySymbol? FindStaticProperty(INamedTypeSymbol symbol, string name) =>
        symbol.GetMembers(name).OfType<IPropertySymbol>().FirstOrDefault(property =>
            property.IsStatic && property.GetMethod is not null && property.SetMethod is null &&
            property.DeclaredAccessibility is not Accessibility.Private and not Accessibility.Protected);

    private static bool IsValidHandler(IMethodSymbol method) =>
        method.IsStatic && !method.IsGenericMethod && method.ReturnsVoid && method.Parameters.Length == 1 &&
        method.Parameters[0].RefKind == RefKind.Ref &&
        method.DeclaredAccessibility is not Accessibility.Private and not Accessibility.Protected;

    private static bool IsUShortEnum(INamedTypeSymbol type) =>
        type.TypeKind == TypeKind.Enum && type.EnumUnderlyingType?.SpecialType == SpecialType.System_UInt16;

    private static Dictionary<ushort, string> EnumMembers(INamedTypeSymbol type)
    {
        var result = new Dictionary<ushort, string>();
        foreach (IFieldSymbol field in type.GetMembers().OfType<IFieldSymbol>())
        {
            if (field.HasConstantValue && TryGetUShort(field.ConstantValue, out ushort value) && !result.ContainsKey(value))
            {
                result.Add(value, field.Name);
            }
        }

        return result;
    }

    private static bool TryGetUShort(object? value, out ushort result)
    {
        try
        {
            result = Convert.ToUInt16(value, CultureInfo.InvariantCulture);
            return true;
        }
        catch (Exception)
        {
            result = default;
            return false;
        }
    }

    private static Location AttributeLocation(AttributeData attribute, ISymbol fallback)
    {
        if (attribute.ApplicationSyntaxReference?.GetSyntax() is AttributeSyntax syntax)
        {
            return syntax.GetLocation();
        }

        return fallback.Locations.FirstOrDefault(location => location.IsInSource) ?? Location.None;
    }

    private static string CatalogClassName(INamedTypeSymbol idType)
    {
        string stem = idType.Name.EndsWith("Id", StringComparison.Ordinal)
            ? idType.Name.Substring(0, idType.Name.Length - 2)
            : idType.Name;
        return "Generated" + stem + "Catalog";
    }

    private static string CatalogHintName(INamedTypeSymbol idType) => CatalogClassName(idType) + ".g.cs";
}
