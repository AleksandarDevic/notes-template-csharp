using System.ComponentModel.DataAnnotations;

namespace Infrastructure.AI;

public sealed class AnthropicOptions
{
    public const string SectionName = "Anthropic";

    [Required]
    public required string ApiKey { get; init; }

    public string Model { get; init; } = "claude-sonnet-4-6";
}
