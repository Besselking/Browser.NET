using System.Collections;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Browser;

[CollectionBuilder(typeof(DisplayListBuilder), nameof(DisplayListBuilder.Create))]
internal sealed class DisplayList : ICollection<DisplayItem>
{
    private readonly List<DisplayItem> _displayItems = [];
    internal float MaxY {get; private set; }

    public int Count => _displayItems.Count;

    public bool IsReadOnly => false;

    internal DisplayList(ReadOnlySpan<DisplayItem> items)
    {
        _displayItems.AddRange(items);
    }

    public void Add(DisplayItem item)
    {
        _displayItems.Add(item);
        float y = item.Pos.Y;
        MaxY = Math.Max(y, MaxY);
    }

    public IEnumerator<DisplayItem> GetEnumerator() => _displayItems.GetEnumerator();
    IEnumerator IEnumerable.GetEnumerator() => _displayItems.GetEnumerator();

    public void Clear() => _displayItems.Clear();

    public bool Contains(DisplayItem item) => _displayItems.Contains(item);

    public void CopyTo(DisplayItem[] array, int arrayIndex) => _displayItems.CopyTo(array, arrayIndex);

    public bool Remove(DisplayItem item)
    {
        bool removed = _displayItems.Remove(item);

        if (removed && item.Pos.Y >= MaxY)
        {
            MaxY = _displayItems.Max(i => i.Pos.Y);
        }

        return removed;
    }

    internal ReadOnlySpan<DisplayItem> AsSpan()
    {
        return CollectionsMarshal.AsSpan(_displayItems);
    }

}

internal static class DisplayListBuilder
{
    internal static DisplayList Create(ReadOnlySpan<DisplayItem> items) => new(items);
}