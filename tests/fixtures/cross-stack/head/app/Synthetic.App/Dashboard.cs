namespace Synthetic.App;

public class Dashboard
{
    public int Refresh() { return new Synthetic.Interop.EngineWrapper().Speed(); }
}
