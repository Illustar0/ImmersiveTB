namespace ImmersiveTB.Core.Contracts.Services;

public interface IDispatcherService
{
    bool TryEnqueue(Action action);
}