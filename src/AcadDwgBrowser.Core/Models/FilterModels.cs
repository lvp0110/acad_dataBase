using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace AcadDwgBrowser.Core.Models
{
    /// <summary>swagger.FiltersResponse</summary>
    public sealed class FiltersResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("data")]
        public List<FilterEntity> Data { get; set; } = new List<FilterEntity>();

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    /// <summary>models.FilterEntity</summary>
    public sealed class FilterEntity
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("options")]
        public List<FilterOption> Options { get; set; } = new List<FilterOption>();
    }

    /// <summary>models.FilterOption</summary>
    public sealed class FilterOption
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        public override string ToString() =>
            string.IsNullOrWhiteSpace(Name) ? Code : Name;
    }

    /// <summary>Selected production-drawing form labels for create/update.</summary>
    public sealed class ProductionDrawingLabels
    {
        public string UserUuid { get; set; } = string.Empty;
        public string BrandCode { get; set; } = string.Empty;
        public string ModelCode { get; set; } = string.Empty;
        public string GlobalCategoryCode { get; set; } = string.Empty;
        public string EdgeCode { get; set; } = string.Empty;
        public string PanelSizeCode { get; set; } = string.Empty;
        public string PerforationCode { get; set; } = string.Empty;

        public bool IsComplete =>
            !string.IsNullOrWhiteSpace(UserUuid)
            && !string.IsNullOrWhiteSpace(BrandCode)
            && !string.IsNullOrWhiteSpace(ModelCode)
            && !string.IsNullOrWhiteSpace(GlobalCategoryCode)
            && !string.IsNullOrWhiteSpace(EdgeCode)
            && !string.IsNullOrWhiteSpace(PanelSizeCode)
            && !string.IsNullOrWhiteSpace(PerforationCode);

        public string? MissingFieldName()
        {
            if (string.IsNullOrWhiteSpace(UserUuid)) return "Заказчик";
            if (string.IsNullOrWhiteSpace(GlobalCategoryCode)) return "Категория";
            if (string.IsNullOrWhiteSpace(BrandCode)) return "Бренд";
            if (string.IsNullOrWhiteSpace(ModelCode)) return "Модель";
            if (string.IsNullOrWhiteSpace(PerforationCode)) return "Перфорация";
            if (string.IsNullOrWhiteSpace(EdgeCode)) return "Кромка";
            if (string.IsNullOrWhiteSpace(PanelSizeCode)) return "Размер панели";
            return null;
        }
    }
}
