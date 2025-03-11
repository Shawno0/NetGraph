using System.Reflection;
using NetGraph.Checkpoint;
using NetGraph.Messages;
using NetGraph.Models;
using NetGraph.Prebuilt;
using NetGraph.State;
using NetGraph.Tools;

namespace NetGraph.Examples;

public class Program
{
    public static void Main(string[] args)
    {
        // Set your OpenAI API key
        // string apiKey = Environment.GetEnvironmentVariable("OPENAI_API_KEY") 
        //     ?? throw new InvalidOperationException("OPENAI_API_KEY environment variable is not set");

        string apiKey = "AIzaSyCjCZIHjD7P1f4k32IlwtB5ZQSJzwxvxfQ";

        // Create the model
        var model = new GeminiModel(apiKey, "gemini-2.0-flash", 0.0f);

        // Define the tools
        var searchTool = new Tool(
            "search",
            "Call to surf the web.",
            Search
        );

        // Bind tools to the model
        var toolModel = model.BindTools(new[] { searchTool });

        // Initialize memory to persist state between graph runs
        var checkpointer = new MemorySaver();

        // Create the ReAct agent
        var app = ReActAgent.CreateReactAgent(toolModel, new[] { searchTool }, checkpointer);

        // Use the agent
        Console.WriteLine("Using the agent to check weather in SF...");
        var initialThreadId = 42;
        var state = app.Invoke(
            new MessagesState(new[] { new UserMessage("what is the weather in sf") }),
            new Dictionary<string, object>
            {
                ["configurable"] = new Dictionary<string, object>
                {
                    ["thread_id"] = initialThreadId
                }
            }
        );

        // Print the result
        Console.WriteLine("\nAgent response:");
        foreach (var message in state.Messages)
        {
            if (message is AIMessage aiMessage)
            {
                Console.WriteLine($"AI: {aiMessage.Content}");
            }
            else if (message is UserMessage userMessage)
            {
                Console.WriteLine($"User: {userMessage.Content}");
            }
        }

        // Continue the conversation with the same thread ID
        Console.WriteLine("\nContinuing the conversation to ask about NY...");
        state = app.Invoke(
            new MessagesState(new[] { new UserMessage("what about ny") }),
            new Dictionary<string, object>
            {
                ["configurable"] = new Dictionary<string, object>
                {
                    ["thread_id"] = initialThreadId
                }
            }
        );

        // Print the result
        Console.WriteLine("\nAgent response:");
        foreach (var message in state.Messages)
        {
            if (message is AIMessage aiMessage && message == state.Messages.Last())
            {
                Console.WriteLine($"AI: {aiMessage.Content}");
            }
            else if (message is UserMessage userMessage && message == state.Messages.First())
            {
                Console.WriteLine($"User: {userMessage.Content}");
            }
        }
    }

    [Tool(Description = "Call to surf the web.")]
    public static string Search(string query)
    {
        // This is a placeholder, same as in the Python example
        if (query.ToLower().Contains("sf") || query.ToLower().Contains("san francisco"))
        {
            return "It's 60 degrees and foggy.";
        }
        return "It's 90 degrees and sunny.";
    }
}