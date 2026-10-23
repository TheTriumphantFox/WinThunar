using Microsoft.UI.Xaml;
using WinThunar.Models;
using WinThunar.Services;
using WinThunar.Testing;

namespace WinThunar;

public sealed partial class MainPage
{
    private async Task CheckDocxAsync(string directory, List<string> checks)
    {
        var fixture = Directory.CreateDirectory(Path.Combine(directory, "docx-files")).FullName;
        var path = Path.Combine(fixture, "report.docx");
        DocxFixture.Write(path, "<w:p><w:r><w:t>Annual </w:t></w:r><w:r><w:t>report approved by José &amp; team.</w:t></w:r></w:p>" +
            "<w:p><w:r><w:t>Read-only body text from the current document.</w:t></w:r></w:p>");
        var large = Path.Combine(fixture, "long.docx");
        DocxFixture.Write(large, "<w:p><w:r><w:t>" + new string('x', TextFileContent.MaximumPreviewCharacters + 100) + "</w:t></w:r></w:p>");
        var bad = Path.Combine(fixture, "broken.docx");
        File.WriteAllText(bad, "Generated malformed DOCX fixture");
        await ViewModel.NavigateAsync(fixture);
        await ViewModel.SearchAsync("type:docx content:\"annual report\" content:José");
        Require(ViewModel.Entries.Count == 1 && ViewModel.Entries[0].FullPath == path &&
            !ViewModel.StatusText.StartsWith("Windows index:"), "DOCX body search finds current text across runs without Windows indexing", checks);
        var entry = ViewModel.Entries[0];
        await UpdateImagePreviewAsync(entry);
        Require(PreviewText.Visibility == Visibility.Visible && PreviewText.IsReadOnly &&
            PreviewText.Text.Contains("Annual report approved by José & team.") && PreviewStatus.Text == "DOCX body text" &&
            Microsoft.UI.Xaml.Automation.AutomationProperties.GetName(PreviewText) == "Selected DOCX body text preview" &&
            PreviewImage.Visibility == Visibility.Collapsed && PdfPageControls.Visibility == Visibility.Collapsed,
            "DOCX preview exposes read-only Unicode body text and clears image/page controls", checks);
        UpdateLayout();
        await Task.Delay(100);
        await CaptureSmokeImageAsync(directory, "DocxPreview");
        using (var exclusive = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            Require(exclusive.Length > 0, "DOCX preview releases the source handle after extraction", checks);

        File.Delete(path);
        DocxFixture.Write(path, "<w:p><w:r><w:t>Replacement body from edited document</w:t></w:r></w:p>");
        await ViewModel.SearchAsync("type:docx content:\"replacement body\"");
        Require(ViewModel.Entries.Count == 1 && ViewModel.Entries[0].FullPath == path,
            "DOCX search reads newly edited document bytes", checks);
        await UpdateImagePreviewAsync(new FileSystemEntry("long.docx", large, false, "", "DOCX document", "", ""));
        Require(PreviewText.Text.Length == TextFileContent.MaximumPreviewCharacters && PreviewStatus.Text.Contains("32,768"),
            "DOCX preview truncates at the visible character limit", checks);
        await UpdateImagePreviewAsync(new FileSystemEntry("broken.docx", bad, false, "", "DOCX document", "", ""));
        Require(PreviewText.Visibility == Visibility.Collapsed && PreviewText.Text.Length == 0 &&
            PreviewStatus.Text.StartsWith("Preview unavailable:"), "Malformed DOCX reports an error and clears the previous text", checks);
        var pending = UpdateImagePreviewAsync(entry);
        await UpdateImagePreviewAsync(null);
        await pending;
        Require(PreviewText.Visibility == Visibility.Collapsed && PreviewText.Text.Length == 0 && PreviewTitle.Text == "No item selected",
            "A newer selection cancels pending DOCX preview without stale text", checks);
        await ViewModel.EndSearchAsync();
    }
}
