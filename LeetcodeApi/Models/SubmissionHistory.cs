using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace LeetcodeApi.Models
{
    public class SubmissionHistory
    {
        [JsonPropertyName("has_next")]
        public bool HasNext { get; set; }

        [JsonPropertyName("last_key")]
        public string LastKey { get; set; }

        [JsonPropertyName("submissions_dump")]
        public List<SubmissionHistoryEntry> Entries { get; set; }
    }
}
