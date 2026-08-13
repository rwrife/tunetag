using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Markup.Xaml;
using TuneTag.App.Services;
using TuneTag.App.ViewModels;
using TuneTag.Core;

namespace TuneTag.App;

public partial class App : Application
{
    public override void Initialize()
    {
        AvaloniaXamlLoader.Load(this);
    }

    public override void OnFrameworkInitializationCompleted()
    {
        if (ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
        {
            var tagService = TuneTagCore.CreateDefaultTagService();
            var trackLibraryService = new TrackLibraryService(tagService, tagService);
            desktop.MainWindow = new MainWindow
            {
                DataContext = new MainWindowViewModel(trackLibraryService, tagService)
            };
        }

        base.OnFrameworkInitializationCompleted();
    }
}
