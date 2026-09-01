using System.Text.Json.Serialization;

namespace AcadDwgBrowser.Core.Models
{
    /// <summary>POST /auth/login — rest.loginRequest from swagger.yaml</summary>
    public sealed class LoginRequest
    {
        [JsonPropertyName("email")]
        public string Email { get; set; } = string.Empty;

        [JsonPropertyName("password")]
        public string Password { get; set; } = string.Empty;
    }

    /// <summary>POST /auth/refresh — plugin body when refresh_token cookie is not used.</summary>
    public sealed class RefreshRequest
    {
        [JsonPropertyName("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;
    }
}
