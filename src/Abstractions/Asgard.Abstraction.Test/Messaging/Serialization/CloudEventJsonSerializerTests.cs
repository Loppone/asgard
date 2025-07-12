using System.Text.Json;
using Asgard.Abstraction.Messaging.Serialization;
using FluentAssertions;

namespace Asgard.Abstraction.Test.Messaging.Serialization;

public class CloudEventJsonSerializerTests
{
    private readonly CloudEventJsonSerializer _serializer = new();
    private readonly JsonSerializerOptions _camelOptions;

    private static readonly JsonSerializerOptions PascalCaseJsonSerializerOptions = new() { PropertyNamingPolicy = null };

    private record SampleData(string FirstName, int Age);
    private record NestedData(string Id, SampleData Inner);
    private record CamelData(string FirstName, int ItemCount);

    public CloudEventJsonSerializerTests()
    {
        _camelOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        };
    }


    [Fact]
    public void Deserialize_Should_Convert_Json_Element_To_Target_Type()
    {
        var json = JsonSerializer.SerializeToElement(new SampleData("Max", 50));

        var result = _serializer.Deserialize(json, typeof(SampleData));

        result.Should().BeOfType<SampleData>()
              .Which.Should().BeEquivalentTo(new SampleData("Max", 50));
    }

    [Fact]
    public void Deserialize_Should_Throw_If_Raw_Data_Is_Not_Json_Element()
    {
        var notJson = 123;

        var act = () => _serializer.Deserialize(notJson, typeof(SampleData));

        act.Should().Throw<JsonException>()
           .WithMessage("Il dato raw non è un JsonElement valido");
    }

    [Fact]
    public void Deserialize_Should_Support_Null_Values_If_Nullable_Type()
    {
        var json = JsonSerializer.SerializeToElement<JsonElement?>(null!);

        var result = _serializer.Deserialize(json, typeof(string));

        result.Should().BeNull();
    }

    [Fact]
    public void Deserialize_Should_Handle_Nested_Objects()
    {
        var nested = new NestedData("X", new SampleData("Luppo", 13));

        var json = JsonSerializer.SerializeToElement(
            nested,
            PascalCaseJsonSerializerOptions);

        var result = _serializer.Deserialize(json, typeof(NestedData));

        result.Should().BeEquivalentTo(nested);
    }

    [Fact]
    public void Deserialize_Should_Handle_List_Of_Objects()
    {
        var list = new List<SampleData>
       {
           new("A", 1),
           new("B", 2)
       };

        var json = JsonSerializer.SerializeToElement(list);

        var result = _serializer.Deserialize(json, typeof(List<SampleData>));

        result.Should().BeEquivalentTo(list);
    }

    [Fact]
    public void Deserialize_Should_Handle_CamelCase_Properties()
    {
        var jsonString = """{ "FirstName": "yes", "ItemCount": 99 }""";
        var json = JsonSerializer.Deserialize<JsonElement>(jsonString);

        var result = _serializer.Deserialize(json, typeof(CamelData));

        result.Should().BeEquivalentTo(new CamelData("yes", 99));
    }

    [Fact]
    public void Deserialize_Should_Use_Custom_Json_Serializer_Options_From_Constructor()
    {
        var serializer = new CloudEventJsonSerializer(_camelOptions);

        var jsonString = """{ "firstName": "Luppo", "age": 13 }""";
        var json = JsonSerializer.Deserialize<JsonElement>(jsonString, _camelOptions);

        var result = serializer.Deserialize(json, typeof(SampleData));

        result.Should().BeEquivalentTo(new SampleData("Luppo", 13));
    }
}
