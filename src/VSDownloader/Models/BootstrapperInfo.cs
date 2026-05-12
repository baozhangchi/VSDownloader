using Avalonia.InternalCheat;
using ReactiveUI;

namespace VSDownloader.Models;

public partial class BootstrapperInfo(string title, string downloaderUrl) : ReactiveObject
{
    public string Title { get; } = title;
    public string DownloaderUrl { get; } = downloaderUrl;

    [ObservableProperty] public partial List<WorkloadInfo> Workloads { get; set; } = new();

    public List<ComponentInfo> Components => Workloads.SelectMany(x => x.Components).ToList();
}