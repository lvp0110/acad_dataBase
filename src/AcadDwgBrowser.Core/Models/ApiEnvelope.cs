using System.Text.Json.Serialization;

namespace AcadDwgBrowser.Core.Models
{
    /// <summary>swagger.SuccessAuthResponse / ErrorResponse envelope</summary>
    public sealed class ApiEnvelope<T>
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("data")]
        public T? Data { get; set; }

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}
