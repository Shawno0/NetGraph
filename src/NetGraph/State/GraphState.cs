namespace NetGraph.State;

/// <summary>
/// Base interface for all state schemas.
/// </summary>
public interface IGraphState
{
    /// <summary>
    /// Merges an update into the current state.
    /// </summary>
    /// <param name="update">The update to merge.</param>
    void Merge(object update);

    /// <summary>
    /// Creates a deep copy of the current state.
    /// </summary>
    /// <returns>A new instance with the same data.</returns>
    IGraphState Clone();
}

/// <summary>
/// Base abstract class for all state schemas.
/// </summary>
/// <typeparam name="T">The concrete state type.</typeparam>
public abstract class GraphState<T> : IGraphState where T : GraphState<T>
{
    /// <summary>
    /// Merges an update into the current state.
    /// </summary>
    /// <param name="update">The update to merge.</param>
    public virtual void Merge(object update)
    {
        if (update is T typedUpdate)
        {
            MergeTyped(typedUpdate);
        }
        else
        {
            throw new ArgumentException($"Update must be of type {typeof(T).Name}", nameof(update));
        }
    }

    /// <summary>
    /// Merges a typed update into the current state.
    /// </summary>
    /// <param name="update">The typed update to merge.</param>
    protected abstract void MergeTyped(T update);

    /// <summary>
    /// Creates a deep copy of the current state.
    /// </summary>
    /// <returns>A new instance with the same data.</returns>
    public abstract IGraphState Clone();
}