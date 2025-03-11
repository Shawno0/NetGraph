using System.Text.Json;
using NetGraph.Checkpoint;
using NetGraph.Graph;
using NetGraph.Messages;
using NetGraph.State;
using NetGraph.Tools;

namespace NetGraph.Prebuilt;

/// <summary>
/// Factory for creating ReAct agents.
/// </summary>
public static class ReActAgent
{
    /// <summary>
    /// Creates a ReAct agent with the specified model and tools.
    /// </summary>
    /// <param name="model">The model to use for the agent.</param>
    /// <param name="tools">The tools available to the agent.</param>
    /// <param name="checkpointer">Optional checkpointer for state persistence.</param>
    /// <returns>A compiled graph that implements the ReAct agent.</returns>
    public static CompiledGraph<MessagesState> CreateReactAgent(
        IModel model, 
        IEnumerable<Tool> tools, 
        ICheckpointer? checkpointer = null)
    {
        // Create a tool node to handle tool execution
        var toolNode = new ToolNode(tools);

        // Define the function that determines whether to continue or not
        string ShouldContinue(MessagesState state)
        {
            var messages = state.Messages;
            var lastMessage = messages.LastOrDefault() as AIMessage;
            
            // If the LLM makes a tool call, then we route to the "tools" node
            if (lastMessage?.ToolCalls != null && lastMessage.ToolCalls.Any())
            {
                return "tools";
            }
            
            // Otherwise, we stop (reply to the user)
            return GraphConstants.END;
        }

        // Define the function that calls the model
        MessagesState CallModel(MessagesState state)
        {
            var response = model.Invoke(state.Messages);
            return new MessagesState(new[] { response });
        }

        // Define a new graph
        var workflow = new StateGraph<MessagesState>();

        // Define the two nodes we will cycle between
        workflow.AddNode("agent", CallModel);
        workflow.AddNode("tools", toolNode.Execute);

        // Set the entrypoint as `agent`
        workflow.AddEdge(GraphConstants.START, "agent");

        // Add a conditional edge from agent node
        workflow.AddConditionalEdges("agent", ShouldContinue);

        // Add a normal edge from `tools` to `agent`
        workflow.AddEdge("tools", "agent");

        // Compile the graph
        return workflow.Compile(checkpointer);
    }
}

/// <summary>
/// Interface for language models that can be used with agents.
/// </summary>
public interface IModel
{
    /// <summary>
    /// Invokes the model with the given messages and returns a response.
    /// </summary>
    /// <param name="messages">The messages to send to the model.</param>
    /// <returns>The model's response.</returns>
    Message Invoke(IEnumerable<Message> messages);
}