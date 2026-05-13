using AvaloniaSourceGenerators;
using ReactiveUI;
using Russkyc.Messaging;

namespace VSDownloader.Models;

public partial class ComponentInfo(string id, string title, bool isRequired, bool isSuggester, WorkloadInfo owner)
    : ReactiveObject
{
    public string Id { get; init; } = id;
    public string Title { get; init; } = title;
    public bool IsRequired { get; init; } = isRequired;

    public bool IsEnabled { get; set; } = true;
    public bool IsSuggester { get; set; } = isSuggester;
    public WorkloadInfo Owner { get; } = owner;

    [RaiseAndSetIfChanged] public partial bool IsSelected { get; set; }

    partial void OnIsSelectedChanged()
    {
        WeakReferenceMessenger.Default.Send(this);
    }
}