using System;
using System.IO;
using System.Text;

/// <summary>
/// A validated, recoverable local file. The last committed record stays in
/// .bak while a replacement is staged and verified. A failed/truncated .tmp
/// never wins over a valid committed file. No filesystem-specific Replace
/// call is required, so this also works on WebGL's virtual filesystem.
/// </summary>
public static class RecoverableFile
{
    public static string Read(string path, Func<string, bool> validate, out bool recovered)
    {
        recovered = false;
        foreach (string candidate in new[] { path, path + ".bak", path + ".bak.tmp", path + ".tmp" })
        {
            if (!File.Exists(candidate)) continue;
            string text;
            try { text = File.ReadAllText(candidate); }
            catch (IOException) { continue; }
            if (!validate(text)) continue;
            recovered = candidate != path;
            return text;
        }
        throw new InvalidDataException("No valid saved copy was found.");
    }

    public static void Write(string path, string text, Func<string, bool> validate)
    {
        if (!validate(text)) throw new InvalidDataException("The saved record failed validation.");
        Directory.CreateDirectory(Path.GetDirectoryName(path));

        // Preserve the last known-good copy BEFORE overwriting a leftover
        // staging file, including recovery after an interrupted first save.
        string previous = null;
        try { previous = Read(path, validate, out _); }
        catch (InvalidDataException) { }
        if (previous != null)
        {
            // Do not truncate an already-valid backup (it may be our only
            // good copy). Stage its update before moving it into place.
            string backupStage = path + ".bak.tmp";
            // Recovery may have read this exact file as the sole good copy.
            // Reuse it unchanged rather than truncating our recovery source.
            if (!File.Exists(backupStage) || File.ReadAllText(backupStage) != previous)
                WriteFlushed(backupStage, previous);
            if (!validate(File.ReadAllText(backupStage)))
                throw new IOException("Could not verify the previous saved copy.");
            if (File.Exists(path + ".bak")) File.Delete(path + ".bak");
            File.Move(backupStage, path + ".bak");
        }

        WriteFlushed(path + ".tmp", text);
        if (!validate(File.ReadAllText(path + ".tmp")))
            throw new IOException("Could not verify the new saved copy.");
        if (File.Exists(path)) File.Delete(path);
        File.Move(path + ".tmp", path);
    }

    static void WriteFlushed(string path, string text)
    {
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None);
        byte[] bytes = new UTF8Encoding(false).GetBytes(text);
        stream.Write(bytes, 0, bytes.Length);
#if UNITY_WEBGL && !UNITY_EDITOR
        stream.Flush(); // IndexedDB is committed by LocalSavePersistence.
#else
        stream.Flush(true);
#endif
    }
}
