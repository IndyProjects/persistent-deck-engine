using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.InputSystem;

/// Rebindable controls. Each test backs up and restores the player's saved bindings.
public class ControlsTests
{
    private const string Prefix = "pde.key.";
    private readonly Dictionary<string, int> _saved = new Dictionary<string, int>();

    [SetUp]
    public void BackUpBindings()
    {
        _saved.Clear();
        for (int i = 0; i < Controls.Count; i++)
        {
            string key = Prefix + (ControlAction)i;
            if (PlayerPrefs.HasKey(key)) _saved[key] = PlayerPrefs.GetInt(key);
        }
        Controls.ResetToDefaults();
    }

    [TearDown]
    public void RestoreBindings()
    {
        for (int i = 0; i < Controls.Count; i++)
        {
            string key = Prefix + (ControlAction)i;
            if (_saved.TryGetValue(key, out int value)) PlayerPrefs.SetInt(key, value);
            else PlayerPrefs.DeleteKey(key);
        }
        PlayerPrefs.Save();
        Controls.Reload();
    }

    /// Keys reachable by the left hand while the right hand stays on the mouse.
    private static readonly HashSet<Key> LeftHandKeys = new HashSet<Key>
    {
        Key.Escape, Key.Tab, Key.LeftShift, Key.LeftCtrl, Key.Space,
        Key.Q, Key.W, Key.E, Key.R, Key.T, Key.A, Key.S, Key.D, Key.F, Key.G, Key.Z, Key.X, Key.C, Key.V,
    };

    [Test]
    public void Defaults_AreAllUnderTheLeftHand()
    {
        for (int i = 0; i < Controls.Count; i++)
        {
            var action = (ControlAction)i;
            Assert.That(LeftHandKeys, Does.Contain(Controls.KeyOf(action)), $"{action} default is {Controls.KeyOf(action)}");
        }
    }

    [Test]
    public void Defaults_AreUniqueWherever_TheyCouldClash()
    {
        for (int a = 0; a < Controls.Count; a++)
        for (int b = a + 1; b < Controls.Count; b++)
        {
            var x = (ControlAction)a;
            var y = (ControlAction)b;
            if (Controls.Clash(x, y))
                Assert.AreNotEqual(Controls.KeyOf(x), Controls.KeyOf(y), $"{x} and {y}");
        }
        Assert.AreEqual(Controls.KeyOf(ControlAction.Deal), Controls.KeyOf(ControlAction.OverhandSlide),
            "the same key may serve different moments (deal at the table, slide during an overhand)");
    }

    [Test]
    public void Binding_ATakenKey_SwapsWithinTheSameContext()
    {
        var swapped = Controls.Bind(ControlAction.Cut, Key.R); // R was Riffle
        Assert.AreEqual(ControlAction.Riffle, swapped);
        Assert.AreEqual(Key.R, Controls.KeyOf(ControlAction.Cut));
        Assert.AreEqual(Key.C, Controls.KeyOf(ControlAction.Riffle), "riffle took cut's old key");
    }

    [Test]
    public void Binding_AKeyUsedInAnotherContext_DoesNotSwap()
    {
        var swapped = Controls.Bind(ControlAction.Cut, Key.A); // A is Riffle-left, used only while shuffling
        Assert.IsNull(swapped);
        Assert.AreEqual(Key.A, Controls.KeyOf(ControlAction.Cut));
        Assert.AreEqual(Key.A, Controls.KeyOf(ControlAction.RiffleLeft));
    }

    [Test]
    public void Cancel_ClashesEverywhere()
    {
        var swapped = Controls.Bind(ControlAction.RiffleLeft, Key.Escape);
        Assert.AreEqual(ControlAction.Cancel, swapped);
        Assert.AreEqual(Key.A, Controls.KeyOf(ControlAction.Cancel));
    }

    [Test]
    public void NumberKeys_AreReservedForPacketSize()
    {
        Assert.IsNull(Controls.Bind(ControlAction.Deal, Key.Digit3));
        Assert.AreEqual(Key.Space, Controls.KeyOf(ControlAction.Deal), "unchanged");
    }

    [Test]
    public void Bindings_PersistAndReset()
    {
        Controls.Bind(ControlAction.History, Key.Q);
        Controls.Reload();
        Assert.AreEqual(Key.Q, Controls.KeyOf(ControlAction.History), "saved and re-read");

        Controls.ResetToDefaults();
        Assert.AreEqual(Key.Tab, Controls.KeyOf(ControlAction.History));
        Controls.Reload();
        Assert.AreEqual(Key.Tab, Controls.KeyOf(ControlAction.History), "the reset was saved too");
    }

    [Test]
    public void EveryActionHasALabel()
    {
        for (int i = 0; i < Controls.Count; i++)
            Assert.IsFalse(string.IsNullOrEmpty(Controls.Label((ControlAction)i)));
        Assert.AreEqual(System.Enum.GetValues(typeof(ControlAction)).Length, Controls.Count);
    }
}
