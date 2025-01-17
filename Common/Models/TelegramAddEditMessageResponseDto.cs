using System.Text.Json.Serialization;

namespace Common.Models;

public class TelegramAddEditMessageResponseDto
{
    [JsonPropertyName("result")]
    public required ResponseResult Result { get; set; }

    public class ResponseResult
    {
        [JsonPropertyName("message_id")]
        public long MessageId { get; set; }
    }
}