namespace TbotUltra.Worker.Services;

public sealed record HeroCropAntiStarveDecision(
    bool IsRequired,
    int TransferAmount,
    int? SecondsToEmpty,
    bool IsPartial,
    string Reason);

public static class HeroCropAntiStarveCalculator
{
    public static HeroCropAntiStarveDecision Calculate(
        long currentCrop,
        long granaryCapacity,
        double productionPerHour,
        int heroCrop,
        int triggerMinutes,
        int targetMinutes,
        int maxCropPerTransfer,
        int minHeroCropRemaining)
    {
        if (productionPerHour >= 0)
        {
            return new(false, 0, null, false, "Crop production is not negative.");
        }

        var consumptionPerHour = -productionPerHour;
        var secondsToEmptyValue = Math.Ceiling(Math.Max(0, currentCrop) / consumptionPerHour * 3600d);
        var secondsToEmpty = secondsToEmptyValue >= int.MaxValue ? int.MaxValue : (int)secondsToEmptyValue;
        if (secondsToEmpty >= Math.Max(1, triggerMinutes) * 60)
        {
            return new(false, 0, secondsToEmpty, false, "Crop is above the anti-starve trigger.");
        }

        var targetCrop = (long)Math.Ceiling(consumptionPerHour * Math.Max(1, targetMinutes) / 60d);
        var needed = Math.Max(0, targetCrop - Math.Max(0, currentCrop));
        var inventoryAvailable = Math.Max(0L, (long)heroCrop - Math.Max(0, minHeroCropRemaining));
        var granaryFree = Math.Max(0L, granaryCapacity - Math.Max(0, currentCrop));
        var allowed = Math.Min(
            needed,
            Math.Min(Math.Max(0, maxCropPerTransfer), Math.Min(inventoryAvailable, granaryFree)));
        var amount = allowed >= int.MaxValue ? int.MaxValue : (int)allowed;
        var reason = amount <= 0
            ? inventoryAvailable <= 0
                ? "Hero crop reserve prevents a transfer."
                : granaryFree <= 0
                    ? "The granary has no free capacity."
                    : "No crop transfer is required."
            : amount < needed
                ? "A partial safe transfer is available."
                : "The target crop duration can be restored.";
        return new(true, amount, secondsToEmpty, amount < needed, reason);
    }

    public static int ResolvePassiveCheckSeconds(int triggerMinutes)
        => Math.Clamp((Math.Max(1, triggerMinutes) * 60) / 3, 15, 10 * 60);

    public static int ResolvePostTransferCheckSeconds(int? secondsToEmpty)
    {
        if (secondsToEmpty is >= 0 and < 10 * 60)
        {
            return Math.Max(30, secondsToEmpty.Value / 2);
        }

        return 5 * 60;
    }
}
