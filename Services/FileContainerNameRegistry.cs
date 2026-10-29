using System.Runtime.CompilerServices;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using WinThunar.Models;

namespace WinThunar.Services;

/// <summary>
/// Owns the accessible-name trackers for file-list containers, one per native container.
/// Called from XAML's container callback, so every update stays typed and allocation-light.
/// </summary>
internal sealed class FileContainerNameRegistry : IDisposable
{
    private readonly ConditionalWeakTable<SelectorItem, FileContainerNameTracker> _trackers = new();
    private readonly List<WeakReference<FileContainerNameTracker>> _created = [];

    /// <summary>Every tracker created so far, live or collected; the endurance harness bounds it.</summary>
    internal IReadOnlyList<WeakReference<FileContainerNameTracker>> Trackers => _created;

    public void Update(ContainerContentChangingEventArgs args)
    {
        var container = args.ItemContainer;
        // Native containers can outlive their managed RCW. Tag gives the tracker the
        // native lifetime; a weak-key-only table would register it again after each RCW GC.
        var tracker = container.Tag as FileContainerNameTracker ?? _trackers.GetValue(container, item =>
        {
            var created = new FileContainerNameTracker(item);
            _created.Add(new(created));
            if (_created.Count > 256 && _created.Count % 64 == 0)
                _created.RemoveAll(reference => !reference.TryGetTarget(out _));
            return created;
        });
        // Keep the entire update path typed. A reflection binding reconnects to every
        // recycled entry inside XAML layout, even if its initial installation was deferred.
        tracker.SetEntry(args.InRecycleQueue ? null : args.Item as FileSystemEntry);
        tracker.EnsureContentTracking();
    }

    public void Dispose()
    {
        foreach (var reference in _created)
            if (reference.TryGetTarget(out var tracker)) tracker.Dispose();
        _created.Clear();
        _trackers.Clear();
    }
}
