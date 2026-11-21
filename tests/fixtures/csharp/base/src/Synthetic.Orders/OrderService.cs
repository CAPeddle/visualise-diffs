namespace Synthetic.Orders;

public class OrderService
{
    public bool Submit(Order order) { return order != null; }
    public void Cancel(int id) { }
    public int RetryCount { get; set; }
}
