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

        /// <summary>Reads rejection comment from GET /content/{id} approvals.</summary>
        Task<string?> GetRejectionCommentAsync(
            string contentId,
            CancellationToken cancellationToken = default);

        /// <summary>One GET /content/{id}: labels + rejection comment.</summary>
        Task<ContentMetaInfo> GetContentMetaAsync(
            string contentId,
            CancellationToken cancellationToken = default);

        Task<string> DownloadFileAsync(
            DwgFileInfo file,
            string destinationDirectory,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Updates content. Labels/rename: PUT /content/{id} without file.
        /// DWG replace: API PUT+file is broken server-side, so create new + delete old.
        /// Returns the resulting catalog item (may have a new Id after DWG replace).
        /// </summary>
        Task<DwgFileInfo> UpdateContentAsync(
            string contentId,
            string? newName = null,
            string? localDwgPath = null,
            string? dwgFieldCode = null,
            ProductionDrawingLabels? labels = null,
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

        /// <summary>GET /content/toApproval/preview/{id} — steps and available approvers.</summary>
        Task<IReadOnlyList<ApprovalPreviewStep>> GetApprovalPreviewAsync(
            string contentId,
            CancellationToken cancellationToken = default);

        /// <summary>POST /content/toApproval/{id} — start publication approval (draft only).</summary>
        Task StartApprovalAsync(
            string contentId,
            StartApprovalProcessRequest request,
            CancellationToken cancellationToken = default);
    }
}
