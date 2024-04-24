namespace CUClock.Windows.Contracts.Services;

public interface IActivationService
{
    Task ActivateAsync(object activationArgs);
}
