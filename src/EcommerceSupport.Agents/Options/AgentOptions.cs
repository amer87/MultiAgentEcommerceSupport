namespace EcommerceSupport.Agents.Options;

/// <summary>
/// Bound from appsettings.json section "AgentOptions".
/// </summary>
public sealed class AgentOptions
{
    public const string SectionName = "AgentOptions";

    public string AzureOpenAIEndpoint { get; set; } = default!;
    public string DeploymentName { get; set; } = "gpt-4o-mini";

    /// <summary>
    /// When true, agents use structured output (JSON mode) for the triage step.
    /// </summary>
    public bool UseStructuredOutput { get; set; } = true;

    /// <summary>
    /// Maximum tokens per response.
    /// </summary>
    public int MaxTokens { get; set; } = 1024;
}
