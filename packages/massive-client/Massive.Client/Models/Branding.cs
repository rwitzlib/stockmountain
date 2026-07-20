using System.Diagnostics.CodeAnalysis;
using System.Text.Json.Serialization;

namespace Massive.Client.Models;

[ExcludeFromCodeCoverage]
public class Branding
{
    [JsonPropertyName("icon_url")]
    public string? IconUrl { get; set; }

    [JsonPropertyName("logo_url")]
    public string? LogoUrl { get; set; }
}
