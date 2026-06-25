using System;
using System.Collections.Generic;

namespace Minicon.CodeMapper;

/// <summary>
/// Globale, prozessweite Registry der vom Source Generator erzeugten Mapping-Delegates.
/// Der Generator trägt seine Maps per <c>[ModuleInitializer]</c> ein, sodass zur Laufzeit
/// kein Reflection-Scan der Profile nötig ist.
/// </summary>
public static class MapperRegistry
{
    private static readonly object Gate = new();

    // (Quelltyp, Zieltyp) -> (source, mapper) => destination
    private static readonly Dictionary<(Type Source, Type Destination), Func<object, IMapper, object>> Maps = new();

    /// <summary>Vom generierten Code aufgerufen, um ein Mapping zu registrieren.</summary>
    public static void Register<TSource, TDestination>(Func<TSource, IMapper, TDestination> map)
    {
        if (map is null) throw new ArgumentNullException(nameof(map));
        lock (Gate)
        {
            Maps[(typeof(TSource), typeof(TDestination))] =
                (src, mapper) => map((TSource)src, mapper)!;
        }
    }

    /// <summary>Sucht ein registriertes Mapping für das exakte Typ-Paar.</summary>
    public static bool TryGet(Type source, Type destination, out Func<object, IMapper, object> map)
    {
        lock (Gate)
        {
            return Maps.TryGetValue((source, destination), out map!);
        }
    }

    /// <summary>True, wenn für das exakte Typ-Paar ein Mapping registriert ist.</summary>
    public static bool Has(Type source, Type destination)
    {
        lock (Gate)
        {
            return Maps.ContainsKey((source, destination));
        }
    }

    /// <summary>Anzahl registrierter Typ-Paare (Diagnose/Tests).</summary>
    public static int Count
    {
        get { lock (Gate) { return Maps.Count; } }
    }
}
