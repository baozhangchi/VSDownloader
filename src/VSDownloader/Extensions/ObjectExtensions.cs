using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;

namespace VSDownloader.Extensions;

internal static class ObjectExtensions
{
    extension(object obj)
    {
        public TopLevel GetTopLevel()
        {
            return TopLevel.GetTopLevel(
                ((IClassicDesktopStyleApplicationLifetime)Application.Current!.ApplicationLifetime!).MainWindow)!;
        }
    }
}