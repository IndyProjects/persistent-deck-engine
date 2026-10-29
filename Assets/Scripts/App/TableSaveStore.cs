using System;
using System.IO;
using UnityEngine;

/// Reads and writes the table as JSON in Application.persistentDataPath.
/// A missing, corrupt or invalid file is reported and ignored — the game then starts from a fresh deck.
public static class TableSaveStore
{
    public static string FilePath => Path.Combine(Application.persistentDataPath, "table.json");

    public static void Save(GameStateManager sm)
    {
        string path = FilePath;
        string temp = path + ".tmp";
        try
        {
            // Write to a temp file first so a crash mid-write never leaves a truncated save
            File.WriteAllText(temp, JsonUtility.ToJson(sm.CreateSaveData(), true));
            File.Copy(temp, path, overwrite: true);
            File.Delete(temp);
            Debug.Log($"[TableSaveStore] Saved table to {path}");
        }
        catch (Exception ex) when (ex is IOException || ex is UnauthorizedAccessException)
        {
            Debug.LogError($"[TableSaveStore] Could not save table: {ex.Message}");
        }
    }

    public static bool TryLoad(GameStateManager sm)
    {
        string path = FilePath;
        if (!File.Exists(path)) return false;
        try
        {
            var data = JsonUtility.FromJson<TableSaveData>(File.ReadAllText(path));
            sm.Load(data);
            Debug.Log($"[TableSaveStore] Loaded table from {path} (round {sm.RoundNumber})");
            return true;
        }
        catch (Exception ex) when (ex is ArgumentException || ex is IOException || ex is UnauthorizedAccessException)
        {
            Debug.LogWarning($"[TableSaveStore] Ignoring unreadable save at {path}: {ex.Message}");
            return false;
        }
    }

    public static void Delete()
    {
        if (File.Exists(FilePath)) File.Delete(FilePath);
    }
}
