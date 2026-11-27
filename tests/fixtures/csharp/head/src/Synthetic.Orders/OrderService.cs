namespace Synthetic.Orders;

public class OrderService
{
    public bool Submit(Order order) { return order == null ? false : true; }
    public void Refund(int id) { }
    protected int RetryCount { get; set; }

    public class Receipt
    {
        public int Number { get; set; }
    }
}
