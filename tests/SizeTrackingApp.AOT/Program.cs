// This is the canonical Native AOT size canary. Keep its model and consumers
// stable: changes require refreshing the per-RID aot-size-baselines.json values.

using Microsoft.Extensions.Configuration;
using PolyType.Examples.ConfigurationBinder;
using PolyType.Examples.JsonSerializer;
using System.Text;
using System.Text.Json.Nodes;

RepresentativeModel sample = RepresentativeModel.CreateSample();
string json = JsonSerializerTS.Serialize(sample);
RepresentativeModel? roundTripped = JsonSerializerTS.Deserialize<RepresentativeModel>(json);
VerifyEquivalent(roundTripped, "JSON");

using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
IConfigurationRoot configuration = new ConfigurationBuilder().AddJsonStream(stream).Build();
using IDisposable configurationLifetime = (IDisposable)configuration;
RepresentativeModel? bound = ConfigurationBinderTS.Get<RepresentativeModel>(configuration);
VerifyEquivalent(bound, "configuration");

Console.WriteLine($"serialized: {Encoding.UTF8.GetByteCount(json)} bytes");
Console.WriteLine("deserialized: ok");
Console.WriteLine("configuration: ok");

void VerifyEquivalent(RepresentativeModel? value, string consumer)
{
    if (value is null || !JsonNode.DeepEquals(JsonNode.Parse(json), JsonNode.Parse(JsonSerializerTS.Serialize(value))))
    {
        throw new InvalidOperationException($"The {consumer} round trip changed the representative model.");
    }
}
