using System.Diagnostics.CodeAnalysis;
using System.Reflection;

namespace Bennewitz.Ninja.AssemblyQuality.Rules;

/// <summary>
/// No type from a leak-prone namespace appears in the public surface.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>A leaked implementation type is a dependency the consumer did not choose.</b> Return a
/// <c>JsonNode</c> from one public method and every consumer now binds against
/// <c>System.Text.Json</c> — its version, its behaviour, its breaking changes — whether or not they
/// serialize anything. Swapping the serializer later is then a breaking change to an API that was
/// never about serialization.
/// </para>
/// <para>
/// ⚠ <b>The default set is curated, not exhaustive.</b> These are the namespaces that leak in
/// practice; adding one is a behaviour change for every consumer, so it is public API and growth
/// belongs at the call site. Pass your own — the type most likely to leak from YOUR library is one
/// no general list will ever name. <see cref="Only"/> takes your list in place of the default one.
/// </para>
/// <para>
/// ⛔ Generic arguments are unwrapped. A <c>Task&lt;JsonNode&gt;</c> leaks exactly as much as a
/// bare one, and checking only the outer type is how this rule would read clean on the shape it
/// most often takes.
/// </para>
/// <para>
/// ⭐ <b><see cref="AssemblyRuleResult.Inspected"/> counts only where a leak is possible.</b> A
/// signature can name a type only from the assembly itself or from one it directly references.
/// When none of those exports a covered namespace, the predicate cannot be satisfied and the
/// assembly contributes zero — so the default set over an assembly that references no JSON library
/// reports that it asked nothing, rather than thousands of slots walked past.
/// </para>
/// </remarks>
public sealed class SurfaceLeakRule : IAssemblyRule
{
    /// <summary>Namespaces whose types are classic accidental exports.</summary>
    public static IReadOnlyCollection<string> LeakProneNamespaces { get; } =
    [
        "System.Text.Json.Nodes",
        "Newtonsoft.Json.Linq",
    ];

    private readonly string[] _namespaces;

    /// <summary>Covers <see cref="LeakProneNamespaces"/>.</summary>
    public SurfaceLeakRule()
        : this(Distinct(LeakProneNamespaces))
    {
    }

    /// <summary>Covers <see cref="LeakProneNamespaces"/> plus <paramref name="additionalNamespaces"/>.</summary>
    /// <param name="additionalNamespaces">
    /// Namespace prefixes whose types must not reach the public surface — your serializer, your
    /// UI framework, your internal model namespace.
    /// </param>
    public SurfaceLeakRule(IEnumerable<string> additionalNamespaces)
        : this(Distinct(LeakProneNamespaces.Concat(
            additionalNamespaces ?? throw new ArgumentNullException(nameof(additionalNamespaces)))))
    {
    }

    private SurfaceLeakRule(string[] namespaces)
    {
        _namespaces = namespaces;
    }

    /// <inheritdoc />
    public string Id => "AQ1002";

    /// <inheritdoc />
    public string Summary => "No type from a leak-prone namespace appears in the public surface.";

    /// <summary>Covers <paramref name="namespaces"/> and nothing else — not <see cref="LeakProneNamespaces"/>.</summary>
    /// <param name="namespaces">
    /// Namespace prefixes whose types must not reach the public surface. Empty forbids nothing, and
    /// the rule then reports that it inspected nothing.
    /// </param>
    public static SurfaceLeakRule Only(IEnumerable<string> namespaces)
    {
        ArgumentNullException.ThrowIfNull(namespaces);
        return new SurfaceLeakRule(Distinct(namespaces));
    }

    /// <inheritdoc />
    [RequiresUnreferencedCode(AssemblyScanContext.TrimMessage)]
    public AssemblyRuleResult Analyze(AssemblyScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<AssemblyFinding> findings = [];
        int inspected = 0;

        foreach (Assembly assembly in context.Assemblies)
        {
            if (!CanLeak(assembly))
            {
                continue;
            }

            foreach ((Assembly owner, Type type) in AssemblyScanContext.Of(assembly).ExportedTypes())
            {
                foreach (MemberInfo member in type.GetMembers(BindingFlags.Public | BindingFlags.Instance | BindingFlags.Static | BindingFlags.DeclaredOnly))
                {
                    foreach (Type used in SignatureTypes(member))
                    {
                        inspected++;

                        if (Offender(used) is not { } offender)
                        {
                            continue;
                        }

                        findings.Add(new AssemblyFinding(
                            Id,
                            owner.GetName().Name ?? "?",
                            $"{type.FullName}.{member.Name}",
                            $"This member's signature names {offender.FullName}, so every consumer binds "
                            + "against that package whether they use it or not, and replacing it later "
                            + "becomes a breaking change to an API that was never about it. Expose your "
                            + "own type instead."));
                    }
                }
            }
        }

        return new AssemblyRuleResult(findings, inspected);
    }

    /// <summary>
    /// Whether any covered namespace exists where this assembly's signatures could reach it: in the
    /// assembly itself, or in one it directly references.
    /// </summary>
    [RequiresUnreferencedCode(AssemblyScanContext.TrimMessage)]
    private bool CanLeak(Assembly assembly)
    {
        if (_namespaces.Length == 0)
        {
            return false;
        }

        IEnumerable<string> own = AssemblyScanContext.Of(assembly).ExportedTypes()
            .Select(pair => pair.Type.Namespace)
            .OfType<string>();

        return own.Concat(AssemblyScanContext.ReferencedNamespaces(assembly)).Any(Covered);
    }

    private static string[] Distinct(IEnumerable<string> namespaces) =>
        [.. namespaces.Distinct(StringComparer.Ordinal)];

    private bool Covered(string ns) => _namespaces.Any(n => ns.StartsWith(n, StringComparison.Ordinal));

    private Type? Offender(Type type)
    {
        foreach (Type candidate in Unwrap(type))
        {
            if (candidate.Namespace is { } ns && Covered(ns))
            {
                return candidate;
            }
        }

        return null;
    }

    /// <summary>The type itself and every generic argument, however deeply nested.</summary>
    private static IEnumerable<Type> Unwrap(Type type)
    {
        yield return type;

        if (type.IsArray && type.GetElementType() is { } element)
        {
            foreach (Type inner in Unwrap(element)) { yield return inner; }
        }

        foreach (Type argument in type.IsGenericType ? type.GetGenericArguments() : [])
        {
            foreach (Type inner in Unwrap(argument)) { yield return inner; }
        }
    }

    private static IEnumerable<Type> SignatureTypes(MemberInfo member)
    {
        switch (member)
        {
            case PropertyInfo p:
                yield return p.PropertyType;
                break;
            case FieldInfo f:
                yield return f.FieldType;
                break;
            case EventInfo e when e.EventHandlerType is { } handler:
                yield return handler;
                break;
            case MethodInfo m:
                yield return m.ReturnType;
                foreach (ParameterInfo parameter in m.GetParameters()) { yield return parameter.ParameterType; }

                break;
            case ConstructorInfo c:
                foreach (ParameterInfo parameter in c.GetParameters()) { yield return parameter.ParameterType; }

                break;
        }
    }
}
