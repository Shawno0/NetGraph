using System.Reflection;
using System.Text;
using System.Text.Json;
using NetGraph.Messages;
using NetGraph.Prebuilt;
using NetGraph.Tools;

namespace NetGraph.Models;

/// <summary>
/// Gemini API integration for NetGraph.
/// </summary>
public class GeminiModel : IModel
{
    private readonly HttpClient _client;
    private readonly string _apiKey;
    private readonly string _model;
    private readonly float _temperature;
    private List<Tool>? _tools;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="GeminiModel"/> class.
    /// </summary>
    /// <param name="apiKey">The Gemini API key.</param>
    /// <param name="model">The model to use (e.g., "gemini-2.0-pro-exp").</param>
    /// <param name="temperature">The sampling temperature to use.</param>
    public GeminiModel(string apiKey, string model = "gemini-2.0-flash", float temperature = 0)
    {
        _client = new HttpClient();
        _apiKey = apiKey;
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
    /// <returns>A new GeminiModel instance with the tools bound.</returns>
    public GeminiModel BindTools(IEnumerable<Tool> tools)
    {
        var newModel = new GeminiModel(
            apiKey: _apiKey,
            model: _model,
            temperature: _temperature);
        
        newModel._tools = tools.ToList();
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
            contents = messages.Select(ConvertMessage).ToArray(),
            tools = _tools?.Select(ConvertTool).ToList(),
            generationConfig = new { temperature = _temperature }
        };

        var requestJson = JsonSerializer.Serialize(request, _jsonOptions);
        var content = new StringContent(requestJson, Encoding.UTF8, "application/json");
        
        var response = _client.PostAsync($"https://generativelanguage.googleapis.com/v1beta/models/{_model}:generateContent?key={_apiKey}", content).Result;
        response.EnsureSuccessStatusCode();
        
        var responseJson = response.Content.ReadAsStringAsync().Result;
        var completionResponse = JsonSerializer.Deserialize<GeminiCompletionResponse>(responseJson, _jsonOptions);
        
        if (completionResponse?.Candidates == null || completionResponse.Candidates.Count == 0)
        {
            throw new InvalidOperationException("No response from model");
        }
        
        var candidate = completionResponse.Candidates[0];
        var messageContent = candidate.Content?.Text ?? string.Empty;
        var toolCalls = candidate.Content?.ToolCalls?.Select(tc => new ToolCall
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
                return new { role = "system", parts = new[] { new { text = systemMessage.Content } } };
            
            case UserMessage userMessage:
                return new { role = "user", parts = new[] { new { text = userMessage.Content } } };
            
            case AIMessage aiMessage:
                var aiResult = new Dictionary<string, object>
                {
                    { "role", "model" },
                    { "parts", new[] { new { text = aiMessage.Content ?? string.Empty } } }
                };
                
                if (aiMessage.ToolCalls != null && aiMessage.ToolCalls.Any())
                {
                    aiResult["toolCalls"] = aiMessage.ToolCalls.Select(tc => new
                    {
                        id = tc.Id,
                        functionCall = new { name = tc.Name, args = tc.Arguments }
                    }).ToList();
                }
                
                return aiResult;
            
            case ToolMessage toolMessage:
                return new
                {
                    role = "tool",
                    toolCallId = toolMessage.ToolCallId,
                    parts = new[] { new { text = toolMessage.Content } }
                };
            
            default:
                throw new ArgumentException($"Unsupported message type: {message.GetType().Name}");
        }
    }
    
    private object ConvertTool(Tool tool)
    {
        return new
        {
            tool_code = new
            {
                function = new
                {
                    name = tool.Name,
                    description = tool.Description,
                    parameters = GenerateJsonSchema(tool.Parameters)
                }
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
/// Response from the Gemini API for content generation.
/// </summary>
internal class GeminiCompletionResponse
{
    public List<GeminiCandidate> Candidates { get; set; } = new();
}

/// <summary>
/// A candidate in a completion response.
/// </summary>
internal class GeminiCandidate
{
    public GeminiContent? Content { get; set; }
}

/// <summary>
/// Content in a completion response.
/// </summary>
internal class GeminiContent
{
    public string? Text { get; set; }
    public List<GeminiToolCall>? ToolCalls { get; set; }
}

/// <summary>
/// Tool call in a message.
/// </summary>
internal class GeminiToolCall
{
    public string Id { get; set; } = string.Empty;
    public GeminiFunction Function { get; set; } = new();
}

/// <summary>
/// Function in a tool call.
/// </summary>
internal class GeminiFunction
{
    public string Name { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
}
