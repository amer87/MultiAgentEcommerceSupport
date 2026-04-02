namespace EcommerceSupport.Workflows.Abstractions;

/// <summary>
/// Dictionary-backed implementation of <see cref="IWorkflowContext"/>.
/// We only create per workflow (it is not shared across runs).
/// </summary>
internal sealed class InMemoryWorkflowContext : IWorkflowContext
{
    private readonly Dictionary<string, object?> _store = [];

    public bool TryGetState<T>(string key, out T? value)
    {
        if (_store.TryGetValue(key, out var raw) && raw is T typed)
        {
            value = typed;
            return true;
        }

        value = default;
        return false;
    }

    public void SetState<T>(string key, T value) =>
        _store[key] = value;
}
