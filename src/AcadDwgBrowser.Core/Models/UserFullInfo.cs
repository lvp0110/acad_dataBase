using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AcadDwgBrowser.Core.Models
{
    /// <summary>models.UserFullInfo from swagger.yaml</summary>
    public sealed class UserFullInfo
    {
        [JsonPropertyName("user_id")]
        public string? UserId { get; set; }

        [JsonPropertyName("email")]
        public string? Email { get; set; }

        [JsonPropertyName("first_name")]
        public string? FirstName { get; set; }

        [JsonPropertyName("last_name")]
        public string? LastName { get; set; }

        [JsonPropertyName("middle_name")]
        public string? MiddleName { get; set; }

        [JsonPropertyName("role_type")]
        public string? RoleType { get; set; }

        [JsonPropertyName("position_type")]
        public string? PositionType { get; set; }

        [JsonPropertyName("is_active")]
        public bool? IsActive { get; set; }

        [JsonPropertyName("department_id")]
        public int? DepartmentId { get; set; }

        [JsonPropertyName("position_id")]
        public int? PositionId { get; set; }

        public string DisplayName
        {
            get
            {
                var parts = new List<string>();
                if (!string.IsNullOrWhiteSpace(LastName)) parts.Add(LastName!.Trim());
                if (!string.IsNullOrWhiteSpace(FirstName)) parts.Add(FirstName!.Trim());
                if (!string.IsNullOrWhiteSpace(MiddleName)) parts.Add(MiddleName!.Trim());
                if (parts.Count > 0)
                    return string.Join(" ", parts);
                return Email ?? UserId ?? "пользователь";
            }
        }
    }

    /// <summary>swagger.UsersListsResponse</summary>
    public sealed class UsersListsResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("data")]
        public List<UserFullInfo> Data { get; set; } = new List<UserFullInfo>();

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }
}
