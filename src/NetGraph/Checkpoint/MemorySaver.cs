using NetGraph.State;
using System.Collections.Concurrent;

namespace NetGraph.Checkpoint;

/// <summary>
/// An in-memory implementation of <see cref="ICheckpointer"/> that stores states in memory.
/// </summary>
public class MemorySaver : ICheckpointer
{
    private readonly ConcurrentDictionary<string, IGraphState> _states = new();

    /// <summary>
    /// Saves the state for the specified thread ID.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <param name="state">The state to save.</param>
    public void Save(string threadId, IGraphState state)
    {
        _states[threadId] = state.Clone();
    }

    /// <summary>
    /// Loads the state for the specified thread ID.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    /// <returns>The loaded state, or null if no state exists.</returns>
    public IGraphState? Load(string threadId)
    {
        if (_states.TryGetValue(threadId, out var state))
        {
            return state.Clone();
        }
        
        return null;
    }

    /// <summary>
    /// Deletes the state for the specified thread ID.
    /// </summary>
    /// <param name="threadId">The thread ID.</param>
    public void Delete(string threadId)
    {
        _states.TryRemove(threadId, out _);
    }
}