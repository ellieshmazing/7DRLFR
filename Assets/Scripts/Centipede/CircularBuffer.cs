/// <summary>
/// Fixed-capacity ring buffer. <c>Push</c> overwrites the oldest entry when full.
/// Index 0 = oldest entry; index <c>Count - 1</c> = most recently pushed.
/// </summary>
public sealed class CircularBuffer<T>
{
    private readonly T[] _buf;
    private int          _head; // next write index

    public int Count    { get; private set; }
    public int Capacity => _buf.Length;

    public CircularBuffer(int capacity)
    {
        _buf  = new T[capacity];
        _head = 0;
        Count = 0;
    }

    public void Push(T item)
    {
        _buf[_head] = item;
        _head       = (_head + 1) % _buf.Length;
        if (Count < _buf.Length) Count++;
    }

    /// <summary>Returns the item at logical index <paramref name="index"/> (0 = oldest).</summary>
    public T this[int index]
    {
        get
        {
            int i = (_head - Count + index + _buf.Length) % _buf.Length;
            return _buf[i];
        }
    }
}
