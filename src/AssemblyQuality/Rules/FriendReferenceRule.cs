using System.Diagnostics.CodeAnalysis;
using System.Reflection;
using System.Reflection.Metadata;
using System.Reflection.Metadata.Ecma335;
using System.Reflection.PortableExecutable;
using System.Runtime.InteropServices;
using System.Runtime.Loader;

namespace Bennewitz.Ninja.AssemblyQuality.Rules;

/// <summary>
/// Every reference into another assembly resolves, and every one into an internal is still granted.
/// </summary>
/// <remarks>
/// <para>
/// ⭐ <b>Run it where a newer dependency meets an older build: at the dependency's release.</b> An
/// assembly compiles against the versions it references, so its own tests cannot see a break: a
/// removed member is a compile error there first. The break happens in an application that resolves
/// a NEWER dependency under an assembly built against an older one. So a provider's release check
/// loads the consumers already published against it beside its new build, and scans the consumers.
/// </para>
/// <para>
/// ⭐ <b>The runtime's own resolution, not a reimplementation.</b> Each type and member reference is
/// resolved through <see cref="Module.ResolveType(int)"/> and <see cref="Module.ResolveMember(int)"/>,
/// which fail with exactly the exception the call would. Measured: a removed member,
/// <see cref="MissingMethodException"/>; a removed type, <see cref="TypeLoadException"/>, once for the
/// type and again for every member reference through it, so it is reported once.
/// </para>
/// <para>
/// ⛔ <b>Resolution does not check access.</b> A reference to an internal whose grant was withdrawn
/// resolves perfectly and then fails at the call with <see cref="MethodAccessException"/>. So a
/// reference that resolves to a non-public member is checked against the provider's
/// <c>InternalsVisibleTo</c> grants, here.
/// </para>
/// <para>
/// ⚠ <b>Examined: references into assemblies outside the scan and outside the shared framework.</b>
/// Assemblies in the scan are built together and cannot skew, and the framework is the runtime's. A
/// shared framework is recognised by its location under the runtime's shared-frameworks folder, so
/// ASP.NET Core and WindowsDesktop count as well as <c>Microsoft.NETCore.App</c>.
/// </para>
/// <para>
/// ⚠ <b>References resolve in the scanned assembly's own load context</b>, which is how a release
/// check puts a published consumer beside the new build. An assembly with no file behind it has no
/// metadata to read, and is named in <see cref="AssemblyRuleResult.Skipped"/>.
/// </para>
/// </remarks>
public sealed class FriendReferenceRule : IAssemblyRule
{
    private static readonly string SharedFrameworks = Path.GetFullPath(
        Path.Combine(RuntimeEnvironment.GetRuntimeDirectory(), "..", ".."));

    /// <inheritdoc />
    public string Id => "BNAQ1006";

    /// <inheritdoc />
    public string Summary => "Every reference into another assembly resolves, and every one into an internal is still granted.";

    /// <inheritdoc />
    [RequiresUnreferencedCode(AssemblyScanContext.TrimMessage)]
    [UnconditionalSuppressMessage("SingleFile", "IL3000", Justification = "An assembly with no file behind it is named in Skipped rather than read.")]
    public AssemblyRuleResult Analyze(AssemblyScanContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        List<AssemblyFinding> findings = [];
        List<string> skipped = [];
        int inspected = 0;

        HashSet<string> inScan = new(
            context.Assemblies.Select(a => a.GetName().Name).OfType<string>(),
            StringComparer.OrdinalIgnoreCase);

        foreach (Assembly assembly in context.Assemblies)
        {
            string name = assembly.GetName().Name ?? "?";

            if (string.IsNullOrEmpty(assembly.Location))
            {
                skipped.Add($"{name}: has no file behind it, so its references could not be read or examined.");
                continue;
            }

            using PEReader pe = new(File.OpenRead(assembly.Location));
            Scan scan = new(this, assembly, name, pe.GetMetadataReader(), inScan, findings, skipped);
            inspected += scan.Run();
        }

        return new AssemblyRuleResult(findings, inspected) { Skipped = [.. skipped.Distinct(StringComparer.Ordinal)] };
    }

    /// <summary>One scanned assembly's references, examined against what loads beside it.</summary>
    private sealed class Scan(
        FriendReferenceRule rule,
        Assembly consumer,
        string consumerName,
        MetadataReader metadata,
        HashSet<string> inScan,
        List<AssemblyFinding> findings,
        List<string> skipped)
    {
        private readonly Dictionary<AssemblyReferenceHandle, Assembly?> _providers = [];
        private readonly Dictionary<Assembly, bool> _grants = [];
        private readonly HashSet<TypeReferenceHandle> _brokenTypes = [];

        [RequiresUnreferencedCode(AssemblyScanContext.TrimMessage)]
        public int Run()
        {
            AssemblyLoadContext loadContext = AssemblyLoadContext.GetLoadContext(consumer) ?? AssemblyLoadContext.Default;
            foreach (AssemblyReferenceHandle handle in metadata.AssemblyReferences)
            {
                _providers[handle] = Examined(loadContext, metadata.GetAssemblyReference(handle).GetAssemblyName());
            }

            int inspected = 0;
            Module module = consumer.ManifestModule;

            foreach (TypeReferenceHandle handle in metadata.TypeReferences)
            {
                if (ProviderOf(handle) is not { } provider)
                {
                    continue;
                }

                inspected++;
                try
                {
                    Type type = module.ResolveType(MetadataTokens.GetToken(handle));
                    if ((type.IsNotPublic || type.IsNestedAssembly || type.IsNestedFamANDAssem) && !Grants(provider))
                    {
                        Report(provider, TypeName(handle), nameof(TypeAccessException), withdrawn: true);
                    }
                }
                catch (TypeLoadException)
                {
                    _brokenTypes.Add(handle);
                    Report(provider, TypeName(handle), nameof(TypeLoadException), withdrawn: false);
                }
            }

            foreach (MemberReferenceHandle handle in metadata.MemberReferences)
            {
                MemberReference reference = metadata.GetMemberReference(handle);
                if (DeclaringTypeOf(reference.Parent) is not { } parent || _brokenTypes.Contains(parent) || ProviderOf(parent) is not { } provider)
                {
                    continue;
                }

                inspected++;
                string subject = TypeName(parent) + "." + metadata.GetString(reference.Name);
                try
                {
                    MemberInfo member = module.ResolveMember(MetadataTokens.GetToken(handle))!;
                    if (IsInternal(member) && !Grants(provider))
                    {
                        Report(provider, subject, member is FieldInfo ? nameof(FieldAccessException) : nameof(MethodAccessException), withdrawn: true);
                    }
                }
                catch (MissingMemberException ex)
                {
                    Report(provider, subject, ex.GetType().Name, withdrawn: false);
                }
                catch (TypeLoadException)
                {
                    _brokenTypes.Add(parent);
                    Report(provider, TypeName(parent), nameof(TypeLoadException), withdrawn: false);
                }
            }

            return inspected;
        }

        /// <summary>
        /// The referenced assembly as it loads beside the consumer, or null when its references are not
        /// examined: in the scan, in the shared framework, or not loadable (named in Skipped).
        /// </summary>
        [RequiresUnreferencedCode(AssemblyScanContext.TrimMessage)]
        [UnconditionalSuppressMessage("SingleFile", "IL3000", Justification = "A provider with no file behind it is not under the shared-frameworks folder, which is the only question asked.")]
        private Assembly? Examined(AssemblyLoadContext loadContext, AssemblyName reference)
        {
            if (reference.Name is null || inScan.Contains(reference.Name))
            {
                return null;
            }

            Assembly provider;
            try
            {
                provider = loadContext.LoadFromAssemblyName(reference);
            }
            catch (Exception ex) when (AssemblyScanContext.IsLoadFailure(ex))
            {
                skipped.Add($"{consumerName}: reference {reference.Name} {reference.Version} would not load "
                    + $"({ex.GetType().Name}), so nothing referenced in it was examined.");
                return null;
            }

            bool framework = provider.Location.Length > 0
                && provider.Location.StartsWith(SharedFrameworks, StringComparison.OrdinalIgnoreCase);
            return framework ? null : provider;
        }

        /// <summary>The examined provider a type reference points into, following nested types outward.</summary>
        private Assembly? ProviderOf(TypeReferenceHandle handle)
        {
            EntityHandle scope = metadata.GetTypeReference(handle).ResolutionScope;
            return scope.Kind switch
            {
                HandleKind.AssemblyReference => _providers.GetValueOrDefault((AssemblyReferenceHandle)scope),
                HandleKind.TypeReference => ProviderOf((TypeReferenceHandle)scope),
                _ => null,
            };
        }

        /// <summary>
        /// The type reference a member reference's parent names: directly, or the generic type of a
        /// constructed one such as <c>List&lt;T&gt;.Add</c>.
        /// </summary>
        private TypeReferenceHandle? DeclaringTypeOf(EntityHandle parent)
        {
            if (parent.Kind == HandleKind.TypeReference)
            {
                return (TypeReferenceHandle)parent;
            }

            if (parent.Kind != HandleKind.TypeSpecification)
            {
                return null;
            }

            BlobReader signature = metadata.GetBlobReader(metadata.GetTypeSpecification((TypeSpecificationHandle)parent).Signature);
            if (signature.ReadSignatureTypeCode() != SignatureTypeCode.GenericTypeInstance)
            {
                return null;
            }

            _ = signature.ReadSignatureTypeCode();
            EntityHandle generic = signature.ReadTypeHandle();
            return generic.Kind == HandleKind.TypeReference ? (TypeReferenceHandle)generic : null;
        }

        private string TypeName(TypeReferenceHandle handle)
        {
            TypeReference type = metadata.GetTypeReference(handle);
            string name = metadata.GetString(type.Name);
            return type.ResolutionScope.Kind == HandleKind.TypeReference
                ? TypeName((TypeReferenceHandle)type.ResolutionScope) + "+" + name
                : metadata.GetString(type.Namespace) is { Length: > 0 } ns ? ns + "." + name : name;
        }

        private bool Grants(Assembly provider)
        {
            if (!_grants.TryGetValue(provider, out bool grants))
            {
                grants = provider.GetCustomAttributesData().Any(a =>
                    a.AttributeType.FullName == "System.Runtime.CompilerServices.InternalsVisibleToAttribute"
                    && a.ConstructorArguments is [{ Value: string grant }]
                    && string.Equals(grant.Split(',')[0].Trim(), consumerName, StringComparison.OrdinalIgnoreCase));
                _grants[provider] = grants;
            }

            return grants;
        }

        private static bool IsInternal(MemberInfo member) => member switch
        {
            MethodBase method => method.IsAssembly || method.IsFamilyAndAssembly,
            FieldInfo field => field.IsAssembly || field.IsFamilyAndAssembly,
            _ => false,
        };

        private void Report(Assembly provider, string subject, string failure, bool withdrawn)
        {
            string providerName = provider.GetName().Name ?? "?";
            string message = withdrawn
                ? $"{consumerName} uses {subject}, an internal of {providerName}, but the {providerName} that loads "
                    + $"beside it no longer grants {consumerName} its internals: the call fails at run time with "
                    + $"{failure}. Restore the grant, or release {consumerName} without the internal alongside it."
                : $"{consumerName} uses {subject}, which the {providerName} that loads beside it does not have: it "
                    + $"fails at run time with {failure}. An internal another assembly uses is a promise; restore "
                    + $"it, or release {consumerName} built against this version alongside it.";

            findings.Add(new AssemblyFinding(rule.Id, consumerName, $"{providerName}: {subject}", message));
        }
    }
}
