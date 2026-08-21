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

    /// <summary>models.FilterOption / models.Option</summary>
    public sealed class FilterOption
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        public override string ToString() =>
            string.IsNullOrWhiteSpace(Name) ? Code : Name;
    }

    /// <summary>swagger.OptionsResponse — GET /content/references/{code}</summary>
    public sealed class OptionsResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("data")]
        public List<FilterOption> Data { get; set; } = new List<FilterOption>();

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    /// <summary>swagger.EntitiesListResponse</summary>
    public sealed class EntitiesListResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("data")]
        public List<EntityItem> Data { get; set; } = new List<EntityItem>();

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    /// <summary>models.Entity</summary>
    public sealed class EntityItem
    {
        [JsonPropertyName("Code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("Name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("Type")]
        public string? Type { get; set; }
    }

    /// <summary>swagger response for /api/v2/globalMenuCategories</summary>
    public sealed class GlobalMenuCategoriesResponse
    {
        [JsonPropertyName("code")]
        public int Code { get; set; }

        [JsonPropertyName("data")]
        public List<GlobalMenuCategory> Data { get; set; } = new List<GlobalMenuCategory>();

        [JsonPropertyName("error")]
        public string? Error { get; set; }
    }

    public sealed class GlobalMenuCategory
    {
        [JsonPropertyName("type")]
        public string Type { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;
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

        public bool HasAnyValue =>
            !string.IsNullOrWhiteSpace(UserUuid)
            || !string.IsNullOrWhiteSpace(BrandCode)
            || !string.IsNullOrWhiteSpace(ModelCode)
            || !string.IsNullOrWhiteSpace(GlobalCategoryCode)
            || !string.IsNullOrWhiteSpace(EdgeCode)
            || !string.IsNullOrWhiteSpace(PanelSizeCode)
            || !string.IsNullOrWhiteSpace(PerforationCode);

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

        public ProductionDrawingLabels Clone() =>
            new ProductionDrawingLabels
            {
                UserUuid = UserUuid,
                BrandCode = BrandCode,
                ModelCode = ModelCode,
                GlobalCategoryCode = GlobalCategoryCode,
                EdgeCode = EdgeCode,
                PanelSizeCode = PanelSizeCode,
                PerforationCode = PerforationCode
            };

        public static ProductionDrawingLabels? TryFromPayload(System.Text.Json.JsonElement payload)
        {
            if (payload.ValueKind != System.Text.Json.JsonValueKind.Object)
                return null;

            string Get(params string[] keys)
            {
                foreach (var key in keys)
                {
                    if (!payload.TryGetProperty(key, out var prop))
                        continue;
                    if (prop.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var s = prop.GetString();
                        if (!string.IsNullOrWhiteSpace(s))
                            return s.Trim();
                    }
                    else if (prop.ValueKind == System.Text.Json.JsonValueKind.Object
                             && prop.TryGetProperty("code", out var nested)
                             && nested.ValueKind == System.Text.Json.JsonValueKind.String)
                    {
                        var s = nested.GetString();
                        if (!string.IsNullOrWhiteSpace(s))
                            return s.Trim();
                    }
                }

                return string.Empty;
            }

            var labels = new ProductionDrawingLabels
            {
                UserUuid = Get("user_uuid"),
                BrandCode = Get("brand_code"),
                ModelCode = Get("model_code"),
                GlobalCategoryCode = Get("global_category_code", "global_cat_code"),
                EdgeCode = Get("prod_drawing_edge_code"),
                PanelSizeCode = Get("prod_drawing_panel_size_code"),
                PerforationCode = Get("prod_drawing_perforation_code")
            };

            return labels.HasAnyValue ? labels : null;
        }

        /// <summary>
        /// Fallback catalog title from label codes when ConstrTodo does not return payload.code.
        /// </summary>
        public string BuildAutoCode()
        {
            var parts = new[]
            {
                BrandCode,
                ModelCode,
                PanelSizeCode,
                PerforationCode,
                EdgeCode
            };
            var filled = new List<string>();
            foreach (var part in parts)
            {
                if (!string.IsNullOrWhiteSpace(part))
                    filled.Add(part.Trim());
            }

            return filled.Count == 0 ? string.Empty : string.Join("_", filled);
        }
    }

    /// <summary>POST /content/production-drawings/sizes — same as web PanelSizeCreate.</summary>
    public sealed class PanelSizeCreateRequest
    {
        [JsonPropertyName("len_x")]
        public double LenX { get; set; }

        [JsonPropertyName("len_z")]
        public double LenZ { get; set; }

        [JsonPropertyName("len_y")]
        public double LenY { get; set; }

        /// <summary>Backend code: len_x_len_z_len_y with '.' → '_' in len_y.</summary>
        public string BuildCode()
        {
            var y = LenY.ToString(System.Globalization.CultureInfo.InvariantCulture)
                .Replace('.', '_');
            return LenX.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "_"
                + LenZ.ToString(System.Globalization.CultureInfo.InvariantCulture)
                + "_"
                + y;
        }
    }

    /// <summary>POST perforations/edges — same as web BrandEntityCreate.</summary>
    public sealed class BrandEntityCreateRequest
    {
        [JsonPropertyName("code")]
        public string Code { get; set; } = string.Empty;

        [JsonPropertyName("name")]
        public string Name { get; set; } = string.Empty;

        [JsonPropertyName("description")]
        public string? Description { get; set; }

        [JsonPropertyName("brand_code")]
        public string BrandCode { get; set; } = string.Empty;
    }
}
