
using System.Text.Json.Serialization;
using AppiumBootstrapInstaller.Models;

namespace AppiumBootstrapInstaller.Models;

[JsonSerializable(typeof(Device))]
[JsonSerializable(typeof(AppiumSession))]
[JsonSerializable(typeof(DeviceRegistryData))]
[JsonSerializable(typeof(InstallConfig))]
[JsonSerializable(typeof(List<Device>))]
[JsonSerializable(typeof(Dictionary<string, Device>))]
[JsonSourceGenerationOptions(
    WriteIndented = true,
    PropertyNamingPolicy = JsonKnownNamingPolicy.CamelCase,
    DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)]
public partial class AppJsonSerializerContext : JsonSerializerContext
{
}
