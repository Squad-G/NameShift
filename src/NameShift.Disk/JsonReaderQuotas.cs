using System.Xml;

namespace NameShift.Disk;

/// <summary>The reader quotas of every JSON file NameShift reads back (the journal, the settings, the presets): no
/// limit on a long string or a big file, but a nesting depth the real files never come near, so a hostile or damaged
/// file is refused at once instead of being read level by level until the stack runs out.</summary>
public static class JsonReaderQuotas
{
    /// <summary>The deepest nesting a file may have. The real files are only a few levels deep.</summary>
    public const int MaxDepth = 32;

    /// <summary>A copy of <see cref="XmlDictionaryReaderQuotas.Max"/> with <see cref="MaxDepth"/> as its depth. Copied,
    /// not new: the defaults would cap a string at 8,192 characters and drop a long preset text.</summary>
    public static XmlDictionaryReaderQuotas Create()
    {
        var quotas = new XmlDictionaryReaderQuotas();
        XmlDictionaryReaderQuotas.Max.CopyTo(quotas);
        quotas.MaxDepth = MaxDepth;
        return quotas;
    }
}
