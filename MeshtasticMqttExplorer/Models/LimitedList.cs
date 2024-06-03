namespace Monitor.Models;

public class LimitedList<T> : List<T>
{
    private readonly int _maxLength;

    public LimitedList(int maxLength)
    {
        if (maxLength <= 0)
        {
            throw new ArgumentException("Maximum length must be greater than zero.");
        }

        _maxLength = maxLength;
    }

    public new void Add(T item)
    {
        if (Count >= _maxLength)
        {
            RemoveAt(0);
        }

        base.Add(item);
    }
}