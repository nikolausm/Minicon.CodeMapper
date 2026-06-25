using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using System.Text;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;

namespace Minicon.CodeMapper.SourceGenerator;

/// <summary>
/// Incremental Source Generator: liest <c>Profile</c>-Subklassen, wertet deren
/// <c>CreateMap&lt;,&gt;()</c>-Deklarationen zur Compile-Zeit aus und erzeugt statische,
/// reflection-freie Mapping-Methoden samt globaler Registrierung per <c>[ModuleInitializer]</c>.
/// </summary>
[Generator]
public sealed class MapperGenerator : IIncrementalGenerator
{
    private const string ProfileFullName = "Minicon.CodeMapper.Profile";

    private static readonly DiagnosticDescriptor UnmappedMember = new(
        id: "MINI001",
        title: "Ziel-Member ohne Quelle",
        messageFormat: "Member '{0}.{1}' konnte keiner Quelle zugeordnet werden und bleibt auf dem Standardwert.",
        category: "Minicon.CodeMapper",
        defaultSeverity: DiagnosticSeverity.Info,
        isEnabledByDefault: true);

    private static readonly SymbolDisplayFormat Fqn = SymbolDisplayFormat.FullyQualifiedFormat;

    public void Initialize(IncrementalGeneratorInitializationContext context)
    {
        var profiles = context.SyntaxProvider.CreateSyntaxProvider(
                predicate: static (node, _) => node is ClassDeclarationSyntax { BaseList: not null },
                transform: static (ctx, _) => GetProfileSymbol(ctx))
            .Where(static s => s is not null)
            .Select(static (s, _) => s!);

        var combined = context.CompilationProvider.Combine(profiles.Collect());

        context.RegisterSourceOutput(combined, static (spc, pair) => Execute(spc, pair.Left, pair.Right));
    }

    private static INamedTypeSymbol? GetProfileSymbol(GeneratorSyntaxContext ctx)
    {
        var decl = (ClassDeclarationSyntax)ctx.Node;
        if (ctx.SemanticModel.GetDeclaredSymbol(decl) is not INamedTypeSymbol symbol)
            return null;
        return InheritsProfile(symbol) ? symbol : null;
    }

    private static bool InheritsProfile(INamedTypeSymbol symbol)
    {
        for (var t = symbol.BaseType; t is not null; t = t.BaseType)
            if (t.ToDisplayString() == ProfileFullName)
                return true;
        return false;
    }

    private static void Execute(SourceProductionContext spc, Compilation compilation,
        ImmutableArray<INamedTypeSymbol> profiles)
    {
        if (profiles.IsDefaultOrEmpty)
            return;

        var distinctProfiles = profiles.Distinct(SymbolEqualityComparer.Default).Cast<INamedTypeSymbol>();

        // 1) Rohe Mapping-Deklarationen sammeln.
        var decls = new List<MapDecl>();
        foreach (var profile in distinctProfiles)
        {
            foreach (var syntaxRef in profile.DeclaringSyntaxReferences)
            {
                if (syntaxRef.GetSyntax() is not ClassDeclarationSyntax classDecl)
                    continue;
                var sem = compilation.GetSemanticModel(classDecl.SyntaxTree);
                CollectFromClass(classDecl, sem, decls);
            }
        }

        if (decls.Count == 0)
            return;

        // 2) Deduplizieren (erste Deklaration gewinnt) + bekannte Paare indizieren.
        var unique = new List<MapDecl>();
        var seen = new HashSet<string>();
        foreach (var d in decls)
            if (seen.Add(d.Key))
                unique.Add(d);

        var knownPairs = new HashSet<string>(unique.Select(d => d.Key));

        // 3) Nach Syntaxbaum gruppieren, damit die using-Direktiven des Profils
        //    (für benutzerdefinierte MapFrom-/ConvertUsing-Ausdrücke) wiederverwendet werden.
        var groups = unique.GroupBy(d => d.Tree).ToList();

        for (var i = 0; i < groups.Count; i++)
        {
            var group = groups[i];
            var source = EmitGroup(i, group.Key, group.ToList(), compilation, knownPairs, spc);
            spc.AddSource($"MiniconMaps_{i}.g.cs", SourceText.From(source, Encoding.UTF8));
        }
    }

    private static void CollectFromClass(ClassDeclarationSyntax classDecl, SemanticModel sem, List<MapDecl> decls)
    {
        foreach (var inv in classDecl.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            if (InvokedName(inv) != "CreateMap")
                continue;
            if (sem.GetSymbolInfo(inv).Symbol is not IMethodSymbol method)
                continue;
            if (method.ContainingType?.ToDisplayString() != ProfileFullName)
                continue;
            if (method.TypeArguments.Length != 2)
                continue;
            if (method.TypeArguments[0] is not INamedTypeSymbol srcType ||
                method.TypeArguments[1] is not INamedTypeSymbol destType)
                continue;

            var decl = new MapDecl(srcType, destType, inv.SyntaxTree);
            WalkChain(inv, decl);
            decls.Add(decl);

            if (decl.Reverse)
                decls.Add(new MapDecl(destType, srcType, inv.SyntaxTree));
        }
    }

    private static void WalkChain(InvocationExpressionSyntax createMap, MapDecl decl)
    {
        ExpressionSyntax current = createMap;
        while (current.Parent is MemberAccessExpressionSyntax ma && ma.Expression == current &&
               ma.Parent is InvocationExpressionSyntax outer)
        {
            var name = ma.Name.Identifier.Text;
            switch (name)
            {
                case "ForMember":
                    ParseForMember(outer.ArgumentList, decl);
                    break;
                case "ReverseMap":
                    decl.Reverse = true;
                    break;
                case "ConvertUsing":
                    decl.ConvertLambda = FirstLambda(outer.ArgumentList);
                    break;
            }

            current = outer;
        }
    }

    private static void ParseForMember(ArgumentListSyntax args, MapDecl decl)
    {
        if (args.Arguments.Count < 2)
            return;
        if (args.Arguments[0].Expression is not LambdaExpressionSyntax destLambda)
            return;
        var memberName = ExtractMemberName(LambdaBody(destLambda));
        if (memberName is null)
            return;
        if (args.Arguments[1].Expression is not LambdaExpressionSyntax optLambda)
            return;

        var cfg = new MemberCfg();
        foreach (var inv in optLambda.DescendantNodes().OfType<InvocationExpressionSyntax>())
        {
            var name = InvokedName(inv);
            if (name == "Ignore")
            {
                cfg.Ignore = true;
            }
            else if (name == "MapFrom" && inv.ArgumentList.Arguments.Count >= 1 &&
                     inv.ArgumentList.Arguments[0].Expression is LambdaExpressionSyntax mapFromLambda)
            {
                cfg.MapFromLambda = mapFromLambda;
            }
        }

        decl.Members[memberName] = cfg;
    }

    private static string EmitGroup(int index, SyntaxTree tree, List<MapDecl> maps, Compilation compilation,
        HashSet<string> knownPairs, SourceProductionContext spc)
    {
        var usings = CollectUsings(tree);
        var className = $"MiniconMaps_{index}";
        var sb = new StringBuilder();

        sb.AppendLine("// <auto-generated/>");
        sb.AppendLine("#nullable enable");
        sb.AppendLine("#pragma warning disable");
        foreach (var u in usings)
            sb.AppendLine(u);
        sb.AppendLine();
        sb.AppendLine("namespace Minicon.CodeMapper.Generated");
        sb.AppendLine("{");
        sb.AppendLine($"    internal static class {className}");
        sb.AppendLine("    {");

        for (var i = 0; i < maps.Count; i++)
            EmitMethod(sb, i, maps[i], compilation, knownPairs, spc);

        // ModuleInitializer: registriert alle Methoden dieser Gruppe.
        sb.AppendLine("        [global::System.Runtime.CompilerServices.ModuleInitializer]");
        sb.AppendLine("        internal static void Register()");
        sb.AppendLine("        {");
        for (var i = 0; i < maps.Count; i++)
        {
            var src = maps[i].Source.ToDisplayString(Fqn);
            var dst = maps[i].Destination.ToDisplayString(Fqn);
            sb.AppendLine($"            global::Minicon.CodeMapper.MapperRegistry.Register<{src}, {dst}>(Map_{i});");
        }
        sb.AppendLine("        }");

        sb.AppendLine("    }");
        sb.AppendLine("}");
        return sb.ToString();
    }

    private static void EmitMethod(StringBuilder sb, int methodIndex, MapDecl map, Compilation compilation,
        HashSet<string> knownPairs, SourceProductionContext spc)
    {
        var srcFqn = map.Source.ToDisplayString(Fqn);
        var dstFqn = map.Destination.ToDisplayString(Fqn);

        sb.AppendLine($"        internal static {dstFqn} Map_{methodIndex}({srcFqn} source, global::Minicon.CodeMapper.IMapper mapper)");
        sb.AppendLine("        {");
        sb.AppendLine("            if (source is null) return default!;");

        // ConvertUsing: vollständig benutzerdefiniert.
        if (map.ConvertLambda is not null)
        {
            sb.AppendLine($"            return new global::System.Func<{srcFqn}, {dstFqn}>({map.ConvertLambda})(source);");
            sb.AppendLine("        }");
            sb.AppendLine();
            return;
        }

        var assignments = BuildAssignments(map, compilation, srcFqn, knownPairs, spc);

        sb.AppendLine($"            var dest = new {dstFqn}");
        sb.AppendLine("            {");
        foreach (var a in assignments)
            sb.AppendLine($"                {a},");
        sb.AppendLine("            };");
        sb.AppendLine("            return dest;");
        sb.AppendLine("        }");
        sb.AppendLine();
    }

    private static List<string> BuildAssignments(MapDecl map, Compilation compilation, string srcFqn,
        HashSet<string> knownPairs, SourceProductionContext spc)
    {
        var result = new List<string>();

        foreach (var destMember in GetSettableMembers(map.Destination))
        {
            var name = destMember.Name;
            var destType = MemberType(destMember);

            if (map.Members.TryGetValue(name, out var cfg))
            {
                if (cfg.Ignore)
                    continue;
                if (cfg.MapFromLambda is not null)
                {
                    var memberFqn = destType.ToDisplayString(Fqn);
                    result.Add($"{name} = new global::System.Func<{srcFqn}, {memberFqn}>({cfg.MapFromLambda})(source)");
                    continue;
                }
            }

            var srcMember = FindSourceMember(map.Source, name);
            if (srcMember is null)
            {
                spc.ReportDiagnostic(Diagnostic.Create(UnmappedMember, map.Destination.Locations.FirstOrDefault(),
                    map.Destination.Name, name));
                continue;
            }

            var srcType = MemberType(srcMember);
            var access = $"source.{srcMember.Name}";

            // a) direkt zuweisbar (gleich oder implizit konvertierbar)
            var conv = compilation.ClassifyConversion(srcType, destType);
            if (conv.IsIdentity || conv.IsImplicit)
            {
                result.Add($"{name} = {access}");
                continue;
            }

            // b) Nullable<T> -> T
            if (srcType is INamedTypeSymbol { OriginalDefinition.SpecialType: SpecialType.System_Nullable_T } nsrc &&
                SymbolEqualityComparer.Default.Equals(nsrc.TypeArguments[0], destType))
            {
                result.Add($"{name} = {access}.GetValueOrDefault()");
                continue;
            }

            // c) verschachteltes Objekt oder Collection -> Laufzeit-Mapper
            if (knownPairs.Contains(Key(srcType, destType)) || IsEnumerable(destType))
            {
                var destFqn = destType.ToDisplayString(Fqn);
                result.Add($"{name} = mapper.Map<{destFqn}>({access})");
                continue;
            }

            spc.ReportDiagnostic(Diagnostic.Create(UnmappedMember, map.Destination.Locations.FirstOrDefault(),
                map.Destination.Name, name));
        }

        return result;
    }

    // ---- Symbol-Helfer -------------------------------------------------------

    private static IEnumerable<ISymbol> GetSettableMembers(INamedTypeSymbol type)
    {
        var seen = new HashSet<string>();
        for (var t = type; t is not null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
        {
            foreach (var m in t.GetMembers())
            {
                if (m.IsStatic || !seen.Add(m.Name))
                    continue;
                switch (m)
                {
                    case IPropertySymbol { Parameters.Length: 0, DeclaredAccessibility: Accessibility.Public } p
                        when p.SetMethod is { DeclaredAccessibility: Accessibility.Public }:
                        yield return p;
                        break;
                    case IFieldSymbol { DeclaredAccessibility: Accessibility.Public, IsConst: false, IsReadOnly: false } f
                        when !f.IsImplicitlyDeclared:
                        yield return f;
                        break;
                }
            }
        }
    }

    private static ISymbol? FindSourceMember(INamedTypeSymbol type, string name)
    {
        ISymbol? caseInsensitive = null;
        for (var t = type; t is not null && t.SpecialType != SpecialType.System_Object; t = t.BaseType)
        {
            foreach (var m in t.GetMembers())
            {
                if (m.IsStatic || m.DeclaredAccessibility != Accessibility.Public)
                    continue;
                var readable = m switch
                {
                    IPropertySymbol { Parameters.Length: 0 } p when p.GetMethod is not null => true,
                    IFieldSymbol => true,
                    _ => false
                };
                if (!readable)
                    continue;
                if (m.Name == name)
                    return m;
                if (caseInsensitive is null && string.Equals(m.Name, name, System.StringComparison.OrdinalIgnoreCase))
                    caseInsensitive = m;
            }
        }

        return caseInsensitive;
    }

    private static ITypeSymbol MemberType(ISymbol symbol) => symbol switch
    {
        IPropertySymbol p => p.Type,
        IFieldSymbol f => f.Type,
        _ => throw new System.InvalidOperationException()
    };

    private static bool IsEnumerable(ITypeSymbol type)
    {
        if (type.SpecialType == SpecialType.System_String)
            return false;
        if (type is IArrayTypeSymbol)
            return true;
        return type.AllInterfaces.Any(i => i.OriginalDefinition.SpecialType == SpecialType.System_Collections_Generic_IEnumerable_T);
    }

    // ---- Syntax-Helfer -------------------------------------------------------

    private static string Key(ITypeSymbol s, ITypeSymbol d) => $"{s.ToDisplayString(Fqn)}|{d.ToDisplayString(Fqn)}";

    private static string? InvokedName(InvocationExpressionSyntax inv) => inv.Expression switch
    {
        MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
        GenericNameSyntax gn => gn.Identifier.Text,
        IdentifierNameSyntax id => id.Identifier.Text,
        _ => null
    };

    private static LambdaExpressionSyntax? FirstLambda(ArgumentListSyntax args)
        => args.Arguments.Count > 0 ? args.Arguments[0].Expression as LambdaExpressionSyntax : null;

    private static ExpressionSyntax? LambdaBody(LambdaExpressionSyntax lambda) => lambda switch
    {
        SimpleLambdaExpressionSyntax s => s.Body as ExpressionSyntax,
        ParenthesizedLambdaExpressionSyntax p => p.Body as ExpressionSyntax,
        _ => null
    };

    private static string? ExtractMemberName(ExpressionSyntax? body) => body switch
    {
        MemberAccessExpressionSyntax ma => ma.Name.Identifier.Text,
        _ => null
    };

    private static List<string> CollectUsings(SyntaxTree tree)
    {
        var root = tree.GetRoot();
        var result = new List<string>();
        var seen = new HashSet<string>();
        foreach (var u in root.DescendantNodes().OfType<UsingDirectiveSyntax>())
        {
            var text = u.ToString();
            if (seen.Add(text))
                result.Add(text);
        }

        return result;
    }
}

// ---- Modelle ----------------------------------------------------------------

internal sealed class MapDecl
{
    public MapDecl(INamedTypeSymbol source, INamedTypeSymbol destination, SyntaxTree tree)
    {
        Source = source;
        Destination = destination;
        Tree = tree;
    }

    public INamedTypeSymbol Source { get; }
    public INamedTypeSymbol Destination { get; }
    public SyntaxTree Tree { get; }
    public bool Reverse { get; set; }
    public LambdaExpressionSyntax? ConvertLambda { get; set; }
    public Dictionary<string, MemberCfg> Members { get; } = new();

    public string Key => $"{Source.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}|{Destination.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat)}";
}

internal sealed class MemberCfg
{
    public bool Ignore { get; set; }
    public LambdaExpressionSyntax? MapFromLambda { get; set; }
}
