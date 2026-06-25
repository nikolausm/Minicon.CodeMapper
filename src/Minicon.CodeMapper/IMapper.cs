using System;
using System.Collections.Generic;

namespace Minicon.CodeMapper;

/// <summary>
/// Laufzeit-Schnittstelle zum Ausführen von Mappings. API-kompatibel zum
/// gewohnten AutoMapper-Stil (<c>mapper.Map&lt;TDestination&gt;(source)</c>),
/// jedoch backed durch vorkompilierte, vom Source Generator erzeugte Delegates.
/// </summary>
public interface IMapper
{
    /// <summary>Mappt <paramref name="source"/> auf eine neue Instanz von <typeparamref name="TDestination"/>.</summary>
    TDestination Map<TDestination>(object source);

    /// <summary>Mappt <paramref name="source"/> auf eine neue Instanz von <typeparamref name="TDestination"/>.</summary>
    TDestination Map<TSource, TDestination>(TSource source);

    /// <summary>Mappt <paramref name="source"/> in die bestehende Instanz <paramref name="destination"/> (v1: erzeugt das Ziel neu auf Basis von source).</summary>
    TDestination Map<TSource, TDestination>(TSource source, TDestination destination);

    /// <summary>Nicht-generisches Mapping über zur Laufzeit bekannte Typen.</summary>
    object? Map(object? source, Type sourceType, Type destinationType);
}
