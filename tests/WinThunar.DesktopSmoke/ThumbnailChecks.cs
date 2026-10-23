using WinThunar.Models;

namespace WinThunar;

public sealed partial class MainPage
{
    private async Task CheckThumbnailReuseAsync(string directory, List<string> checks)
    {
        var fixture = Directory.CreateDirectory(Path.Combine(directory, "thumbnail-files")).FullName;
        var path = Path.Combine(fixture, "thumbnail.txt");
        File.WriteAllText(path, "Thumbnail regression fixture");
        ViewModel.ViewMode = BrowserViewMode.Details;
        ViewModel.ShowThumbnails = true;
        await ViewModel.NavigateAsync(fixture);
        await ViewModel.ImageLoadingCompletion.WaitAsync(TimeSpan.FromSeconds(15));
        var entry = ViewModel.Entries.Single();
        var initial = entry.Thumbnail;
        Require(initial is not null, "Shell image regression loads a real file icon", checks);

        ViewModel.ViewMode = BrowserViewMode.Compact;
        await ViewModel.ImageLoadingCompletion.WaitAsync(TimeSpan.FromSeconds(15));
        ViewModel.ZoomLevel = ViewModel.ZoomLevel == 4 ? 3 : 4;
        await ViewModel.ImageLoadingCompletion.WaitAsync(TimeSpan.FromSeconds(15));
        Require(ReferenceEquals(initial, entry.Thumbnail),
            "Compact layout and text zoom reuse the unchanged 32-pixel shell image", checks);

        ViewModel.ViewMode = BrowserViewMode.Icons;
        await ViewModel.ImageLoadingCompletion.WaitAsync(TimeSpan.FromSeconds(15));
        var large = entry.Thumbnail;
        Require(large is not null && !ReferenceEquals(initial, large),
            "Icon layout requests a new image at its larger decode size", checks);
        ViewModel.ShowThumbnails = false;
        await ViewModel.ImageLoadingCompletion.WaitAsync(TimeSpan.FromSeconds(15));
        Require(entry.Thumbnail is not null && !ReferenceEquals(large, entry.Thumbnail),
            "Disabling content thumbnails replaces the content-mode shell image", checks);

        // Supersede native requests without waiting for them, then allow the old calls to drain.
        ViewModel.ShowThumbnails = true;
        ViewModel.ZoomLevel = 1;
        ViewModel.ViewMode = BrowserViewMode.Details;
        await ViewModel.ImageLoadingCompletion.WaitAsync(TimeSpan.FromSeconds(15));
        await Task.Delay(250);
        Require(entry.Thumbnail is not null && entry.ThumbnailSize == 32 && !entry.ThumbnailShowsContent,
            "Superseded icon requests cannot overwrite the current detailed-view image", checks);

        File.WriteAllText(path, "Modified thumbnail regression fixture with a different length");
        await ViewModel.NavigateAsync(fixture);
        await ViewModel.ImageLoadingCompletion.WaitAsync(TimeSpan.FromSeconds(15));
        Require(!ReferenceEquals(entry, ViewModel.Entries.Single()) && ViewModel.Entries[0].Thumbnail is not null,
            "Changed file metadata replaces the entry and loads a fresh image", checks);
        ViewModel.ShowThumbnails = true;
        ViewModel.ViewMode = BrowserViewMode.Details;
        await ViewModel.ImageLoadingCompletion.WaitAsync(TimeSpan.FromSeconds(15));
    }
}
