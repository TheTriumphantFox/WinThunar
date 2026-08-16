using System.Collections.ObjectModel;

namespace WinThunar.Services;

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
