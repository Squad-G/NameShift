using System.Collections.Generic;

namespace NameShift.Ui.Presets;

/// <summary>The preset store's only disk access: its own files in its own folder. A seam so a test can fail a write.</summary>
internal interface IPresetFiles
{
    /// <summary>True when the folder exists.</summary>
    bool FolderExists(string folder);

    /// <summary>The full paths of the files directly in the folder whose names end in ".json" (ignoring case).</summary>
    IReadOnlyList<string> ListJsonFiles(string folder);

    /// <summary>True when a file (any file) has that path.</summary>
    bool FileExists(string path);

    /// <summary>The file's bytes.</summary>
    byte[] ReadAll(string path);

    /// <summary>Writes a temporary file in the same folder (created if needed), flushes it, then replaces the file with
    /// it or moves it into place: a reader sees the old file or the new one. On failure the temporary file is removed
    /// and the old file is intact.</summary>
    void WriteAtomic(string path, byte[] content);

    /// <summary>Deletes the file.</summary>
    void Delete(string path);
}
