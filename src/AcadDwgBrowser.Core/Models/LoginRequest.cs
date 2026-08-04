using System.Text.Json.Serialization;

namespace AcadDwgBrowser.Core.Models
{
    /// <summary>POST /login — rest.loginRequest from swagger.yaml</summary>
    public sealed class LoginRequest
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;
    }
}
