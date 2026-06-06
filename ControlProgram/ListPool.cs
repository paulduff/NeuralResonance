using System.Collections.Concurrent;

internal static class ListPool<T>
{
    private const int MaxRetainedLists = 256;
    private const int MaxRetainedCapacity = 8192;
    private static readonly ConcurrentBag<List<T>> Pool = new();

    public static List<T> Rent(int capacity = 0)
    {
        if (Pool.TryTake(out var list))
        {
            if (capacity > 0 && list.Capacity < capacity)
            {
                list.Capacity = capacity;
            }

            return list;
        }

        return capacity > 0 ? new List<T>(capacity) : new List<T>();
    }

    public static void Return(List<T> list)
    {
        list.Clear();
        if (list.Capacity > MaxRetainedCapacity)
        {
            list.Capacity = MaxRetainedCapacity;
        }

        if (Pool.Count >= MaxRetainedLists)
        {
            return;
        }

        Pool.Add(list);
    }
}
