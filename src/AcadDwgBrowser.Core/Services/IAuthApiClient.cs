using System.Threading;
using System.Threading.Tasks;
using AcadDwgBrowser.Core.Models;

namespace AcadDwgBrowser.Core.Services
{
    public interface IAuthApiClient
    {
        Task<AuthSession> LoginAsync(string email, string password, CancellationToken cancellationToken = default);

        Task<AuthSession> GetSessionAsync(AuthSession session, CancellationToken cancellationToken = default);

        Task<AuthSession> RefreshSessionAsync(AuthSession session, CancellationToken cancellationToken = default);

        Task<AuthSession> EnsureFreshCsrfAsync(AuthSession session, CancellationToken cancellationToken = default);

        Task LogoutAsync(AuthSession session, CancellationToken cancellationToken = default);
    }
}
