using Newtonsoft.Json;

namespace VoiceBot.Models
{
    public class GroqChatResponse
    {
        [JsonProperty("choices")]
        public List<GroqChoice> Choices { get; set; }
    }

    public class GroqChoice
    {
        [JsonProperty("message")]
        public GroqMessage Message { get; set; }
    }

    public class GroqMessage
    {
        [JsonProperty("role")]
        public string Role { get; set; }

        [JsonProperty("content")]
        public string Content { get; set; }
    }

}
