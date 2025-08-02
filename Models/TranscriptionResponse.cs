using Newtonsoft.Json;
using System.Text.Json.Serialization;

namespace VoiceBot.Models
{
    public class TranscriptionResponse
    {
        [JsonProperty("task")]
        public string Task { get; set; }

        [JsonProperty("language")]
        public string Language { get; set; }

        [JsonProperty("duration")]
        public double Duration { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("segments")]
        public List<Segment> Segments { get; set; }

        [JsonProperty("x_groq")]
        public XGroq XGroq { get; set; }
    }

    public class Segment
    {
        [JsonProperty("id")]
        public int Id { get; set; }

        [JsonProperty("seek")]
        public int Seek { get; set; }

        [JsonProperty("start")]
        public double Start { get; set; }

        [JsonProperty("end")]
        public double End { get; set; }

        [JsonProperty("text")]
        public string Text { get; set; }

        [JsonProperty("tokens")]
        public List<int> Tokens { get; set; }

        [JsonProperty("temperature")]
        public double Temperature { get; set; }

        [JsonProperty("avg_logprob")]
        public double AvgLogprob { get; set; }

        [JsonProperty("compression_ratio")]
        public double CompressionRatio { get; set; }

        [JsonProperty("no_speech_prob")]
        public double NoSpeechProb { get; set; }
    }

    public class XGroq
    {
        [JsonProperty("id")]
        public string Id { get; set; }
    }

}
