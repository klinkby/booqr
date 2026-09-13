using Klinkby.Booqr.Core;

namespace Klinkby.Booqr.Infrastructure.Services;

/// <inheritdoc cref="IBatchScope" />
internal sealed class BatchScope : IBatchScope
{
    public bool IsEnabled { get; private set; }

    public void Enable() => IsEnabled = true;
}
