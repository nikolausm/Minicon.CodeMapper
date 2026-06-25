using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;

namespace Minicon.CodeMapper;

/// <summary>
/// Standard-<see cref="IMapper"/>. Löst Mappings über die global registrierten,
/// vom Source Generator erzeugten Delegates auf und behandelt Collections sowie
/// Identitäts-Zuweisungen.
/// </summary>
public sealed class Mapper : IMapper
{
    /// <summary>Geteilte Instanz für statische bzw. DI-lose Nutzung.</summary>
    public static readonly Mapper Instance = new();

    public TDestination Map<TDestination>(object source)
        => (TDestination)MapCore(source, source?.GetType(), typeof(TDestination))!;

    public TDestination Map<TSource, TDestination>(TSource source)
        => (TDestination)MapCore(source, source?.GetType() ?? typeof(TSource), typeof(TDestination))!;

    public TDestination Map<TSource, TDestination>(TSource source, TDestination destination)
        => (TDestination)MapCore(source, source?.GetType() ?? typeof(TSource), typeof(TDestination))!;

    public object? Map(object? source, Type sourceType, Type destinationType)
        => MapCore(source, source?.GetType() ?? sourceType, destinationType);

    private object? MapCore(object? source, Type? sourceType, Type destinationType)
    {
        if (source is null || sourceType is null)
            return null;

        // 1) Exakt registriertes Mapping bevorzugen.
        if (MapperRegistry.TryGet(sourceType, destinationType, out var map))
            return map(source, this);

        // 2) Collection-Mapping (List<T>, T[], IEnumerable<T>, ...).
        if (TryGetEnumerableElementType(destinationType, out var destElementType)
            && source is IEnumerable enumerable
            && destinationType != typeof(string))
        {
            return MapEnumerable(enumerable, destElementType, destinationType);
        }

        // 3) Identität / direkte Zuweisbarkeit (Primitive, string, bereits passende Referenzen).
        if (destinationType.IsInstanceOfType(source))
            return source;

        throw new MappingNotConfiguredException(sourceType, destinationType);
    }

    [UnconditionalSuppressMessage("AOT", "IL3050",
        Justification = "Nur Komfort-Fallback für dynamisches Top-Level-Collection-Mapping. Der vom Generator erzeugte Mapping-Code ist vollständig statisch und reflection-frei.")]
    private object MapEnumerable(IEnumerable source, Type destElementType, Type destinationType)
    {
        var listType = typeof(List<>).MakeGenericType(destElementType);
        var list = (IList)Activator.CreateInstance(listType)!;

        foreach (var item in source)
            list.Add(item is null ? null : MapCore(item, item.GetType(), destElementType));

        if (destinationType.IsArray)
        {
            var array = Array.CreateInstance(destElementType, list.Count);
            list.CopyTo(array, 0);
            return array;
        }

        // List<T> erfüllt IEnumerable<T>/IList<T>/ICollection<T>/IReadOnlyList<T> etc.
        return list;
    }

    [UnconditionalSuppressMessage("Trimming", "IL2070",
        Justification = "Komfort-Fallback für dynamisches Collection-Mapping; die generierten Maps benötigen keine Reflection.")]
    private static bool TryGetEnumerableElementType(Type type, out Type elementType)
    {
        if (type.IsArray)
        {
            elementType = type.GetElementType()!;
            return true;
        }

        if (type.IsGenericType)
        {
            var def = type.GetGenericTypeDefinition();
            if (def == typeof(List<>) || def == typeof(IEnumerable<>) || def == typeof(IList<>)
                || def == typeof(ICollection<>) || def == typeof(IReadOnlyList<>)
                || def == typeof(IReadOnlyCollection<>))
            {
                elementType = type.GetGenericArguments()[0];
                return true;
            }
        }

        var iface = type.GetInterfaces()
            .FirstOrDefault(i => i.IsGenericType && i.GetGenericTypeDefinition() == typeof(IEnumerable<>));
        if (iface is not null && type != typeof(string))
        {
            elementType = iface.GetGenericArguments()[0];
            return true;
        }

        elementType = typeof(object);
        return false;
    }
}

/// <summary>Wird geworfen, wenn für ein Typ-Paar kein Mapping konfiguriert/generiert wurde.</summary>
public sealed class MappingNotConfiguredException : InvalidOperationException
{
    public MappingNotConfiguredException(Type source, Type destination)
        : base($"Kein Mapping von '{source.FullName}' nach '{destination.FullName}' konfiguriert. "
               + "Lege ein CreateMap<TSource, TDestination>() in einem Profile an.")
    {
        SourceType = source;
        DestinationType = destination;
    }

    public Type SourceType { get; }
    public Type DestinationType { get; }
}
