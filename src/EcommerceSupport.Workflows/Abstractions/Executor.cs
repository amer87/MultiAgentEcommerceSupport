namespace EcommerceSupport.Workflows.Abstractions;

/// <summary>
/// Base class for all workflow.
/// Each executor transforms <typeparamref name="TIn"/> to <typeparamref name="TOut"/>.
/// </summary>
public abstract class Executor<TIn, TOut>(string executorId)
{
    protected string ExecutorId { get; } = executorId;

    public abstract ValueTask<TOut> HandleAsync(
        TIn input,
        IWorkflowContext context,
        CancellationToken ct = default);
}
