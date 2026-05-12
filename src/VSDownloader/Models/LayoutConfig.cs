namespace VSDownloader.Models;

public class LayoutConfig
{
    public string? Version { get; set; }
    public List<string> Components { get; set; } = new();
    public List<string> Extensions { get; set; } = new();
    public List<string> Languages { get; set; } = new();
}