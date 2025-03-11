using System.Collections.Immutable;
using NetGraph.State;
using NetGraph.Checkpoint;

namespace NetGraph.Graph;

/// <summary>
/// Constants for start and end node names.
/// </summary>
public static class GraphConstants
{
    /// <summary>
    /// Special node name representing the start of a graph.
    /// </summary>
    public const string START = "__start__";
    
    /// <summary>
    /// Special node name representing the end of a graph.
    /// </summary>
    public const string END = "__end__";
}

/// <summary>
/// Represents a directed graph for managing state transitions.
/// </summary>
/// <typeparam name="TState">The type of state managed by the graph.</typeparam>
public class StateGraph<TState> where TState : IGraphState, new()
{
    private readonly Dictionary<string, Func<TState, object>> _nodes = new();
    private readonly Dictionary<string, List<string>> _edges = new();
    private readonly Dictionary<string, Func<TState, string>> _conditionalEdges = new();
    private readonly HashSet<string> _nodeNames = new();

    /// <summary>
    /// Gets the names of all nodes in the graph.
    /// </summary>
    public IReadOnlyCollection<string> NodeNames => _nodeNames.ToImmutableArray();

    /// <summary>
    /// Adds a node to the graph.
    /// </summary>
    /// <param name="name">The name of the node.</param>
    /// <param name="handler">The function that processes the state and returns an update.</param>
    /// <returns>The current instance for method chaining.</returns>
    public StateGraph<TState> AddNode(string name, Func<TState, object> handler)
    {
        if (_nodes.ContainsKey(name))
        {
            throw new ArgumentException($"Node '{name}' already exists", nameof(name));
        }

        _nodes[name] = handler;
        _nodeNames.Add(name);
        _edges[name] = new List<string>();
        return this;
    }

    /// <summary>
    /// Adds an edge between two nodes.
    /// </summary>
    /// <param name="from">The source node name.</param>
    /// <param name="to">The destination node name.</param>
    /// <returns>The current instance for method chaining.</returns>
    public StateGraph<TState> AddEdge(string from, string to)
    {
        if (from != GraphConstants.START && !_nodes.ContainsKey(from))
        {
            throw new ArgumentException($"Source node '{from}' does not exist", nameof(from));
        }

        if (to != GraphConstants.END && !_nodes.ContainsKey(to))
        {
            throw new ArgumentException($"Destination node '{to}' does not exist", nameof(to));
        }

        if (from == GraphConstants.START)
        {
            if (_edges.ContainsKey(from))
            {
                throw new InvalidOperationException("Start node can only have one outgoing edge");
            }
            _edges[from] = new List<string> { to };
        }
        else
        {
            if (_conditionalEdges.ContainsKey(from))
            {
                throw new InvalidOperationException($"Node '{from}' already has conditional edges");
            }
            _edges[from].Add(to);
        }

        return this;
    }

    /// <summary>
    /// Adds conditional edges from a node based on a function that evaluates the state.
    /// </summary>
    /// <param name="from">The source node name.</param>
    /// <param name="conditionFunction">The function that evaluates the state and returns the destination node name.</param>
    /// <returns>The current instance for method chaining.</returns>
    public StateGraph<TState> AddConditionalEdges(string from, Func<TState, string> conditionFunction)
    {
        if (!_nodes.ContainsKey(from))
        {
            throw new ArgumentException($"Node '{from}' does not exist", nameof(from));
        }

        if (_edges[from].Count > 0)
        {
            throw new InvalidOperationException($"Node '{from}' already has regular edges");
        }

        _conditionalEdges[from] = conditionFunction;
        return this;
    }

    /// <summary>
    /// Compiles the graph into a runnable workflow.
    /// </summary>
    /// <param name="checkpointer">Optional checkpointer for state persistence.</param>
    /// <returns>A compiled graph that can be executed.</returns>
    public CompiledGraph<TState> Compile(ICheckpointer? checkpointer = null)
    {
        ValidateGraph();
        return new CompiledGraph<TState>(
            ImmutableDictionary.CreateRange(_nodes),
            ImmutableDictionary.CreateRange(_edges),
            ImmutableDictionary.CreateRange(_conditionalEdges),
            checkpointer);
    }

    private void ValidateGraph()
    {
        // Check if START has outgoing edges
        if (!_edges.ContainsKey(GraphConstants.START) || _edges[GraphConstants.START].Count == 0)
        {
            throw new InvalidOperationException("Start node must have at least one outgoing edge");
        }

        // Check if all nodes have either regular edges or conditional edges
        foreach (var nodeName in _nodeNames)
        {
            if (!_edges.TryGetValue(nodeName, out var edgeList) || 
                (edgeList.Count == 0 && !_conditionalEdges.ContainsKey(nodeName)))
            {
                throw new InvalidOperationException(
                    $"Node '{nodeName}' has no outgoing edges. Add regular or conditional edges to fix this.");
            }
        }
    }
}