namespace Vdiff.Core;

/// <summary>
/// Every expected failure of the tool. The message names the problem and never carries
/// file content from the analysed repository.
/// </summary>
public class VdiffException : Exception
{
    public VdiffException(string message) : base(message)
    {
    }

    public VdiffException(string message, Exception inner) : base(message, inner)
    {
    }
}
