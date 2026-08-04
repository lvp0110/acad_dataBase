using AcadDwgBrowser.Core.Models;

namespace AcadDwgBrowser.Core.Services
{
    /// <summary>
    /// Session from POST /login: cookies access_token + csrf_token (swagger ConstrTodo auth).
    /// </summary>
    public sealed class AuthSession
    {
        public string AccessToken { get; set; } = string.Empty;

        public string CsrfToken { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public UserFullInfo? User { get; set; }

        public bool IsAuthenticated =>
            !string.IsNullOrWhiteSpace(AccessToken) || User != null;
    }
}
