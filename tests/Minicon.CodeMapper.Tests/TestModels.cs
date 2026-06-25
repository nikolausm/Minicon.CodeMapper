using System.Collections.Generic;
using Minicon.CodeMapper;

namespace Minicon.CodeMapper.Tests;

// ---- Domain / Quelle ----

public class Person
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public int Age { get; set; }
    public int? LuckyNumber { get; set; }
    public Address HomeAddress { get; set; } = new();
    public List<Order> Orders { get; set; } = new();
    public string Secret { get; set; } = "";
}

public class Address
{
    public string City { get; set; } = "";
    public string Zip { get; set; } = "";
}

public class Order
{
    public int Number { get; set; }
    public decimal Total { get; set; }
}

// ---- DTOs / Ziel ----

public class PersonDto
{
    public int Id { get; set; }
    public string FirstName { get; set; } = "";
    public string LastName { get; set; } = "";
    public string FullName { get; set; } = "";
    public int Age { get; set; }
    public int LuckyNumber { get; set; }
    public AddressDto HomeAddress { get; set; } = new();
    public List<OrderDto> Orders { get; set; } = new();
    public string Secret { get; set; } = "";
}

public class AddressDto
{
    public string City { get; set; } = "";
    public string Zip { get; set; } = "";
}

public class OrderDto
{
    public int Number { get; set; }
    public decimal Total { get; set; }
}

// ---- Profile ----

public class PersonProfile : Profile
{
    public PersonProfile()
    {
        CreateMap<Address, AddressDto>().ReverseMap();
        CreateMap<Order, OrderDto>();
        CreateMap<Person, PersonDto>()
            .ForMember(d => d.FullName, o => o.MapFrom(s => s.FirstName + " " + s.LastName))
            .ForMember(d => d.Secret, o => o.Ignore());
    }
}
