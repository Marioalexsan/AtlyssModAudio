using System.Buffers;
using System.Runtime.CompilerServices;

namespace Marioalexsan.ModAudio;

internal static class ForeachCache<T>
{
    public const int InitialCapacity = 1024;

    private static T[] _cache = new T[InitialCapacity];
    public static int CacheSize { get; private set; } = 0;

    // Note: This method sucks
    // Make sure you're done with the previous span before trying to call this method again,
    // otherwise you'll be overwriting the previous span's data!
    // This includes accidental recursions!
    public static Span<T> CacheFrom(ICollection<T> collection)
    {
        int inputElements = collection.Count;

        if (_cache.Length < inputElements)
        {
            // Throw out previous array
            _cache = new T[collection.Count * 2];

            collection.CopyTo(_cache, 0);
            CacheSize = inputElements;
        }
        else
        {
            collection.CopyTo(_cache, 0);

            // Clear out previous values that didn't get replaced
            for (int i = inputElements; i < CacheSize; i++)
                _cache[i] = default!;

            CacheSize = inputElements;
        }

        return new Span<T>(_cache, 0, CacheSize);
    }
}

// TODO: Investigate Burst with hand-written intrinsics
internal static class Utils
{
    // Note: Burst's automated vectorization couldn't optimize this method any better than JIT
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void MultiplyFloatArray(float[] data, float factor)
    {
        unsafe
        {
            fixed (float* start = data)
            {
                float* current = start;
                float* end = start + data.Length;

                while (current != end)
                {
                    *current = *current * factor;
                    current++;
                }
            }
        }
    }

    public delegate void CachedForeachAction<T>(in T value);

    public static void CachedForeach<T>(ICollection<T> enumerable, CachedForeachAction<T> action)
    {
        var cacheSize = enumerable.Count;
        var cache = ArrayPool<T>.Shared.Rent(cacheSize);

        enumerable.CopyTo(cache, 0);

        for (int i = 0; i < cacheSize; i++)
        {
            action(cache[i]);
        }

        ArrayPool<T>.Shared.Return(cache);
    }

    public delegate void CachedForeachAction<T, V>(in T value, in V context);

    public static void CachedForeach<T, V>(ICollection<T> enumerable, in V context, CachedForeachAction<T, V> action)
    {
        var cacheSize = enumerable.Count;
        var cache = ArrayPool<T>.Shared.Rent(cacheSize);

        enumerable.CopyTo(cache, 0);

        for (int i = 0; i < cacheSize; i++)
        {
            action(cache[i], in context);
        }

        ArrayPool<T>.Shared.Return(cache);
    }

    public static (AudioPack Pack, Route Route) SelectRandomWeighted(Random rng, List<(AudioPack Pack, Route Route)> routes)
    {
        var totalWeight = 0.0;

        for (int i = 0; i < routes.Count; i++)
            totalWeight += routes[i].Route.ReplacementWeight;

        var selectedIndex = -1;

        var randomValue = rng.NextDouble() * totalWeight;

        do
        {
            selectedIndex++;
            randomValue -= routes[selectedIndex].Route.ReplacementWeight;
        }
        while (randomValue >= 0.0);

        return routes[selectedIndex];
    }

    public static ClipSelection SelectRandomWeighted(Random rng, List<ClipSelection> selections)
    {
        var totalWeight = 0.0;

        for (int i = 0; i < selections.Count; i++)
            totalWeight += selections[i].Weight;

        var selectedIndex = -1;

        var randomValue = rng.NextDouble() * totalWeight;

        do
        {
            selectedIndex++;
            randomValue -= selections[selectedIndex].Weight;
        }
        while (randomValue >= 0.0);

        return selections[selectedIndex];
    }
}
