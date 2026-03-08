namespace SONA.Models
{
    public class DependencyItem
    {
        public string DisplayName { get; set; } = "";
        public string PackageId { get; set; } = "";
        public bool IsInstalled { get; set; }
        public bool IsSelected { get; set; }
    }
}
