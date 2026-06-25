using System;
using System.Reflection;
using Microsoft.Extensions.DependencyInjection;

namespace Minicon.CodeMapper;

/// <summary>
/// DI-Registrierung. Pendant zu AutoMappers <c>AddAutoMapper(...)</c>.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Registriert <see cref="IMapper"/> als Singleton. Die Assembly-Parameter dienen nur der
    /// API-Kompatibilität – die Mappings werden vom Source Generator pro Assembly automatisch
    /// per <c>[ModuleInitializer]</c> registriert.
    /// </summary>
    public static IServiceCollection AddMiniconMapper(this IServiceCollection services, params Assembly[] assemblies)
    {
        EnsureAssembliesLoaded(assemblies);
        services.AddSingleton<IMapper>(Mapper.Instance);
        return services;
    }

    /// <summary>Registriert <see cref="IMapper"/> und führt zusätzlich eine (optionale) Konfiguration aus.</summary>
    public static IServiceCollection AddMiniconMapper(this IServiceCollection services, Action<IMapperConfigurationExpression> configure)
    {
        _ = new MapperConfiguration(configure);
        services.AddSingleton<IMapper>(Mapper.Instance);
        return services;
    }

    /// <summary>Registriert <see cref="IMapper"/> und lädt die Assemblies der angegebenen Marker-Typen.</summary>
    public static IServiceCollection AddMiniconMapper(this IServiceCollection services, params Type[] markerTypes)
    {
        foreach (var t in markerTypes)
            EnsureAssemblyLoaded(t.Assembly);
        services.AddSingleton<IMapper>(Mapper.Instance);
        return services;
    }

    private static void EnsureAssembliesLoaded(Assembly[] assemblies)
    {
        if (assemblies is null) return;
        foreach (var a in assemblies)
            EnsureAssemblyLoaded(a);
    }

    // Berührt die Assembly, damit ihr [ModuleInitializer] (und damit die Map-Registrierung)
    // garantiert ausgeführt wurde, falls noch kein Typ daraus geladen war.
    private static void EnsureAssemblyLoaded(Assembly assembly)
        => System.Runtime.CompilerServices.RuntimeHelpers.RunModuleConstructor(assembly.ManifestModule.ModuleHandle);
}
