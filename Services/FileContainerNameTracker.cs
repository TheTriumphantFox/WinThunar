using System.ComponentModel;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using WinThunar.Models;

namespace WinThunar.Services;

// No PropertyPath/ICustomPropertyProvider: reconnecting a reflection binding during
// recycling can wait on the runtime's CCW table and re-enter protected XAML layout.
internal sealed class FileContainerNameTracker(SelectorItem container) : IDisposable
{
    private readonly WeakReference<SelectorItem> _container = new(container);
    private FileSystemEntry? _entry;
    private bool _queued, _registering, _attached, _disposed;
    private long _contentToken;

    internal void SetEntry(FileSystemEntry? entry)
    {
        if (_disposed) return;
        if (!ReferenceEquals(_entry, entry))
        {
            if (_entry is not null) _entry.PropertyChanged -= EntryChanged;
            _entry = entry;
            if (_entry is not null) _entry.PropertyChanged += EntryChanged;
        }
        UpdateName();
    }

    internal void EnsureContentTracking()
    {
        if (_disposed || _attached || _queued || _registering || !_container.TryGetTarget(out var container)) return;
        _queued = true;
        // Register the single native callback outside XAML's protected recycling callback.
        // Its subsequent updates only use typed Content and a string property assignment.
        if (!container.DispatcherQueue.TryEnqueue(() =>
        {
            _queued = false;
            if (_disposed || !_container.TryGetTarget(out var current)) return;
            if (current.Tag is FileContainerNameTracker existing && !ReferenceEquals(existing, this))
            {
                existing.SetEntry(current.Content as FileSystemEntry);
                Dispose();
                return;
            }
            // Store the managed tracker on the native container, once, outside layout.
            // The tracker holds only a weak reference back to the container.
            current.Tag = this;
            _registering = true;
            try
            {
                _contentToken = current.RegisterPropertyChangedCallback(ContentControl.ContentProperty, ContentChanged);
                _attached = true;
            }
            finally { _registering = false; }
            if (_disposed) current.UnregisterPropertyChangedCallback(ContentControl.ContentProperty, _contentToken);
            else SetEntry(current.Content as FileSystemEntry);
        })) _queued = false;
    }

    private void ContentChanged(DependencyObject sender, DependencyProperty property) =>
        SetEntry(((ContentControl)sender).Content as FileSystemEntry);

    private void EntryChanged(object? sender, PropertyChangedEventArgs args)
    {
        if (_disposed || (args.PropertyName is not null and not "" && args.PropertyName != nameof(FileSystemEntry.AutomationName))) return;
        if (!_container.TryGetTarget(out var container))
        {
            Dispose();
            return;
        }
        if (container.DispatcherQueue.HasThreadAccess) UpdateName();
        else container.DispatcherQueue.TryEnqueue(UpdateName);
    }

    private void UpdateName()
    {
        if (!_disposed && _container.TryGetTarget(out var container))
            AutomationProperties.SetName(container, _entry?.AutomationName ?? string.Empty);
    }

    public void Dispose()
    {
        if (_disposed) return;
        _disposed = true;
        if (_entry is not null) _entry.PropertyChanged -= EntryChanged;
        _entry = null;
        if (_attached && _container.TryGetTarget(out var container))
            container.UnregisterPropertyChangedCallback(ContentControl.ContentProperty, _contentToken);
    }
}
