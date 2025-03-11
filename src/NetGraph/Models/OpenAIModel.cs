using System.Reflection;
using System.Text;
using System.Text.Json;
using NetGraph.Messages;
using NetGraph.Prebuilt;
using NetGraph.Tools;

namespace NetGraph.Models;

/// <summary>
/// OpenAI API integration for NetGraph.
/// </summary>
public class OpenAIModel : IModel
{
    private readonly HttpClient _client;
    private readonly string _model;
    private readonly float _temperature;
    private readonly List<Tool>? _tools;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="OpenAIModel"/> class.
    /// </summary>
    /// <param name="apiKey">The OpenAI API key.</param>
    /// <param name="model">The model to use (e.g., "gpt-4o").</param>
    /// <param name="temperature">The sampling temperature to use.</param>
    public OpenAIModel(string apiKey, string model = "gpt-4o", float temperature = 0)
    {
        _client = new HttpClient();
        _client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
        _model = model;
        _temperature = temperature;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };
    }

    /// <summary>
    /// Binds tools to the model for tool calling.
    /// </summary>
    /// <param name="tools">The tools to bind.</param>
    /// <returns>A new OpenAIModel instance with the tools bound.</returns>
    public OpenAIModel BindTools(IEnumerable<Tool> tools)
    {
        var newModel = new OpenAIModel(
            apiKey: _client.DefaultRequestHeaders.Authorization!.Parameter!,
            model: _model,
            temperature: _temperature)
        {
        };

        newModel.BindTools(tools);
        
        return newModel;
    }

    /// <summary>
    /// Invokes the model with the given messages and returns a response.
    /// </summary>
    /// <param name="messages">The messages to send to the model.</param>
    /// <returns>The model's response.</returns>
    public Message Invoke(IEnumerable<Message> messages)
    {
        var request = new
        {
            model = _model,
            messages = messages.Select(ConvertMessage),
            temperature = _temperature,
            tools = _tools?.Select(ConvertTool).ToList()
        };

        var requestJson = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        
        var response = _client.PostAsync("https://api.openai.com/v1/chat/completions", content).Result;
        response.EnsureSuccessStatusCode();
        
        var responseJson = response.Content.ReadAsStringAsync().Result;
        var completionResponse = JsonSerializer.Deserialize<OpenAICompletionResponse>(responseJson, _jsonOptions);
        
        if (completionResponse?.Choices == null || completionResponse.Choices.Count == 0)
        {
            throw new InvalidOperationException("No response from model");
        }
        
        var choice = completionResponse.Choices[0];
        var messageContent = choice.Message.Content ?? string.Empty;
        var toolCalls = choice.Message.ToolCalls?.Select(tc => new ToolCall
        {
            Id = tc.Id,
            Name = tc.Function.Name,
            Arguments = tc.Function.Arguments
        }).ToList();
        
        return new AIMessage(messageContent, toolCalls);
    }
    
    private object ConvertMessage(Message message)
    {
        switch (message)
        {
            case SystemMessage systemMessage:
                return new { role = "system", content = systemMessage.Content };
            
            case UserMessage userMessage:
                return new { role = "user", content = userMessage.Content };
            
            case AIMessage aiMessage:
                var aiResult = new Dictionary<string, object>
                {
                    { "role", "assistant" },
                    { "content", aiMessage.Content ?? string.Empty }
                };
                
                if (aiMessage.ToolCalls != null && aiMessage.ToolCalls.Any())
                {
                    aiResult["tool_calls"] = aiMessage.ToolCalls.Select(tc => new
                    {
                        id = tc.Id,
                        type = "function",
                        function = new { name = tc.Name, arguments = tc.Arguments }
                    }).ToList();
                }
                
                return aiResult;
            
            case ToolMessage toolMessage:
                return new
                {
                    role = "tool",
                    tool_call_id = toolMessage.ToolCallId,
                    content = toolMessage.Content
                };
            
            default:
                throw new ArgumentException($"Unsupported message type: {message.GetType().Name}");
        }
    }
    
    private object ConvertTool(Tool tool)
    {
        return new
        {
            type = "function",
            function = new
            {
                name = tool.Name,
                description = tool.Description,
                parameters = GenerateJsonSchema(tool.Parameters)
            }
        };
    }
    
    private object GenerateJsonSchema(ParameterInfo[] parameters)
    {
        if (parameters.Length == 0)
        {
            return new { type = "object", properties = new { } };
        }
        
        var properties = new Dictionary<string, object>();
        var required = new List<string>();
        
        foreach (var param in parameters)
        {
            var paramType = param.ParameterType;
            properties[param.Name!] = GetJsonSchemaForType(paramType);
            
            if (!param.HasDefaultValue && !paramType.IsClass)
            {
                required.Add(param.Name!);
            }
        }
        
        var schema = new Dictionary<string, object>
        {
            { "type", "object" },
            { "properties", properties }
        };
        
        if (required.Count > 0)
        {
            schema["required"] = required;
        }
        
        return schema;
    }
    
    private object GetJsonSchemaForType(Type type)
    {
        if (type == typeof(string))
        {
            return new { type = "string" };
        }
        else if (type == typeof(int) || type == typeof(long))
        {
            return new { type = "integer" };
        }
        else if (type == typeof(float) || type == typeof(double) || type == typeof(decimal))
        {
            return new { type = "number" };
        }
        else if (type == typeof(bool))
        {
            return new { type = "boolean" };
        }
        else if (type.IsArray || (type.IsGenericType && typeof(IEnumerable<>).IsAssignableFrom(type.GetGenericTypeDefinition())))
        {
            var elementType = type.IsArray 
                ? type.GetElementType()! 
                : type.GetGenericArguments()[0];
            
            return new
            {
                type = "array",
                items = GetJsonSchemaForType(elementType)
            };
        }
        else
        {
            // For complex types, we'll just use a generic object schema
            return new { type = "object" };
        }
    }
}

/// <summary>
/// Response from the OpenAI API for chat completions.
/// </summary>
internal class OpenAICompletionResponse
{
    public List<OpenAIChoice> Choices { get; set; } = new();
}

/// <summary>
/// A choice in a completion response.
/// </summary>
internal class OpenAIChoice
{
    public OpenAIMessage Message { get; set; } = new();
}

/// <summary>
/// Message in a completion response.
/// </summary>
internal class OpenAIMessage
{
    public string? Content { get; set; }
    public List<OpenAIToolCall>? ToolCalls { get; set; }
}

/// <summary>
/// Tool call in a message.
/// </summary>
internal class OpenAIToolCall
{
    public string Id { get; set; } = string.Empty;
    public string Type { get; set; } = "function";
    public OpenAIFunction Function { get; set; } = new();
}

/// <summary>
/// Function in a tool call.
/// </summary>
internal class OpenAIFunction
{
    public string Name { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
}