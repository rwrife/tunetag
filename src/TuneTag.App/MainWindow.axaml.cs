using System.Linq;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Platform.Storage;
using TuneTag.App.ViewModels;

namespace TuneTag.App;

public partial class MainWindow : Window
{
    private static readonly FilePickerFileType CoverImageFileType = new("Cover Images")
    {
        Patterns = ["*.jpg", "*.jpeg", "*.png", "*.webp", "*.gif", "*.bmp"],
        AppleUniformTypeIdentifiers = ["public.image"],
        MimeTypes = ["image/jpeg", "image/png", "image/webp", "image/gif", "image/bmp"]
    };

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
        await UpdateSelectionFromGridAsync();
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

    private void RefreshRenamePreviewClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.RefreshRenamePreview();
    }

    private async void ApplyRenameClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.ApplyRenameAsync();
    }

    private async void UndoRenameClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.UndoLastRenameAsync();
    }

    private void SuggestTagsFromFilenameClick(object? sender, RoutedEventArgs e)
    {
        ViewModel?.SuggestTagsFromFilenames();
    }

    private async void SetCoverClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var imagePath = await PickCoverImagePathAsync();
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return;
        }

        await ViewModel.SetCoverForSelectionAsync(imagePath);
    }

    private async void ExtractCoverClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var outputPath = await PickExtractOutputPathAsync();
        if (string.IsNullOrWhiteSpace(outputPath))
        {
            return;
        }

        await ViewModel.ExtractCoverFromSelectionAsync(outputPath);
    }

    private async void RemoveCoverClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        await ViewModel.RemoveCoverFromSelectionAsync();
    }

    private async void ApplyCoverToFolderClick(object? sender, RoutedEventArgs e)
    {
        if (ViewModel is null)
        {
            return;
        }

        var imagePath = await PickCoverImagePathAsync();
        if (string.IsNullOrWhiteSpace(imagePath))
        {
            return;
        }

        await ViewModel.ApplyCoverToSelectedFolderAsync(imagePath);
    }

    private void ExitClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private async void TracksGridSelectionChanged(object? sender, SelectionChangedEventArgs e)
    {
        await UpdateSelectionFromGridAsync();
    }

    private async Task UpdateSelectionFromGridAsync()
    {
        if (ViewModel is null || TracksGrid.SelectedItems is null)
        {
            return;
        }

        ViewModel.SetSelectedTracks(TracksGrid.SelectedItems.OfType<TrackRowViewModel>());
        await ViewModel.RefreshSelectedCoverAsync();
    }

    private async Task<string?> PickCoverImagePathAsync()
    {
        var files = await StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select cover image",
            AllowMultiple = false,
            FileTypeFilter = [CoverImageFileType]
        });

        return files.FirstOrDefault()?.Path.LocalPath;
    }

    private async Task<string?> PickExtractOutputPathAsync()
    {
        var file = await StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Extract cover art",
            SuggestedFileName = "cover"
        });

        return file?.Path.LocalPath;
    }
}
