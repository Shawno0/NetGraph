using System.Collections.Immutable;
using NetGraph.Checkpoint;
using NetGraph.State;

namespace NetGraph.Graph;

/// <summary>
/// Represents a graph that has been compiled and can be executed.
/// </summary>
/// <typeparam name="TState">The type of state managed by the graph.</typeparam>
public class CompiledGraph<TState> where TState : IGraphState, new()
{
    private readonly ImmutableDictionary<string, Func<TState, object>> _nodes;
    private readonly ImmutableDictionary<string, List<string>> _edges;
    private readonly ImmutableDictionary<string, Func<TState, string>> _conditionalEdges;
    private readonly ICheckpointer? _checkpointer;

    /// <summary>
    /// Initializes a new instance of the <see cref="CompiledGraph{TState}"/> class.
    /// </summary>
    /// <param name="nodes">The nodes in the graph.</param>
    /// <param name="edges">The edges in the graph.</param>
    /// <param name="conditionalEdges">The conditional edges in the graph.</param>
    /// <param name="checkpointer">Optional checkpointer for state persistence.</param>
    public CompiledGraph(
        ImmutableDictionary<string, Func<TState, object>> nodes,
        ImmutableDictionary<string, List<string>> edges,
        ImmutableDictionary<string, Func<TState, string>> conditionalEdges,
        ICheckpointer? checkpointer = null)
    {
        _nodes = nodes;
        _edges = edges;
        _conditionalEdges = conditionalEdges;
        _checkpointer = checkpointer;
    }

    /// <summary>
    /// Executes the graph with the provided state or input.
    /// </summary>
    /// <param name="input">The initial state or input to the graph.</param>
    /// <param name="config">Optional configuration parameters.</param>
    /// <returns>The final state after execution.</returns>
    public TState Invoke(object input, Dictionary<string, object>? config = null)
    {
        config ??= new Dictionary<string, object>();
        
        string? threadId = null;
        if (config.TryGetValue("configurable", out var configurable) && 
            configurable is Dictionary<string, object> configurableDict &&
            configurableDict.TryGetValue("thread_id", out var threadIdObj))
        {
            threadId = threadIdObj.ToString();
        }

        TState currentState;
        
        // Load state from checkpointer if available and thread_id is provided
        if (_checkpointer != null && !string.IsNullOrEmpty(threadId))
        {
            currentState = (TState)_checkpointer.Load(threadId) ?? new TState();
        }
        else
        {
            currentState = new TState();
        }
        
        // Merge input into the state
        currentState.Merge(input);

        // Save initial state if checkpointing
        if (_checkpointer != null && !string.IsNullOrEmpty(threadId))
        {
            _checkpointer.Save(threadId, currentState);
        }

        // Start execution
        string currentNode = _edges[GraphConstants.START][0];
        
        while (currentNode != GraphConstants.END)
        {
            // Execute current node
            var nodeHandler = _nodes[currentNode];
            var update = nodeHandler(currentState);
            
            // Update state with result
            currentState.Merge(update);
            
            // Save state if checkpointing
            if (_checkpointer != null && !string.IsNullOrEmpty(threadId))
            {
                _checkpointer.Save(threadId, currentState);
            }
            
            // Determine next node
            if (_conditionalEdges.TryGetValue(currentNode, out var conditionFunc))
            {
                // Use conditional edges
                currentNode = conditionFunc(currentState);
            }
            else
            {
                // Use regular edges
                var nextNodes = _edges[currentNode];
                if (nextNodes.Count == 0)
                {
                    throw new InvalidOperationException($"Node '{currentNode}' has no outgoing edges");
                }
                currentNode = nextNodes[0];
            }
        }
        
        return currentState;
    }

    /// <summary>
    /// Asynchronously streams the execution of the graph.
    /// </summary>
    /// <param name="input">The initial state or input to the graph.</param>
    /// <param name="config">Optional configuration parameters.</param>
    /// <returns>An async enumerable of state snapshots during execution.</returns>
    public async IAsyncEnumerable<TState> StreamAsync(object input, Dictionary<string, object>? config = null)
    {
        config ??= new Dictionary<string, object>();
        
        string? threadId = null;
        if (config.TryGetValue("configurable", out var configurable) && 
            configurable is Dictionary<string, object> configurableDict &&
            configurableDict.TryGetValue("thread_id", out var threadIdObj))
        {
            threadId = threadIdObj.ToString();
        }

        TState currentState;
        
        // Load state from checkpointer if available and thread_id is provided
        if (_checkpointer != null && !string.IsNullOrEmpty(threadId))
        {
            currentState = (TState)_checkpointer.Load(threadId) ?? new TState();
        }
        else
        {
            currentState = new TState();
        }
        
        // Merge input into the state
        currentState.Merge(input);
        
        // Yield initial state
        yield return (TState)currentState.Clone();
        
        // Save initial state if checkpointing
        if (_checkpointer != null && !string.IsNullOrEmpty(threadId))
        {
            _checkpointer.Save(threadId, currentState);
        }
        
        // Start execution
        string currentNode = _edges[GraphConstants.START][0];
        
        while (currentNode != GraphConstants.END)
        {
            // Execute current node
            var nodeHandler = _nodes[currentNode];
            var update = nodeHandler(currentState);
            
            // Update state with result
            currentState.Merge(update);
            
            // Yield updated state
            yield return (TState)currentState.Clone();
            
            // Save state if checkpointing
            if (_checkpointer != null && !string.IsNullOrEmpty(threadId))
            {
                _checkpointer.Save(threadId, currentState);
            }
            
            // Determine next node
            if (_conditionalEdges.TryGetValue(currentNode, out var conditionFunc))
            {
                // Use conditional edges
                currentNode = conditionFunc(currentState);
            }
            else
            {
                // Use regular edges
                var nextNodes = _edges[currentNode];
                if (nextNodes.Count == 0)
                {
                    throw new InvalidOperationException($"Node '{currentNode}' has no outgoing edges");
                }
                currentNode = nextNodes[0];
            }
            
            // Small delay to prevent CPU overuse in tight loops
            await Task.Delay(1);
        }
    }
}