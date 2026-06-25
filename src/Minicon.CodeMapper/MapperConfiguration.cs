using System;
using System.Collections.Generic;
using System.Reflection;

namespace Minicon.CodeMapper;

/// <summary>
/// API-kompatibles Pendant zur AutoMapper-<c>MapperConfiguration</c>.
/// Da der Source Generator alle Mappings bereits zur Compile-Zeit registriert,
/// dient diese Klasse vor allem der Drop-in-Kompatibilität.
/// </summary>
public sealed class MapperConfiguration
{
    public MapperConfiguration(Action<IMapperConfigurationExpression> configure)
    {
        if (configure is null) throw new ArgumentNullException(nameof(configure));
        configure(new MapperConfigurationExpression());
    }

    /// <summary>Erzeugt einen <see cref="IMapper"/> auf Basis der generierten Mappings.</summary>
    public IMapper CreateMapper() => Mapper.Instance;

    /// <summary>
    /// Prüft, ob mindestens ein Mapping generiert wurde. Eine vollständige Member-Validierung
    /// erfolgt bereits zur Compile-Zeit durch den Generator (Diagnostics).
    /// </summary>
    public void AssertConfigurationIsValid()
    {
        if (MapperRegistry.Count == 0)
            throw new InvalidOperationException(
                "Es wurden keine Mappings generiert. Stelle sicher, dass mindestens ein Profile mit "
                + "CreateMap<,>() existiert und der Minicon.CodeMapper-Generator aktiv ist.");
    }
}

/// <summary>Konfigurations-Oberfläche, die an <see cref="MapperConfiguration"/> übergeben wird.</summary>
public interface IMapperConfigurationExpression
{
    void AddProfile<TProfile>() where TProfile : Profile, new();
    void AddProfile(Profile profile);
    void AddProfile(Type profileType);
    void AddMaps(params Assembly[] assemblies);
    void AddMaps(IEnumerable<Assembly> assemblies);
    void AddMaps(params Type[] markerTypes);
    IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>();
}

internal sealed class MapperConfigurationExpression : IMapperConfigurationExpression
{
    // Alle Methoden sind effektiv No-ops: Die Mappings stammen aus dem generierten Code.
    // Profiles werden dennoch instanziiert, um Drop-in-Verhalten und Nebenwirkungsfreiheit zu wahren.
    public void AddProfile<TProfile>() where TProfile : Profile, new() => _ = new TProfile();

    public void AddProfile(Profile profile) { }

    public void AddProfile(Type profileType) { }

    public void AddMaps(params Assembly[] assemblies) { }

    public void AddMaps(IEnumerable<Assembly> assemblies) { }

    public void AddMaps(params Type[] markerTypes) { }

    public IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>()
        => new MappingExpression<TSource, TDestination>();
}
