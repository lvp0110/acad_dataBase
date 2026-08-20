using System.Drawing;

namespace AcadDwgBrowser.Plugin.Ui
{
    /// <summary>ConstrTodo content status labels and catalog cell colors.</summary>
    internal static class ContentStatusUi
    {
        public static string Normalize(string? status)
        {
            var s = (status ?? string.Empty).Trim().ToLowerInvariant();
            if (s == "draft" || s == "черновик")
                return "draft";
            if (s == "pending" || s == "на подписании" || s == "на подписи")
                return "pending";
            if (s == "in_review" || s == "in-review" || s == "inreview" || s == "на согласовании")
                return "in_review";
            if (s == "rejected" || s == "отклонен" || s == "отклонён"
                || s == "на доработку" || s == "на доработке")
                return "rejected";
            if (s == "approved" || s == "подписан" || s == "утвержден" || s == "утверждён")
                return "approved";
            if (s == "archived" || s == "архив" || s == "в архиве")
                return "archived";
            if (s == "archiving" || s == "архивируется")
                return "archiving";
            return s;
        }

        public static string ToDisplayName(string? status)
        {
            switch (Normalize(status))
            {
                case "draft":
                    return "Черновик";
                case "pending":
                case "in_review":
                    return "На подписании";
                case "rejected":
                    return "На доработку";
                case "approved":
                    return "Подписан";
                case "archived":
                    return "В архиве";
                case "archiving":
                    return "Архивируется";
                default:
                    return string.IsNullOrWhiteSpace(status) ? "—" : status!.Trim();
            }
        }

        public static bool TryGetFill(string? status, out Color back, out Color fore)
        {
            switch (Normalize(status))
            {
                case "draft":
                    back = Color.FromArgb(0xB0, 0xB8, 0xBE);
                    fore = Color.FromArgb(0x1E, 0x2A, 0x32);
                    return true;
                case "pending":
                case "in_review":
                    back = Color.FromArgb(0xF5, 0xD0, 0x3A);
                    fore = Color.FromArgb(0x1E, 0x2A, 0x32);
                    return true;
                case "rejected":
                    back = Color.FromArgb(0xE0, 0x53, 0x53);
                    fore = Color.White;
                    return true;
                case "approved":
                    back = Color.FromArgb(0x43, 0xA0, 0x47);
                    fore = Color.White;
                    return true;
                default:
                    back = Color.Empty;
                    fore = Color.Empty;
                    return false;
            }
        }
    }
}
