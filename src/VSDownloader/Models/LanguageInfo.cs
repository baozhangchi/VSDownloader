using AvaloniaSourceGenerators;
using ReactiveUI;

namespace VSDownloader.Models;

public partial class LanguageInfo(string id, string title) : ReactiveObject
{
    public string Id { get; } = id;
    public string Title { get; } = title;

    [RaiseAndSetIfChanged] public partial bool IsSelected { get; set; } = false;
}