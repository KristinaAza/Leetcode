using System;
using System.Text.Json.Serialization;

namespace LeetcodeApi.Models
{
    public class SubmissionHistoryEntry
    {
        [JsonPropertyName("status_display")]
        public string StatusDisplay { get; set; }

        [JsonPropertyName("title")]
        public string Title { get; set; }

        [JsonPropertyName("timestamp")]
        public int Timestamp { get; set; }

        public DateTime CreatedDateTime => DateTimeOffset.FromUnixTimeSeconds(Timestamp).DateTime;
    }
}
