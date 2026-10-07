using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;

namespace WinThunar.Services;

public sealed class BulkObservableCollection<T> : ObservableCollection<T>
{
    public void ReplaceAll(IEnumerable<T> items)
    {
        ArgumentNullException.ThrowIfNull(items);
        CheckReentrancy();
        Items.Clear();
        foreach (var item in items)
        {
            Items.Add(item);
        }

        OnPropertyChanged(new PropertyChangedEventArgs(nameof(Count)));
        OnPropertyChanged(new PropertyChangedEventArgs("Item[]"));
        OnCollectionChanged(new NotifyCollectionChangedEventArgs(NotifyCollectionChangedAction.Reset));
    }
}

public static class ObservableCollectionReconciler
{
    public static bool Reconcile<T>(
        ObservableCollection<T> collection,
        IReadOnlyList<T> targetItems,
        Func<T, T, bool> sameIdentity)
    {
        ArgumentNullException.ThrowIfNull(collection);
        ArgumentNullException.ThrowIfNull(targetItems);
        ArgumentNullException.ThrowIfNull(sameIdentity);

        if (collection.Count == 0)
        {
            if (collection is BulkObservableCollection<T> bulkCollection)
            {
                bulkCollection.ReplaceAll(targetItems);
                return targetItems.Count > 0;
            }

            foreach (var targetItem in targetItems)
            {
                collection.Add(targetItem);
            }

            return targetItems.Count > 0;
        }

        if (collection.Count == targetItems.Count)
        {
            var sameOrder = true;
            for (var index = 0; index < targetItems.Count; index++)
            {
                if (!sameIdentity(collection[index], targetItems[index]))
                {
                    sameOrder = false;
                    break;
                }
            }

            if (sameOrder)
            {
                var replaced = false;
                for (var index = 0; index < targetItems.Count; index++)
                {
                    if (!ReferenceEquals(collection[index], targetItems[index]))
                    {
                        collection[index] = targetItems[index];
                        replaced = true;
                    }
                }

                return replaced;
            }
        }

        // Moving or inserting thousands of ObservableCollection items performs quadratic work.
        // A single reset followed by linear population is much cheaper for a substantially changed view.
        if (Math.Max(collection.Count, targetItems.Count) >= 512)
        {
            if (collection is BulkObservableCollection<T> bulkCollection)
            {
                bulkCollection.ReplaceAll(targetItems);
            }
            else
            {
                collection.Clear();
                foreach (var targetItem in targetItems)
                {
                    collection.Add(targetItem);
                }
            }

            return true;
        }

        var changed = false;
        for (var targetIndex = 0; targetIndex < targetItems.Count; targetIndex++)
        {
            var target = targetItems[targetIndex];
            if (targetIndex < collection.Count && ReferenceEquals(collection[targetIndex], target))
            {
                continue;
            }

            var existingIndex = collection.IndexOf(target);
            if (existingIndex >= 0)
            {
                collection.Move(existingIndex, targetIndex);
            }
            else if (targetIndex < collection.Count && sameIdentity(collection[targetIndex], target))
            {
                collection[targetIndex] = target;
            }
            else
            {
                collection.Insert(targetIndex, target);
            }

            changed = true;
        }

        while (collection.Count > targetItems.Count)
        {
            collection.RemoveAt(collection.Count - 1);
            changed = true;
        }

        return changed;
    }
}
