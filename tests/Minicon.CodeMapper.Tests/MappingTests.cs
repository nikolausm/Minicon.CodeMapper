using System.Collections.Generic;
using Microsoft.Extensions.DependencyInjection;
using Minicon.CodeMapper;
using Xunit;

namespace Minicon.CodeMapper.Tests;

public class MappingTests
{
    private static Person SamplePerson() => new()
    {
        Id = 42,
        FirstName = "John",
        LastName = "Doe",
        Age = 30,
        LuckyNumber = 7,
        HomeAddress = new Address { City = "Berlin", Zip = "10115" },
        Orders = new List<Order>
        {
            new() { Number = 1, Total = 19.99m },
            new() { Number = 2, Total = 5.00m },
        },
        Secret = "top-secret",
    };

    [Fact]
    public void Maps_simple_properties_by_name()
    {
        var dto = Mapper.Instance.Map<PersonDto>(SamplePerson());

        Assert.Equal(42, dto.Id);
        Assert.Equal("John", dto.FirstName);
        Assert.Equal("Doe", dto.LastName);
        Assert.Equal(30, dto.Age);
    }

    [Fact]
    public void Maps_nullable_value_to_non_nullable()
    {
        var dto = Mapper.Instance.Map<PersonDto>(SamplePerson());
        Assert.Equal(7, dto.LuckyNumber);
    }

    [Fact]
    public void Applies_MapFrom_expression()
    {
        var dto = Mapper.Instance.Map<PersonDto>(SamplePerson());
        Assert.Equal("John Doe", dto.FullName);
    }

    [Fact]
    public void Respects_Ignore()
    {
        var dto = Mapper.Instance.Map<PersonDto>(SamplePerson());
        Assert.Equal("", dto.Secret);
    }

    [Fact]
    public void Maps_nested_object()
    {
        var dto = Mapper.Instance.Map<PersonDto>(SamplePerson());

        Assert.NotNull(dto.HomeAddress);
        Assert.Equal("Berlin", dto.HomeAddress.City);
        Assert.Equal("10115", dto.HomeAddress.Zip);
    }

    [Fact]
    public void Maps_nested_collection()
    {
        var dto = Mapper.Instance.Map<PersonDto>(SamplePerson());

        Assert.Equal(2, dto.Orders.Count);
        Assert.Equal(1, dto.Orders[0].Number);
        Assert.Equal(19.99m, dto.Orders[0].Total);
        Assert.Equal(2, dto.Orders[1].Number);
    }

    [Fact]
    public void Maps_top_level_collection()
    {
        var orders = new List<Order>
        {
            new() { Number = 10, Total = 1m },
            new() { Number = 20, Total = 2m },
        };

        var dtos = Mapper.Instance.Map<List<OrderDto>>(orders);

        Assert.Equal(2, dtos.Count);
        Assert.Equal(10, dtos[0].Number);
        Assert.Equal(20, dtos[1].Number);
    }

    [Fact]
    public void Supports_ReverseMap()
    {
        var dto = new AddressDto { City = "Hamburg", Zip = "20095" };

        var entity = Mapper.Instance.Map<Address>(dto);

        Assert.Equal("Hamburg", entity.City);
        Assert.Equal("20095", entity.Zip);
    }

    [Fact]
    public void Works_through_dependency_injection()
    {
        var provider = new ServiceCollection()
            .AddCodeMapper(typeof(PersonProfile).Assembly)
            .BuildServiceProvider();

        var mapper = provider.GetRequiredService<IMapper>();
        var dto = mapper.Map<PersonDto>(SamplePerson());

        Assert.Equal("John Doe", dto.FullName);
        Assert.Equal("Berlin", dto.HomeAddress.City);
    }

    [Fact]
    public void At_least_one_map_is_registered()
    {
        Assert.True(MapperRegistry.Count > 0);
    }
}
