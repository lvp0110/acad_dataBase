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

        Task<IReadOnlyList<FilterEntity>> GetFiltersAsync(CancellationToken cancellationToken = default);

        /// <summary>Reads label field codes from GET /content/{id} payload.</summary>
        Task<ProductionDrawingLabels?> GetContentLabelsAsync(
            string contentId,
            CancellationToken cancellationToken = default);

        Task<string> DownloadFileAsync(
            DwgFileInfo file,
            string destinationDirectory,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates content via PUT /content/{id}.
        /// Rename: pass newName. Save DWG: pass localDwgPath (multipart).
        /// </summary>
        Task UpdateContentAsync(
            string contentId,
            string? newName = null,
            string? localDwgPath = null,
            string? dwgFieldCode = null,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates new content via POST /content/{code} with DWG file and required labels.
        /// </summary>
        Task<DwgFileInfo> CreateContentAsync(
            string name,
            string localDwgPath,
            ProductionDrawingLabels labels,
            string? dwgFieldCode = null,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Deletes content via DELETE /content/{id}. API allows draft/rejected only.
        /// </summary>
        Task DeleteContentAsync(string contentId, CancellationToken cancellationToken = default);
    }
}
