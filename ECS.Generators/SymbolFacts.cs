using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;

namespace Crusaders30XX.ECS.Generators;

internal static class SymbolFacts
{
    public const string ComponentMetadataName = "Crusaders30XX.ECS.DataOriented.Core.IComponent";
    public const string TagMetadataName = "Crusaders30XX.ECS.DataOriented.Core.ITag";
    public const string QueryAttributeMetadataName = "Crusaders30XX.ECS.DataOriented.Generated.EcsQueryAttribute";

    public static bool Implements(INamedTypeSymbol symbol, INamedTypeSymbol? interfaceSymbol)
    {
        return interfaceSymbol is not null && symbol.AllInterfaces.Any(candidate =>
            SymbolEqualityComparer.Default.Equals(candidate, interfaceSymbol));
    }

    public static bool ImplementsMetadataName(INamedTypeSymbol symbol, string metadataName)
    {
        return symbol.AllInterfaces.Any(candidate => MetadataName(candidate) == metadataName);
    }

    public static IFieldSymbol? FirstManagedField(INamedTypeSymbol symbol)
    {
        foreach (IFieldSymbol field in symbol.GetMembers().OfType<IFieldSymbol>())
        {
            if (!field.IsStatic && !field.Type.IsUnmanagedType)
            {
                return field;
            }
        }

        return null;
    }

    public static IFieldSymbol? FirstInstanceField(INamedTypeSymbol symbol)
    {
        return symbol.GetMembers().OfType<IFieldSymbol>().FirstOrDefault(field => !field.IsStatic);
    }

    public static bool IsGeneratorAccessible(INamedTypeSymbol symbol)
    {
        for (INamedTypeSymbol? current = symbol; current is not null; current = current.ContainingType)
        {
            if (current.DeclaredAccessibility is Accessibility.Private or Accessibility.Protected or Accessibility.ProtectedAndInternal)
            {
                return false;
            }
        }

        return true;
    }

    public static bool IsPartial(INamedTypeSymbol symbol)
    {
        return symbol.DeclaringSyntaxReferences.Any(reference =>
            reference.GetSyntax() is TypeDeclarationSyntax declaration &&
            declaration.Modifiers.Any(SyntaxKind.PartialKeyword));
    }

    public static Location Location(INamedTypeSymbol symbol)
    {
        return symbol.Locations.FirstOrDefault(candidate => candidate.IsInSource) ?? Microsoft.CodeAnalysis.Location.None;
    }

    public static string MetadataName(INamedTypeSymbol symbol)
    {
        var containingTypes = new Stack<string>();
        for (INamedTypeSymbol? current = symbol; current is not null; current = current.ContainingType)
        {
            containingTypes.Push(current.MetadataName);
        }

        string typeName = string.Join("+", containingTypes);
        return symbol.ContainingNamespace.IsGlobalNamespace
            ? typeName
            : symbol.ContainingNamespace.ToDisplayString() + "." + typeName;
    }

    public static string TypeReference(ITypeSymbol symbol)
    {
        return symbol.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
    }

    public static string Escape(string value)
    {
        return value.Replace("\\", "\\\\").Replace("\"", "\\\"");
    }
}
