using NetGraph.State;

namespace NetGraph.Checkpoint;

/// <summary>
/// Interface for checkpointing state in a graph.
/// </summary>
public interface ICheckpointer
{
    /// <summary>
    /// Saves the state for the specified thread ID.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <param name="state">The state to save.</param>
    void Save(string threadId, IGraphState state);

    /// <summary>
    /// Loads the state for the specified thread ID.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <returns>The loaded state, or null if no state exists.</returns>
    IGraphState? Load(string threadId);

    /// <summary>
    /// Deletes the state for the specified thread ID.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    void Delete(string threadId);
}
    