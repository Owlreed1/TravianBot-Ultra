namespace TbotUltra.Worker.Domain;

public sealed record ManualLoginConfirmationRequest(string? ValidationMessage = null);

public sealed class ManualLoginCanceledException : OperationCanceledException
{
    public ManualLoginCanceledException()
        : base("Manual login was canceled by the user.")
    {
    }
}
