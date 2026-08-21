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

        /// <summary>
        /// GET /content/list/{type} with optional filter query params (web ContentFilters).
        /// Returns files and the filter option sets from the same response.
        /// </summary>
        Task<ContentCatalogPage> ListCatalogAsync(
            IReadOnlyDictionary<string, string>? filters = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Label dropdown options via POST /content/types/form + GET /content/references
        /// (same cascade path as constr-todo-web DynamicForm).
        /// </summary>
        Task<IReadOnlyList<FilterEntity>> GetLabelOptionsAsync(
            IReadOnlyDictionary<string, string>? listValues = null,
            CancellationToken cancellationToken = default);

        /// <summary>Alias of GetLabelOptionsAsync for older call sites.</summary>
        Task<IReadOnlyList<FilterEntity>> GetFiltersAsync(CancellationToken cancellationToken = default);

        /// <summary>POST /content/production-drawings/sizes</summary>
        Task CreatePanelSizeAsync(
            PanelSizeCreateRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>POST /content/production-drawings/perforations</summary>
        Task CreatePerforationAsync(
            BrandEntityCreateRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>POST /content/production-drawings/edges</summary>
        Task CreateEdgeAsync(
            BrandEntityCreateRequest request,
            CancellationToken cancellationToken = default);

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
        /// Updates content. Labels: PUT /content/{id} without renaming.
        /// Rename: pass newName (only from «Задать имя…»).
        /// DWG replace: create new + delete old, then restore existing code.
        /// knownDisplayName is a hint to preserve title when payload.name is stale.
        /// </summary>
        Task<DwgFileInfo> UpdateContentAsync(
            string contentId,
            string? newName = null,
            string? localDwgPath = null,
            string? dwgFieldCode = null,
            ProductionDrawingLabels? labels = null,
            string? knownDisplayName = null,
            IProgress<double>? progress = null,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// Creates new content via POST /content/{code} with DWG file and required labels.
        /// Name is optional: omit it so ConstrTodo assigns payload.code, then read it back.
        /// </summary>
        Task<DwgFileInfo> CreateContentAsync(
            string? name,
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

        /// <summary>
        /// GET /content/toApproval/active-preview/{id} — incomplete publication steps
        /// and assignees who have not decided yet.
        /// </summary>
        Task<IReadOnlyList<ContentApprovalStep>> GetActiveApprovalPreviewAsync(
            string contentId,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// PUT /content/toApproval/assignees/{id} — replace unsigned assignees of one step.
        /// </summary>
        Task UpdateApprovalAssigneesAsync(
            string contentId,
            UpdateApprovalAssigneesRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// PUT /content/withdraw/{id} — recall content from active publication approval.
        /// </summary>
        Task WithdrawApprovalAsync(
            string contentId,
            CancellationToken cancellationToken = default);
    }
}
