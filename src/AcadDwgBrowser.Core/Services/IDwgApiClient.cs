using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using AcadDwgBrowser.Core.Models;

namespace AcadDwgBrowser.Core.Services
{
    public interface IDwgApiClient
    {
        Task<IReadOnlyList<DwgFileInfo>> ListFilesAsync(CancellationToken cancellationToken = default);

        Task<string> DownloadFileAsync(
            DwgFileInfo file,
            string destinationDirectory,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default);
    }
}
