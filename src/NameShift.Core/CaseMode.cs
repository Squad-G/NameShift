namespace NameShift.Core;

/// <summary>How the Case rule rewrites the base name.</summary>
public enum CaseMode
{
    /// <summary>The name is left as it is.</summary>
    Same = 0,

    /// <summary>Every letter in lower case.</summary>
    Lower,

    /// <summary>Every letter in upper case.</summary>
    Upper,

    /// <summary>Each word starts with a capital, except the listed exceptions.</summary>
    Title,

    /// <summary>Only the first letter of each sentence is a capital.</summary>
    Sentence,
}
