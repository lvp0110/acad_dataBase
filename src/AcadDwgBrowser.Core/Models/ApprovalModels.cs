using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AcadDwgBrowser.Core.Models
{
    public sealed class ApprovalPreviewResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("data")]
        public List<ApprovalPreviewStep> Data { get; set; } = new List<ApprovalPreviewStep>();

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    public sealed class ApprovalPreviewStep
    {
        [JsonPropertyName("policy_step_id")]
        public int PolicyStepId { get; set; }

        [JsonPropertyName("step_order")]
        public int StepOrder { get; set; }

        [JsonPropertyName("department_id")]
        public int DepartmentId { get; set; }

        [JsonPropertyName("required_signatures")]
        public int RequiredSignatures { get; set; }

        [JsonPropertyName("approval_users")]
        public List<ApprovalUser> ApprovalUsers { get; set; } = new List<ApprovalUser>();
    }

    public sealed class ApprovalUser
    {
        [JsonPropertyName("user_id")]
        public string UserId { get; set; } = string.Empty;

        [JsonPropertyName("full_name")]
        public string FullName { get; set; } = string.Empty;

        [JsonPropertyName("position")]
        public string Position { get; set; } = string.Empty;

        [JsonPropertyName("action")]
        public string? Action { get; set; }

        [JsonPropertyName("comment")]
        public string? Comment { get; set; }

        [JsonPropertyName("update_action_at")]
        public string? UpdateActionAt { get; set; }

        public override string ToString()
        {
            var name = string.IsNullOrWhiteSpace(FullName) ? UserId : FullName.Trim();
            if (string.IsNullOrWhiteSpace(Position))
                return name;
            return name + " — " + Position.Trim();
        }
    }

    public sealed class StartApprovalProcessRequest
    {
        [JsonPropertyName("steps")]
        public List<ApprovalStepAssignInput> Steps { get; set; } = new List<ApprovalStepAssignInput>();
    }

    public sealed class ApprovalStepAssignInput
    {
        [JsonPropertyName("policy_step_id")]
        public int PolicyStepId { get; set; }

        [JsonPropertyName("user_ids")]
        public List<string> UserIds { get; set; } = new List<string>();
    }

    /// <summary>models.ApprovalStep — from GET /content/{id} approvals.</summary>
    public sealed class ContentApprovalStep
    {
        [JsonPropertyName("process_step_id")]
        public string? ProcessStepId { get; set; }

        [JsonPropertyName("step_order")]
        public int StepOrder { get; set; }

        [JsonPropertyName("department_id")]
        public int DepartmentId { get; set; }

        [JsonPropertyName("required_signatures")]
        public int RequiredSignatures { get; set; }

        [JsonPropertyName("approval_users")]
        public List<ApprovalUser> ApprovalUsers { get; set; } = new List<ApprovalUser>();
    }
}
