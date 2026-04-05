namespace SDRSharp.RFWhisperer.Models
{
    public enum MessageRole { User, Assistant, System, ToolResult }

    public class ChatMessage
    {
        public MessageRole Role { get; set; }
        public string Content { get; set; } = "";
        public DateTime Timestamp { get; set; } = DateTime.Now;

        // For tool use tracking
        public string? ToolName { get; set; }
        public string? ToolCallId { get; set; }
        public bool IsToolCall { get; set; }
    }
}
