using System.Text.Json.Serialization;

namespace NetGraph.Messages;

/// <summary>
/// Represents a message role.
/// </summary>
public enum MessageRole
{
    System,
    User,
    Assistant,
    Tool,
    Function
}

/// <summary>
/// Represents a tool call in a message.
/// </summary>
public class ToolCall
{
    /// <summary>
    /// Gets or sets the unique ID of the tool call.
    /// </summary>
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the name of the tool being called.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the arguments to pass to the tool.
    /// </summary>
    [JsonPropertyName("arguments")]
    public string Arguments { get; set; } = string.Empty;
}

/// <summary>
/// Base class for all message types.
/// </summary>
public abstract class Message
{
    /// <summary>
    /// Gets or sets the role of the message sender.
    /// </summary>
    [JsonPropertyName("role")]
    public MessageRole Role { get; set; }

    /// <summary>
    /// Gets or sets the content of the message.
    /// </summary>
    [JsonPropertyName("content")]
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the tool calls in the message.
    /// </summary>
    [JsonPropertyName("tool_calls")]
    public List<ToolCall>? ToolCalls { get; set; }
    
    /// <summary>
    /// Creates a deep clone of this message.
    /// </summary>
    public abstract Message Clone();
}

/// <summary>
/// Represents a system message.
/// </summary>
public class SystemMessage : Message
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SystemMessage"/> class.
    /// </summary>
    public SystemMessage()
    {
        Role = MessageRole.System;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="SystemMessage"/> class with the specified content.
    /// </summary>
    /// <param name="content">The content of the message.</param>
    public SystemMessage(string content) : this()
    {
        Content = content;
    }

    /// <summary>
    /// Creates a deep clone of this message.
    /// </summary>
    public override Message Clone()
    {
        var clone = new SystemMessage(Content);
        return clone;
    }
}

/// <summary>
/// Represents a user message.
/// </summary>
public class UserMessage : Message
{
    /// <summary>
    /// Initializes a new instance of the <see cref="UserMessage"/> class.
    /// </summary>
    public UserMessage()
    {
        Role = MessageRole.User;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="UserMessage"/> class with the specified content.
    /// </summary>
    /// <param name="content">The content of the message.</param>
    public UserMessage(string content) : this()
    {
        Content = content;
    }

    /// <summary>
    /// Creates a deep clone of this message.
    /// </summary>
    public override Message Clone()
    {
        var clone = new UserMessage(Content);
        return clone;
    }
}

/// <summary>
/// Represents an AI assistant message.
/// </summary>
public class AIMessage : Message
{
    /// <summary>
    /// Initializes a new instance of the <see cref="AIMessage"/> class.
    /// </summary>
    public AIMessage()
    {
        Role = MessageRole.Assistant;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AIMessage"/> class with the specified content.
    /// </summary>
    /// <param name="content">The content of the message.</param>
    public AIMessage(string content) : this()
    {
        Content = content;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="AIMessage"/> class with content and tool calls.
    /// </summary>
    /// <param name="content">The content of the message.</param>
    /// <param name="toolCalls">The tool calls in the message.</param>
    public AIMessage(string content, List<ToolCall> toolCalls) : this(content)
    {
        ToolCalls = toolCalls;
    }

    /// <summary>
    /// Creates a deep clone of this message.
    /// </summary>
    public override Message Clone()
    {
        var clone = new AIMessage(Content);
        if (ToolCalls != null)
        {
            clone.ToolCalls = ToolCalls.Select(tc => new ToolCall
            {
                Id = tc.Id,
                Name = tc.Name,
                Arguments = tc.Arguments
            }).ToList();
        }
        return clone;
    }
}

/// <summary>
/// Represents a tool message (response from a tool).
/// </summary>
public class ToolMessage : Message
{
    /// <summary>
    /// Gets or sets the ID of the tool call this message is responding to.
    /// </summary>
    [JsonPropertyName("tool_call_id")]
    public string ToolCallId { get; set; } = string.Empty;
    
    /// <summary>
    /// Gets or sets the name of the tool.
    /// </summary>
    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolMessage"/> class.
    /// </summary>
    public ToolMessage()
    {
        Role = MessageRole.Tool;
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ToolMessage"/> class with the specified parameters.
    /// </summary>
    /// <param name="toolCallId">The ID of the tool call this message is responding to.</param>
    /// <param name="content">The content of the message (tool result).</param>
    /// <param name="name">The name of the tool.</param>
    public ToolMessage(string toolCallId, string content, string name) : this()
    {
        ToolCallId = toolCallId;
        Content = content;
        Name = name;
    }

    /// <summary>
    /// Creates a deep clone of this message.
    /// </summary>
    public override Message Clone()
    {
        return new ToolMessage(ToolCallId, Content, Name);
    }
}