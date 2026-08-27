using TbotUltra.Worker.Domain;

namespace TbotUltra.Worker.Services;

public sealed partial class TravianClient
{
    private async Task PublishConstructionQueueObservationAsync(
        CancellationToken cancellationToken,
        IReadOnlyList<ActiveConstruction>? knownActiveConstructions = null)
    {
        if (_constructionQueueObserved is null)
        {
            return;
        }

        try
        {
            var activeConstructions = knownActiveConstructions;
            if (activeConstructions is null)
            {
                InvalidateActiveConstructionsCache();
                activeConstructions = await ReadActiveConstructionsAsync(
                    cancellationToken,
                    allowNavigationToBuildings: false,
                    readMode: ActiveConstructionReadMode.FreshForMutation);
            }

            if (!_lastActiveConstructionsFromOverview || activeConstructions.Count == 0)
            {
                Notify(
                    "[construction-ui:verbose] immediate queue observation skipped because " +
                    $"the current overview was not authoritative or empty (count={activeConstructions.Count}).");
                return;
            }

            var villageName = await ReadActiveVillageNameAsync(cancellationToken);
            var coordinates = await TryReadActiveVillageCoordsFromCurrentPageAsync(cancellationToken);
            _constructionQueueObserved(new ConstructionQueueObservation(
                AccountName,
                villageName,
                coordinates.X,
                coordinates.Y,
                activeConstructions));
            Notify(
                $"[construction-ui] published {activeConstructions.Count} active construction(s) " +
                $"for village='{villageName}' immediately after Travian queue confirmation.");
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception ex)
        {
            // UI responsiveness is best-effort. The normal post-task village refresh remains authoritative.
            Notify($"[construction-ui] immediate queue observation failed: {ex.Message}");
        }
    }
}
