using ImmersiveTB.Core.Contracts.Services;
using Microsoft.UI.Dispatching;

namespace ImmersiveTB.Services;

public class DispatcherService : IDispatcherService
{
    private readonly DispatcherQueue _dispatcherQueue = DispatcherQueue.GetForCurrentThread();

    public bool TryEnqueue(Action action) => _dispatcherQueue.TryEnqueue(() => action());
}