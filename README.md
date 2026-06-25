# Minicon.CodeMapper

Schlanker **Objekt-zu-Objekt-Mapper für .NET auf Basis eines Source Generators** – als
Ablösung für AutoMapper. Die vertraute `Profile` / `CreateMap`-DSL bleibt erhalten, die
Mappings werden jedoch **zur Compile-Zeit** als statischer, reflection-freier Code erzeugt.

- ⚡ **Schnell** – keine Reflection im heißen Pfad, nur direkte Zuweisungen und Delegate-Lookups
- 🧩 **Vertraute API** – `Profile`, `CreateMap<,>()`, `ForMember`, `MapFrom`, `Ignore`, `ReverseMap`
- 🔍 **Compile-Zeit-Diagnostics** – nicht zuordenbare Ziel-Member werden direkt im Build gemeldet
- 🪶 **Trimming-/AOT-freundlich** – der generierte Mapping-Code ist vollständig statisch
- 🆓 **MIT-Lizenz** – keine kommerzielle Lizenzpflicht

> Status: **v0.1** – die Kern-Features stehen und sind getestet. Siehe [Roadmap](#roadmap).

---

## Installation

Projektreferenz (während der Entwicklung):

```xml
<ProjectReference Include="path/to/src/Minicon.CodeMapper/Minicon.CodeMapper.csproj" />
```

Der zugehörige Source Generator ist im Paket enthalten und wird automatisch aktiv.

## Schnellstart

**1. Profile definieren** – exakt wie gewohnt:

```csharp
using Minicon.CodeMapper;

public class PersonProfile : Profile
{
    public PersonProfile()
    {
        CreateMap<Address, AddressDto>().ReverseMap();
        CreateMap<Order, OrderDto>();
        CreateMap<Person, PersonDto>()
            .ForMember(d => d.FullName, o => o.MapFrom(s => s.FirstName + " " + s.LastName))
            .ForMember(d => d.Secret,   o => o.Ignore());
    }
}
```

**2. Registrieren** (Microsoft.Extensions.DependencyInjection):

```csharp
services.AddCodeMapper(typeof(PersonProfile).Assembly);
```

**3. Mappen:**

```csharp
public class PersonService(IMapper mapper)
{
    public PersonDto ToDto(Person p) => mapper.Map<PersonDto>(p);
    public List<PersonDto> ToDtos(IEnumerable<Person> people) => mapper.Map<List<PersonDto>>(people);
}
```

Ohne DI geht es auch statisch über `Mapper.Instance`:

```csharp
var dto = Mapper.Instance.Map<PersonDto>(person);
```

## Unterstützte Features (v0.1)

| Feature | Status | Hinweis |
|---|---|---|
| `CreateMap<TSource, TDestination>()` | ✅ | Property-/Feld-Zuordnung nach Name |
| `ForMember(..., o => o.MapFrom(s => expr))` | ✅ | beliebiger Quell-Ausdruck |
| `ForMember(..., o => o.Ignore())` | ✅ | |
| `ReverseMap()` | ✅ | erzeugt Auto-Mapping in Gegenrichtung |
| Verschachtelte Objekte | ✅ | nutzt registriertes Sub-Mapping |
| Collections (`List<T>`, `T[]`, `IEnumerable<T>`, …) | ✅ | Element-Mapping |
| `Nullable<T>` → `T` | ✅ | via `GetValueOrDefault()` |
| Implizite Konvertierungen | ✅ | z. B. `int` → `long` |
| `ConvertUsing(...)` | ✅ | vollständig benutzerdefinierte Konvertierung |
| `BeforeMap` / `AfterMap` | 🚧 | DSL vorhanden, noch ohne Codegen |
| `ConstructUsing` | 🚧 | geplant |
| `ValueResolver` / `Condition` / `NullSubstitute` | 🚧 | geplant |

Nicht zugeordnete Ziel-Member erzeugen die Compile-Zeit-Diagnose **`MINI001`** (Severity: Info).

## Migration von AutoMapper

In den meisten Fällen genügt das Ersetzen des Namespaces und der DI-Methode:

| AutoMapper | Minicon.CodeMapper |
|---|---|
| `using AutoMapper;` | `using Minicon.CodeMapper;` |
| `class X : Profile` | `class X : Profile` (unverändert) |
| `CreateMap<A, B>()...` | unverändert |
| `IMapper` / `mapper.Map<T>(x)` | unverändert |
| `services.AddAutoMapper(asm)` | `services.AddCodeMapper(asm)` |

## Wie es funktioniert

1. Der Source Generator findet alle `Profile`-Subklassen und liest deren
   `CreateMap`/`ForMember`-Deklarationen aus dem Syntaxbaum.
2. Pro Typ-Paar wird eine statische Methode erzeugt (`new TDest { ... }` mit direkten Zuweisungen).
3. Alle Methoden werden per `[ModuleInitializer]` in der `MapperRegistry` registriert –
   ganz ohne Assembly-Scan zur Laufzeit.
4. `IMapper.Map<T>()` ist nur noch ein Dictionary-Lookup auf den passenden Delegate.

Auszug aus generiertem Code:

```csharp
internal static PersonDto Map_3(Person source, IMapper mapper)
{
    if (source is null) return default!;
    return new PersonDto
    {
        Id        = source.Id,
        FullName  = ((Func<Person, string>)(s => s.FirstName + " " + s.LastName))(source),
        LuckyNumber = source.LuckyNumber.GetValueOrDefault(),
        HomeAddress = mapper.Map<AddressDto>(source.HomeAddress),
        Orders      = mapper.Map<List<OrderDto>>(source.Orders),
        // Secret wird ignoriert
    };
}
```

## Trimming & AOT

Der **generierte Mapping-Code** ist vollständig statisch typisiert und reflection-frei.
Einzige Ausnahme ist ein Komfort-Fallback in `Mapper` für *Top-Level*-Collection-Aufrufe
wie `Map<List<T>>(...)`, der minimale Reflection nutzt (sauber annotiert). Für NativeAOT
empfiehlt es sich, registrierte Element-Mappings direkt zu verwenden; die automatische
Generierung dedizierter Collection-Maps ist auf der Roadmap.

## Roadmap

- [ ] Inline-Codegen für Collections (vollständige AOT-Reinheit ohne Reflection-Fallback)
- [ ] `BeforeMap` / `AfterMap` / `ConstructUsing` im Codegen
- [ ] `Condition`, `NullSubstitute`, `ValueResolver`
- [ ] Flattening (`Order.Customer.Name` → `OrderDto.CustomerName`)
- [ ] NuGet-Veröffentlichung
- [ ] Drop-in-Kompatibilitätspaket (Namespace-Alias `AutoMapper`)

## Build & Test

```bash
dotnet build
dotnet test
```

## Lizenz

[MIT](LICENSE) © Michael Nikolaus
