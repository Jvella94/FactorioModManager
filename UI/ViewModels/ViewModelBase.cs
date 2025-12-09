using ReactiveUI;
using System;

namespace FactorioModManager.ViewModels;

public abstract class ViewModelBase : ReactiveObject, IDisposable
{
    private bool _disposed;

    protected virtual void Dispose(bool disposing)
    {
        if (!_disposed)
        {
            if (disposing)
            {
                // Derived classes override this to dispose their resources
            }
            _disposed = true;
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}