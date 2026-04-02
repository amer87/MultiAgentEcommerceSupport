namespace EcommerceSupport.Workflows.Abstractions;

/// <summary>
/// Shared mutable state bag for a single workflow run.
/// Allows executors to persist objects (e.g. agent sessions) across steps
/// without coupling them to each other directly.
/// </summary>
public interface IWorkflowContext
{
    bool TryGetState<T>(string key, out T? value);
    void SetState<T>(string key, T value);
}
