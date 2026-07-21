using ChurchSuffering.ECS.Systems;
using Xunit;

namespace ChurchSuffering.Tests;

public sealed class GuardianAngelSpeechQueueTests
{
    [Fact]
    public void DequeuesByPriorityThenArrival()
    {
        var queue = new GuardianAngelSpeechQueue();
        queue.Enqueue(Request("card:a", GuardianSpeechCategory.Card, 100, 0f), 0f);
        queue.Enqueue(Request("attack:a", GuardianSpeechCategory.EnemyAttack, 300, 0.2f), 0.2f);
        queue.Enqueue(Request("attack:b", GuardianSpeechCategory.EnemyAttack, 300, 0.3f), 0.3f);

        Assert.True(queue.TryDequeue(0.4f, out var first));
        Assert.Equal("attack:a", first.SourceKey);
        Assert.True(queue.TryDequeue(0.4f, out var second));
        Assert.Equal("attack:b", second.SourceKey);
    }

    [Fact]
    public void CoalescesDuplicatePendingSource()
    {
        var queue = new GuardianAngelSpeechQueue();
        Assert.True(queue.Enqueue(Request("card:a", GuardianSpeechCategory.Card, 100, 0f), 0f));
        Assert.False(queue.Enqueue(Request("card:a", GuardianSpeechCategory.Card, 100, 0.1f), 0.1f));
        Assert.Single(queue.Pending);
    }

    [Fact]
    public void ExpiresStaleMessages()
    {
        var queue = new GuardianAngelSpeechQueue();
        queue.Enqueue(Request("card:a", GuardianSpeechCategory.Card, 100, 0f, 1f), 0f);
        Assert.False(queue.TryDequeue(1.1f, out _));
    }

    [Fact]
    public void OverflowEvictsOldestLowestPriorityMessage()
    {
        var queue = new GuardianAngelSpeechQueue();
        for (int index = 0; index < GuardianAngelSpeechQueue.MaximumPending; index++)
            queue.Enqueue(Request($"card:{index}", GuardianSpeechCategory.Card, 100, index), index);

        queue.Enqueue(Request("attack", GuardianSpeechCategory.EnemyAttack, 300, 9f), 9f);

        Assert.Equal(GuardianAngelSpeechQueue.MaximumPending, queue.Pending.Count);
        Assert.DoesNotContain(queue.Pending, item => item.SourceKey == "card:0");
        Assert.Contains(queue.Pending, item => item.SourceKey == "attack");
    }

    [Fact]
    public void UrgentSpeechOnlyInterruptsRoutineSpeech()
    {
        var card = Request("card", GuardianSpeechCategory.Card, 100, 0f);
        var phase = Request("phase", GuardianSpeechCategory.Phase, 50, 0f);
        var medal = Request("medal", GuardianSpeechCategory.Medal, 250, 0f);
        var attack = Request("attack", GuardianSpeechCategory.EnemyAttack, 300, 0f);

        Assert.True(GuardianAngelSpeechQueue.ShouldInterrupt(card, medal));
        Assert.True(GuardianAngelSpeechQueue.ShouldInterrupt(phase, attack));
        Assert.False(GuardianAngelSpeechQueue.ShouldInterrupt(medal, attack));
        Assert.False(GuardianAngelSpeechQueue.ShouldInterrupt(attack, medal));
    }

    [Theory]
    [InlineData("", 1.6f)]
    [InlineData("A short line.", 1.925f)]
    public void DurationUsesTextLength(string text, float expected)
    {
        Assert.Equal(expected, GuardianAngelSpeechQueue.GetDisplayDuration(text), 3);
    }

    private static GuardianSpeechRequest Request(
        string key,
        GuardianSpeechCategory category,
        int priority,
        float enqueuedAt,
        float maxAge = 20f) =>
        new(key, key, category, priority, enqueuedAt, maxAge, GuardianFlightGesture.None);
}
