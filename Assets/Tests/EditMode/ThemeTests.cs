using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

public class ThemeTests
{
    private readonly List<Object> _cleanup = new List<Object>();

    [SetUp]
    public void ResetThemes() => ThemeService.Refresh();

    [TearDown]
    public void Cleanup()
    {
        foreach (var o in _cleanup) if (o != null) Object.DestroyImmediate(o);
        _cleanup.Clear();
        ThemeService.Refresh();
    }

    [Test]
    public void BuiltInThemes_AreAvailable_WithUniqueIds()
    {
        var ids = new HashSet<string>();
        foreach (var theme in ThemeService.Available)
            Assert.IsTrue(ids.Add(theme.Id), $"duplicate theme id {theme.Id}");
        Assert.IsNotNull(ThemeService.Find("classic"));
        Assert.IsNotNull(ThemeService.Find("midnight"));
        Assert.IsNull(ThemeService.Find("no-such-theme"));
        Assert.IsNotNull(ThemeService.Current);
    }

    [Test]
    public void Select_ChangesCurrent_AndNotifies()
    {
        TableTheme notified = null;
        void OnChanged(TableTheme t) => notified = t;
        ThemeService.Changed += OnChanged;
        try
        {
            var target = ThemeService.Current.Id == "midnight" ? "classic" : "midnight";
            Assert.IsTrue(ThemeService.Select(target, remember: false));
            Assert.AreEqual(target, ThemeService.Current.Id);
            Assert.AreSame(ThemeService.Current, notified);
            Assert.IsFalse(ThemeService.Select("no-such-theme", remember: false));
        }
        finally
        {
            ThemeService.Changed -= OnChanged;
        }
    }

    [Test]
    public void Cycle_VisitsEveryThemeAndWraps()
    {
        ThemeService.Select(ThemeService.Available[0], remember: false);
        var seen = new HashSet<string> { ThemeService.Current.Id };
        // Cycle remembers the choice; restore the player's preference afterwards
        string saved = PlayerPrefs.GetString(ThemeService.PrefsKey, null);
        try
        {
            for (int i = 0; i < ThemeService.Available.Count; i++)
            {
                ThemeService.Cycle();
                seen.Add(ThemeService.Current.Id);
            }
        }
        finally
        {
            if (saved == null) PlayerPrefs.DeleteKey(ThemeService.PrefsKey);
            else PlayerPrefs.SetString(ThemeService.PrefsKey, saved);
        }
        Assert.AreEqual(ThemeService.Available.Count, seen.Count);
        Assert.AreSame(ThemeService.Available[0], ThemeService.Current, "wrapped back to the start");
    }

    [Test]
    public void CardAtlas_BuildsFromAnyStyle()
    {
        var tex = Track(CardAtlas.Build(BuiltInThemes.Midnight().Cards));
        Assert.AreEqual(CardAtlas.Width, tex.width);
        Assert.AreEqual(CardAtlas.Height, tex.height);

        // UV tiles for every card are distinct and inside the texture
        var seen = new HashSet<Rect>();
        for (int c = 0; c < 52; c++)
        {
            var uv = CardAtlas.FaceUV(c);
            Assert.IsTrue(seen.Add(uv));
            Assert.That(uv.xMin, Is.GreaterThanOrEqualTo(0f));
            Assert.That(uv.yMax, Is.LessThanOrEqualTo(1f));
        }
        Assert.IsFalse(seen.Contains(CardAtlas.BackUV));
    }

    [Test]
    public void Environment_RebuildsForANewTheme_WithoutDuplicating()
    {
        var cam = Track(new GameObject("Cam", typeof(Camera))).GetComponent<Camera>();
        var env = Track(new GameObject("Env")).AddComponent<TableEnvironment>();

        env.Build(cam, BuiltInThemes.Classic());
        Assert.AreEqual(1, env.transform.childCount);
        var felt = env.transform.GetChild(0).Find("Felt");
        Assert.IsNotNull(felt, "basic table built");
        Assert.AreEqual(TableLayout.SurfaceY, felt.position.y + felt.localScale.y, 1e-4f, "felt top at the card surface");

        var midnight = BuiltInThemes.Midnight();
        env.Build(cam, midnight);
        Assert.AreEqual(1, env.transform.childCount, "old environment removed");
        Assert.AreSame(midnight, env.Theme);
        Assert.AreEqual(midnight.Table.Background, cam.backgroundColor);
        Assert.IsNotNull(cam.GetComponent<SeatedCameraRig>());
    }

    [Test]
    public void Environment_UsesThemePrefab_WhenProvided()
    {
        var prefab = Track(new GameObject("Custom Room"));
        var theme  = BuiltInThemes.Classic();
        theme.EnvironmentPrefab = prefab;

        var cam = Track(new GameObject("Cam", typeof(Camera))).GetComponent<Camera>();
        var env = Track(new GameObject("Env")).AddComponent<TableEnvironment>();
        env.Build(cam, theme);

        var built = env.transform.GetChild(0);
        Assert.IsNull(built.Find("Felt"), "built-in table skipped");
        Assert.IsNotNull(built.Find("Custom Room(Clone)"));
        Assert.IsNotNull(built.Find("Key Light"), "theme lighting still applied");
    }

    private T Track<T>(T o) where T : Object
    {
        _cleanup.Add(o);
        return o;
    }
}
