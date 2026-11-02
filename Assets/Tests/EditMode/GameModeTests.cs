using NUnit.Framework;
using UnityEngine;

/// The game picker's modes. Backs up and restores the player's saved choice.
public class GameModeTests
{
    private string _saved;

    [SetUp]
    public void BackUp() => _saved = PlayerPrefs.GetString(GameModes.PrefsKey, null);

    [TearDown]
    public void Restore()
    {
        if (_saved == null) PlayerPrefs.DeleteKey(GameModes.PrefsKey);
        else PlayerPrefs.SetString(GameModes.PrefsKey, _saved);
        PlayerPrefs.Save();
    }

    [Test]
    public void ThreeGamesAreOffered_InMenuOrder()
    {
        CollectionAssert.AreEqual(new[] { "johoti", "freeplay", "blackjack" },
            new[] { GameModes.All[0].Id, GameModes.All[1].Id, GameModes.All[2].Id });
        foreach (var mode in GameModes.All) Assert.IsTrue(mode.Available, mode.Id);
        Assert.IsInstanceOf<BlackjackRules>(GameModes.Blackjack.CreateRules());
    }

    [Test]
    public void Johoti_DealsAll52_ThirteenEachToFour()
    {
        var rules = (DealerRules)GameModes.Johoti.CreateRules();
        Assert.AreEqual(4, rules.Players);
        Assert.AreEqual(13, rules.CardsPerPlayer);

        var sm = new GameStateManager(rules);
        sm.Apply(new PickUpDeckOperation());
        rules.PacketSize = 13;
        for (int i = 0; i < 4; i++) Assert.IsTrue(sm.Apply(rules.NextPacket(sm.Table)).Success);
        Assert.AreEqual(RoundPhase.Dealt, rules.PhaseOf(sm.Table));
        Assert.AreEqual(0, sm.Table.Count(ZoneId.RightHand), "nothing left in the dealer's hand");
    }

    [Test]
    public void Selection_IsRemembered_AndUnavailableModesAreRefused()
    {
        Assert.IsTrue(GameModes.Select(GameModes.Johoti));
        Assert.AreSame(GameModes.Johoti, GameModes.Selected);

        var comingSoon = new GameMode("later", "Later", "", () => new FreePlayRules(), available: false);
        Assert.IsFalse(GameModes.Select(comingSoon));
        Assert.AreSame(GameModes.Johoti, GameModes.Selected, "unchanged");

        Assert.IsTrue(GameModes.Select(GameModes.Blackjack));
        Assert.AreSame(GameModes.Blackjack, GameModes.Selected);
        PlayerPrefs.SetString(GameModes.PrefsKey, "no-such-game");
        Assert.AreSame(GameModes.FreePlay, GameModes.Selected);
    }

    [Test]
    public void EveryModeBuildsRules()
    {
        foreach (var mode in GameModes.All)
        {
            Assert.IsNotNull(mode.CreateRules(), mode.Id);
            Assert.IsFalse(string.IsNullOrEmpty(mode.DisplayName));
        }
    }
}
