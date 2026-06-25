using System;
using System.Linq.Expressions;

namespace Minicon.CodeMapper;

// Laufzeit-No-op-Implementierungen der Fluent-DSL.
// Die eigentliche Auswertung übernimmt der Source Generator zur Compile-Zeit;
// diese Typen existieren, damit Profile-Code kompiliert und ggf. instanziiert werden kann,
// ohne dass zur Laufzeit etwas passieren muss.

internal sealed class MappingExpression<TSource, TDestination> : IMappingExpression<TSource, TDestination>
{
    public IMappingExpression<TSource, TDestination> ForMember<TMember>(
        Expression<Func<TDestination, TMember>> destinationMember,
        Action<IMemberConfigurationExpression<TSource, TDestination, TMember>> memberOptions) => this;

    public IMappingExpression<TSource, TDestination> ForMember(
        string destinationMember,
        Action<IMemberConfigurationExpression<TSource, TDestination, object>> memberOptions) => this;

    public IMappingExpression<TSource, TDestination> IgnoreAllUnmapped() => this;

    public IMappingExpression<TDestination, TSource> ReverseMap()
        => new MappingExpression<TDestination, TSource>();

    public IMappingExpression<TSource, TDestination> ConvertUsing(Expression<Func<TSource, TDestination>> converter) => this;

    public IMappingExpression<TSource, TDestination> ConstructUsing(Expression<Func<TSource, TDestination>> constructor) => this;

    public IMappingExpression<TSource, TDestination> BeforeMap(Action<TSource, TDestination> action) => this;

    public IMappingExpression<TSource, TDestination> AfterMap(Action<TSource, TDestination> action) => this;
}

internal sealed class MappingExpression : IMappingExpression
{
    public IMappingExpression ForMember(string name, Action<IMemberConfigurationExpression> memberOptions) => this;
    public IMappingExpression ReverseMap() => this;
}
