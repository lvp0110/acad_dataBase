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
                var name = ((FirstName ?? string.Empty) + " " + (LastName ?? string.Empty)).Trim();
                if (!string.IsNullOrEmpty(name))
                    return name;
                return Email ?? UserId ?? "пользователь";
            }
        }
    }
}
