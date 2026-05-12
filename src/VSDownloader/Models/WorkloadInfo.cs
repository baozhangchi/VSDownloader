using Avalonia.InternalCheat;
using ReactiveUI;
using Russkyc.Messaging;

namespace VSDownloader.Models;

public partial class WorkloadInfo(string id, string title, BootstrapperInfo bootstrapperInfo) : ReactiveObject
{
    public List<ComponentInfo> Components { get; } = new();
    public string Id { get; } = id;
    public string Title { get; } = title;

    public bool IsRequired => Components.All(x => x.IsRequired);

    public bool IsEnabled => !IsRequired;

    [ObservableProperty] public partial bool IsSelected { get; set; } = false;
    public BootstrapperInfo BootstrapperInfo { get; } = bootstrapperInfo;

    partial void OnIsSelectedChanged()
    {
        WeakReferenceMessenger.Default.Send(this);
    }
}