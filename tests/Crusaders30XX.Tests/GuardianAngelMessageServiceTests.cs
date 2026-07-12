using System;
using System.Collections.Generic;
using System.Linq;
using Crusaders30XX.ECS.Data.Ids;
using Crusaders30XX.ECS.Factories;
using Crusaders30XX.ECS.Systems;
using Xunit;

namespace Crusaders30XX.Tests;

public sealed class GuardianAngelMessageServiceTests
{
    [Fact]
    public void EveryRegisteredCardHasTwoValidMessages()
    {
        foreach (CardId id in Enum.GetValues<CardId>())
        {
            Assert.NotNull(CardFactory.Create(id));
            Assert.True(GuardianAngelMessageService.HasCardMessages(id), $"Missing messages for card {id}");
            AssertValidPool(id.ToString(), GuardianAngelMessageService.GetCardMessages(id));
        }
    }

    [Fact]
    public void EveryRegisteredMedalHasTwoValidMessages()
    {
        foreach (MedalId id in Enum.GetValues<MedalId>())
        {
            Assert.NotNull(MedalFactory.Create(id));
            Assert.True(GuardianAngelMessageService.HasMedalMessages(id), $"Missing messages for medal {id}");
            AssertValidPool(id.ToString(), GuardianAngelMessageService.GetMedalMessages(id));
        }
    }

    [Fact]
    public void EveryRegisteredEnemyAttackHasTwoValidMessages()
    {
        foreach (EnemyAttackId id in Enum.GetValues<EnemyAttackId>())
        {
            Assert.NotNull(EnemyAttackFactory.Create(id));
            Assert.True(GuardianAngelMessageService.HasEnemyAttackMessages(id), $"Missing messages for attack {id}");
            AssertValidPool(id.ToString(), GuardianAngelMessageService.GetEnemyAttackMessages(id));
        }
    }

    [Fact]
    public void SelectionDoesNotImmediatelyRepeat()
    {
        string first = GuardianAngelMessageService.GetCardMessage(CardId.Strike);
        string second = GuardianAngelMessageService.GetCardMessage(CardId.Strike);
        Assert.NotEqual(first, second);
    }

    private static void AssertValidPool(string id, IReadOnlyList<string> messages)
    {
        Assert.Equal(2, messages.Count);
        Assert.Equal(2, messages.Distinct(StringComparer.Ordinal).Count());
        foreach (string message in messages)
        {
            Assert.False(string.IsNullOrWhiteSpace(message), $"Empty message for {id}");
            Assert.True(message.Length <= 80, $"Message for {id} exceeds 80 characters: {message}");
            Assert.All(message, character => Assert.InRange((int)character, 0, 127));
            Assert.DoesNotContain("~", message, StringComparison.Ordinal);
            Assert.DoesNotContain("^", message, StringComparison.Ordinal);
            Assert.DoesNotContain(">_<", message, StringComparison.Ordinal);
            Assert.DoesNotContain(":P", message, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("-san", message, StringComparison.OrdinalIgnoreCase);
        }
    }
}
