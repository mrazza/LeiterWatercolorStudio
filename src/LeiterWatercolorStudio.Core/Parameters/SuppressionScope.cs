namespace LeiterWatercolorStudio.Core.Parameters;

internal sealed class SuppressionScope(StageViewModelBase owner) : IDisposable
{
    private bool _disposed;

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        owner.ReleaseSuppression();
    }
}
