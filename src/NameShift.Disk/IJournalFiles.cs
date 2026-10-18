namespace NameShift.Disk;

/// <summary>The undo journal's only access to the disk: its own files under the history root, never user items.</summary>
internal interface IJournalFiles
{
    /// <summary>True when the file exists.</summary>
    bool Exists(string path);

    /// <summary>The whole content of the file.</summary>
    byte[] ReadAll(string path);

    /// <summary>Writes the file so that a reader sees the old content or the new one, never a partial one; creates the
    /// folder when needed.</summary>
    void WriteAtomic(string path, byte[] content);

    /// <summary>Deletes the journal's own file; a missing file is not an error.</summary>
    void Delete(string path);
}
