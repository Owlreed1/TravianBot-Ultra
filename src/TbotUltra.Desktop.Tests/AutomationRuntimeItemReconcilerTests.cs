using TbotUltra.Core.Configuration;
using TbotUltra.Desktop.Services.Orchestration;
using TbotUltra.Worker.Domain;
using Xunit;

namespace TbotUltra.Desktop.Tests;

public sealed class AutomationRuntimeItemReconcilerTests
{
    [Fact]
    public void Ensure_AddsOnlyOneRuntimeItemForTheSameVillage()
    {
        var queue = new RecordingQueuePort();
        var reconciler = new AutomationRuntimeItemReconciler([], key => key, queue);
        var spec = Spec("0|0");

        var first = reconciler.Ensure(spec);
        var second = reconciler.Ensure(spec);

        Assert.Equal(AutomationRuntimeItemChange.Added, first.Change);
        Assert.Equal(AutomationRuntimeItemChange.None, second.Change);
        Assert.Single(queue.Items);
    }

    [Fact]
    public void Ensure_RefreshesPayloadWithoutChangingTheAuthoritativeDeadline()
    {
        var deadline = new DateTimeOffset(2026, 8, 14, 13, 0, 0, TimeSpan.Zero);
        var existing = CreateQueueItem("0|0", new Dictionary<string, string>
        {
            [BotOptionPayloadKeys.TargetVillageKey] = "0|0",
            ["setting"] = "old",
        });
        existing.NextAttemptAt = deadline;
        var queue = new RecordingQueuePort(existing);
        var reconciler = new AutomationRuntimeItemReconciler([existing], key => key, queue);
        var spec = Spec("0|0") with
        {
            Payload = new Dictionary<string, string>
            {
                [BotOptionPayloadKeys.TargetVillageKey] = "0|0",
                ["setting"] = "new",
            },
            RefreshPendingPayload = true,
        };

        var result = reconciler.Ensure(spec);

        Assert.Equal(AutomationRuntimeItemChange.PayloadUpdated, result.Change);
        Assert.Equal(deadline, queue.Items.Single().NextAttemptAt);
    }

    [Fact]
    public void Ensure_UpdatesAntiStarvePriorityForAnExistingPendingItem()
    {
        var existing = CreateQueueItem("1|1", new Dictionary<string, string>
        {
            [BotOptionPayloadKeys.TargetVillageKey] = "1|1",
        });
        var queue = new RecordingQueuePort(existing);
        var reconciler = new AutomationRuntimeItemReconciler([existing], key => key, queue);

        var result = reconciler.Ensure(Spec("1|1") with
        {
            Priority = 500,
            RefreshPendingPriority = true,
        });

        Assert.Equal(AutomationRuntimeItemChange.PriorityUpdated, result.Change);
        Assert.Equal(500, queue.Items.Single().Priority);
    }

    private static AutomationRuntimeItemSpec Spec(string villageKey) => new(
        "build_troops",
        "Build troops",
        new Dictionary<string, string> { [BotOptionPayloadKeys.TargetVillageKey] = villageKey },
        -50,
        0,
        villageKey);

    private static QueueItem CreateQueueItem(string villageKey, Dictionary<string, string> payload) => new()
    {
        Id = Guid.NewGuid(),
        TaskName = "build_troops",
        DisplayName = "Build troops",
        Payload = payload,
        Priority = -50,
        MaxRetries = 0,
        IsRuntimeOnly = true,
        Status = QueueStatus.Pending,
        CreatedAt = DateTimeOffset.UtcNow,
        NextAttemptAt = DateTimeOffset.UtcNow,
    };

    private sealed class RecordingQueuePort(params QueueItem[] initial) : IAutomationRuntimeQueuePort
    {
        internal List<QueueItem> Items { get; } = [.. initial];

        public QueueItem Enqueue(AutomationRuntimeItemSpec spec)
        {
            var item = new QueueItem
            {
                Id = Guid.NewGuid(),
                TaskName = spec.TaskName,
                DisplayName = spec.DisplayName,
                Payload = spec.Payload ?? [],
                Priority = spec.Priority,
                MaxRetries = spec.MaxRetries,
                IsRuntimeOnly = true,
                Status = QueueStatus.Pending,
                CreatedAt = DateTimeOffset.UtcNow,
                NextAttemptAt = DateTimeOffset.UtcNow,
            };
            Items.Add(item);
            return item;
        }

        public bool UpdatePendingPayload(Guid id, Dictionary<string, string> payload)
        {
            var index = Items.FindIndex(item => item.Id == id);
            Items[index].Payload = payload;
            return true;
        }

        public bool UpdatePendingPriority(Guid id, int priority)
        {
            var index = Items.FindIndex(item => item.Id == id);
            Items[index].Priority = priority;
            return true;
        }
    }
}
