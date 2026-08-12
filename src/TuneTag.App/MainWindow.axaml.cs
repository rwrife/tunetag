using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TuneTag.App.ViewModels;

namespace TuneTag.App;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
    }

    private MainWindowViewModel? ViewModel => DataContext as MainWindowViewModel;

    private async void OpenFolderClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var folders = await StorageProvider.OpenFolderPickerAsync(new FolderPickerOpenOptions
        {
            Title = "Open music folder",
            AllowMultiple = false
        });

        var folder = folders.FirstOrDefault();
        if (folder is null)
        {
            return;
        }

        await ViewModel.LoadFolderAsync(folder.Path.LocalPath);
        UpdateSelectionFromGrid();
    }

    private async void SaveClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.SaveAsync();
    }

    private void ApplyBatchEditClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.ApplyBatchEditToSelection();
    }

    private void ExitClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void TracksGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        UpdateSelectionFromGrid();
    }

    private void UpdateSelectionFromGrid()
    {
        if (ViewModel is null || TracksGrid.SelectedItems is null)
        {
            return;
        }

        ViewModel.SetSelectedTracks(TracksGrid.SelectedItems.OfType<TrackRowViewModel>());
    }
}
