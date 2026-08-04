using System.Collections.Generic;

namespace AcadDwgBrowser.Core.Models
{
    public sealed class DwgListResponse
    {
        public List<DwgFileInfo> Files { get; set; } = new List<DwgFileInfo>();
    }
}
