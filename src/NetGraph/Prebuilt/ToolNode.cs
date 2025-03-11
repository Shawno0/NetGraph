using System.Text.Json;
using NetGraph.Messages;
using NetGraph.State;
using NetGraph.Tools;

namespace NetGraph.Prebuilt;

/// <summary>
/// A node that executes tools based on tool calls in messages.
/// </summary>
public class ToolNode
{
    private readonly Dictionary<string, Tool> _tools;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolNode"/> class.
    /// </summary>
    /// <param name="tools">The tools available to the node.</param>
    public ToolNode(IEnumerable<Tool> tools)
    {
        _tools = tools.ToDictionary(t => t.Name);
    }

    /// <summary>
    /// Processes the state and executes any tool calls.
    /// </summary>
    /// <param name="state">The current state.</param>
    /// <returns>Updates to the state.</returns>
    public MessagesState Execute(MessagesState state)
    {
        var lastMessage = state.Messages.LastOrDefault() as AIMessage;
        if (lastMessage == null || lastMessage.ToolCalls == null || !lastMessage.ToolCalls.Any())
        {
            // No tool calls to process
            return new MessagesState();
        }

        var toolMessages = new List<Message>();

        foreach (var toolCall in lastMessage.ToolCalls)
        {
            if (!_tools.TryGetValue(toolCall.Name, out var tool))
            {
                var errorMessage = $"Tool '{toolCall.Name}' not found";
                toolMessages.Add(new ToolMessage(toolCall.Id, errorMessage, toolCall.Name));
                continue;
            }

            try
            {
                var result = tool.Execute(toolCall.Arguments);
                toolMessages.Add(new ToolMessage(toolCall.Id, result, toolCall.Name));
            }
            catch (Exception ex)
            {
                toolMessages.Add(new ToolMessage(
                    toolCall.Id, 
                    $"Error executing tool: {ex.Message}", 
                    toolCall.Name));
            }
        }

        return new MessagesState(toolMessages);
    }
}