using System;
using AcadDwgBrowser.Core.Models;

namespace AcadDwgBrowser.Core.Services
{
    /// <summary>
    /// Session from POST /auth/login: access_token + refresh_token + csrf_token (swagger ConstrTodo auth).
    /// </summary>
    public sealed class AuthSession
    {
        public string AccessToken { get; set; } = string.Empty;

        public string RefreshToken { get; set; } = string.Empty;

        public string CsrfToken { get; set; } = string.Empty;

        public string Email { get; set; } = string.Empty;

        public DateTimeOffset? AccessExpiresAt { get; set; }

        public DateTimeOffset? RefreshExpiresAt { get; set; }

        public UserFullInfo? User { get; set; }

        public bool IsAuthenticated =>
            !string.IsNullOrWhiteSpace(AccessToken)
            || User != null
            || HasRefreshToken;

        public bool HasRefreshToken => ApiHttpFactory.IsRealToken(RefreshToken);

        public bool IsAccessExpiring(TimeSpan skew)
        {
            if (!ApiHttpFactory.IsRealToken(AccessToken))
                return true;
            if (!AccessExpiresAt.HasValue)
                return false;
            return AccessExpiresAt.Value <= DateTimeOffset.UtcNow.Add(skew);
        }
    }
}
