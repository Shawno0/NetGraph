using System.Linq.Expressions;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace NetGraph.Tools;

/// <summary>
/// Attribute for marking methods as tools.
/// </summary>
[AttributeUsage(AttributeTargets.Method)]
public class ToolAttribute : Attribute
{
    /// <summary>
    /// Gets or sets the name of the tool.
    /// </summary>
    public string? Name { get; set; }
    
    /// <summary>
    /// Gets or sets the description of the tool.
    /// </summary>
    public string? Description { get; set; }
}

/// <summary>
/// Represents a tool that can be used by an agent.
/// </summary>
public class Tool
{
    /// <summary>
    /// Gets the name of the tool.
    /// </summary>
    public string Name { get; }
    
    /// <summary>
    /// Gets the description of the tool.
    /// </summary>
    public string Description { get; }
    
    /// <summary>
    /// Gets the delegate that executes the tool.
    /// </summary>
    public Delegate Executor { get; }
    
    /// <summary>
    /// Gets the parameter information for the tool.
    /// </summary>
    public ParameterInfo[] Parameters { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="Tool"/> class.
    /// </summary>
    /// <param name="name">The name of the tool.</param>
    /// <param name="description">The description of the tool.</param>
    /// <param name="executor">The delegate that executes the tool.</param>
    public Tool(string name, string description, Delegate executor)
    {
        Name = name;
        Description = description;
        Executor = executor;
        Parameters = executor.Method.GetParameters();
    }

    /// <summary>
    /// Executes the tool with the given arguments.
    /// </summary>
    /// <param name="arguments">The arguments for the tool, as a JSON string.</param>
    /// <returns>The result of the tool execution.</returns>
    public string Execute(string arguments)
    {
        try
        {
            var options = new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true,
                Converters = { new JsonStringEnumConverter() }
            };
            
            // Parse arguments based on parameter types
            var parameters = new object?[Parameters.Length];
            
            if (Parameters.Length == 1)
            {
                // Single parameter - try to deserialize directly
                var paramType = Parameters[0].ParameterType;
                
                if (paramType == typeof(string))
                {
                    parameters[0] = arguments;
                }
                else
                {
                    parameters[0] = JsonSerializer.Deserialize(arguments, paramType, options);
                }
            }
            else if (Parameters.Length > 0)
            {
                // Multiple parameters - expect a JSON object with named parameters
                var argsDict = JsonSerializer.Deserialize<Dictionary<string, JsonElement>>(arguments, options);
                
                if (argsDict == null)
                {
                    throw new InvalidOperationException("Failed to parse arguments");
                }

                for (int i = 0; i < Parameters.Length; i++)
                {
                    var param = Parameters[i];
                    if (argsDict.TryGetValue(param.Name!, out var value))
                    {
                        parameters[i] = value.Deserialize(param.ParameterType, options);
                    }
                    else if (param.HasDefaultValue)
                    {
                        parameters[i] = param.DefaultValue;
                    }
                    else
                    {
                        throw new ArgumentException($"Missing required parameter '{param.Name}'");
                    }
                }
            }

            // Invoke the tool
            var result = Executor.DynamicInvoke(parameters);
            
            // Convert result to string if it's not already
            return result switch
            {
                string str => str,
                null => string.Empty,
                _ => JsonSerializer.Serialize(result, options)
            };
        }
        catch (Exception ex)
        {
            return $"Error executing tool: {ex.Message}";
        }
    }
}

/// <summary>
/// Factory for creating tools from methods.
/// </summary>
public static class ToolFactory
{
    /// <summary>
    /// Creates a tool from a method.
    /// </summary>
    /// <param name="method">The method info.</param>
    /// <param name="target">The target object (for instance methods).</param>
    /// <returns>The created tool.</returns>
    public static Tool FromMethod(MethodInfo method, object? target = null)
    {
        var toolAttr = method.GetCustomAttribute<ToolAttribute>();
        if (toolAttr == null)
        {
            throw new ArgumentException("Method does not have ToolAttribute", nameof(method));
        }

        string name = toolAttr.Name ?? method.Name;
        string description = toolAttr.Description ?? "No description provided.";
        
        Delegate executor = method.IsStatic 
            ? Delegate.CreateDelegate(Expression.GetDelegateType(
                method.GetParameters().Select(p => p.ParameterType).Append(method.ReturnType).ToArray()), 
                method)
            : Delegate.CreateDelegate(Expression.GetDelegateType(
                method.GetParameters().Select(p => p.ParameterType).Append(method.ReturnType).ToArray()), 
                target!, method);

        return new Tool(name, description, executor);
    }
}