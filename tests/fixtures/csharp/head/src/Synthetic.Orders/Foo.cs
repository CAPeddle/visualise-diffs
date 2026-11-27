namespace Synthetic.Orders;

public class Foo
{
    public void Reset() { }
    public void Reset(int to) { }
}

public class Foo<T>
{
    public int Count { get; set; }
    public T Value { get; set; }
}
