using System;
using System.Collections.Generic;
using System.Linq.Expressions;

namespace Minicon.CodeMapper;

/// <summary>
/// Basisklasse zur Deklaration von Mappings – API-kompatibel zum AutoMapper-Profile.
/// Die hier per <see cref="CreateMap{TSource,TDestination}()"/> deklarierten Mappings
/// werden vom Source Generator zur Compile-Zeit ausgewertet; zur Laufzeit ist die
/// Fluent-Konfiguration ein No-op (der generierte Code führt das Mapping aus).
/// </summary>
public abstract class Profile
{
    /// <summary>Optionaler Profilname (rein informativ).</summary>
    public string ProfileName { get; }

    protected Profile() => ProfileName = GetType().FullName ?? GetType().Name;

    protected Profile(string profileName) => ProfileName = profileName;

    /// <summary>Deklariert ein Mapping von <typeparamref name="TSource"/> nach <typeparamref name="TDestination"/>.</summary>
    public IMappingExpression<TSource, TDestination> CreateMap<TSource, TDestination>()
        => new MappingExpression<TSource, TDestination>();

    /// <summary>Deklariert ein Mapping über zur Laufzeit bekannte Typen (eingeschränkt unterstützt).</summary>
    public IMappingExpression CreateMap(Type sourceType, Type destinationType)
        => new MappingExpression();
}

/// <summary>Nicht-generische Mapping-Konfiguration (Platzhalter für den Type-basierten Overload).</summary>
public interface IMappingExpression
{
    IMappingExpression ForMember(string name, Action<IMemberConfigurationExpression> memberOptions);
    IMappingExpression ReverseMap();
}

/// <summary>Fluent-Konfiguration eines Mappings <typeparamref name="TSource"/> → <typeparamref name="TDestination"/>.</summary>
public interface IMappingExpression<TSource, TDestination>
{
    /// <summary>Konfiguriert ein einzelnes Ziel-Member (z. B. <c>opt =&gt; opt.MapFrom(...)</c> oder <c>opt =&gt; opt.Ignore()</c>).</summary>
    IMappingExpression<TSource, TDestination> ForMember<TMember>(
        Expression<Func<TDestination, TMember>> destinationMember,
        Action<IMemberConfigurationExpression<TSource, TDestination, TMember>> memberOptions);

    /// <summary>Konfiguriert ein Ziel-Member über seinen Namen.</summary>
    IMappingExpression<TSource, TDestination> ForMember(
        string destinationMember,
        Action<IMemberConfigurationExpression<TSource, TDestination, object>> memberOptions);

    /// <summary>Ignoriert alle (noch) nicht zugeordneten Ziel-Member, statt eine Diagnose zu erzeugen.</summary>
    IMappingExpression<TSource, TDestination> IgnoreAllUnmapped();

    /// <summary>Erzeugt zusätzlich das Rückwärts-Mapping <typeparamref name="TDestination"/> → <typeparamref name="TSource"/>.</summary>
    IMappingExpression<TDestination, TSource> ReverseMap();

    /// <summary>Vollständig benutzerdefinierte Konvertierung (wird vom Generator als Direkt-Aufruf übernommen).</summary>
    IMappingExpression<TSource, TDestination> ConvertUsing(Expression<Func<TSource, TDestination>> converter);

    /// <summary>Benutzerdefinierte Ziel-Konstruktion.</summary>
    IMappingExpression<TSource, TDestination> ConstructUsing(Expression<Func<TSource, TDestination>> constructor);

    /// <summary>Aktion vor dem Mapping (zur Laufzeit als Hook ausgeführt, sofern generiert).</summary>
    IMappingExpression<TSource, TDestination> BeforeMap(Action<TSource, TDestination> action);

    /// <summary>Aktion nach dem Mapping.</summary>
    IMappingExpression<TSource, TDestination> AfterMap(Action<TSource, TDestination> action);
}

/// <summary>Member-Konfiguration (nicht-generisch).</summary>
public interface IMemberConfigurationExpression
{
    void Ignore();
    void MapFrom(string sourcePath);
}

/// <summary>Member-Konfiguration für ein konkretes Ziel-Member <typeparamref name="TMember"/>.</summary>
public interface IMemberConfigurationExpression<TSource, TDestination, TMember>
{
    /// <summary>Ziel-Member aus einem Quell-Ausdruck befüllen.</summary>
    void MapFrom<TResult>(Expression<Func<TSource, TResult>> sourceMember);

    /// <summary>Ziel-Member aus Quelle und (Teil-)Ziel befüllen.</summary>
    void MapFrom<TResult>(Expression<Func<TSource, TDestination, TResult>> mappingExpression);

    /// <summary>Member nicht mappen.</summary>
    void Ignore();

    /// <summary>Mapping nur unter Bedingung anwenden.</summary>
    void Condition(Func<TSource, bool> condition);

    /// <summary>Ersatzwert, falls die Quelle null ist.</summary>
    void NullSubstitute(object nullSubstitute);

    /// <summary>Konstanten Wert zuweisen.</summary>
    void UseValue<TValue>(TValue value);
}
