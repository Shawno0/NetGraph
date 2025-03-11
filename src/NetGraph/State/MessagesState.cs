using NetGraph.Messages;

namespace NetGraph.State;

/// <summary>
/// A state schema that maintains a list of messages.
/// </summary>
public class MessagesState : GraphState<MessagesState>
{
    /// <summary>
    /// Gets or sets the messages in the state.
    /// </summary>
    public List<Message> Messages { get; set; } = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagesState"/> class.
    /// </summary>
    public MessagesState()
    {
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="MessagesState"/> class with initial messages.
    /// </summary>
    /// <param name="messages">The initial messages.</param>
    public MessagesState(IEnumerable<Message> messages)
    {
        Messages = messages.ToList();
    }

    /// <summary>
    /// Merges a typed update into the current state.
    /// </summary>
    /// <param name="update">The typed update to merge.</param>
    protected override void MergeTyped(MessagesState update)
    {
        // Add any new messages from the update
        if (update.Messages != null && update.Messages.Any())
        {
            Messages.AddRange(update.Messages);
        }
    }

    /// <summary>
    /// Creates a deep copy of the current state.
    /// </summary>
    /// <returns>A new instance with the same data.</returns>
    public override IGraphState Clone()
    {
        return new MessagesState(Messages.Select(m => m.Clone()));
    }
}