using System;
using System.Text;

namespace NameShift.Core;

/// <summary>Adds text around and inside the base name (the extension is never touched).</summary>
public sealed class AddRule : IRenameRule
{
    /// <summary>Creates the rule; every text may be empty (that part does nothing).</summary>
    public AddRule(string prefix = "", string insert = "", int? insertPosition = null, bool insertFromEnd = false,
        string suffix = "", bool wordSpace = false)
    {
        Prefix = prefix ?? throw new ArgumentNullException(nameof(prefix));
        Insert = insert ?? throw new ArgumentNullException(nameof(insert));
        InsertPosition = insertPosition;
        InsertFromEnd = insertFromEnd;
        Suffix = suffix ?? throw new ArgumentNullException(nameof(suffix));
        WordSpace = wordSpace;
    }

    /// <summary>Text put before the name.</summary>
    public string Prefix { get; }

    /// <summary>Text put inside the name at <see cref="InsertPosition"/>.</summary>
    public string Insert { get; }

    /// <summary>The 1-based position of <see cref="Insert"/>; null or below 1 means the insert part is ignored.</summary>
    public int? InsertPosition { get; }

    /// <summary>True: the position counts from the end (characters left after the inserted text).</summary>
    public bool InsertFromEnd { get; }

    /// <summary>Text put after the base name, before the extension.</summary>
    public string Suffix { get; }

    /// <summary>True: a space is put between camel-case words.</summary>
    public bool WordSpace { get; }

    /// <inheritdoc />
    public RuleStage Stage => RuleStage.Add;

    /// <inheritdoc />
    public bool Enabled => Prefix.Length > 0 || Suffix.Length > 0 || WordSpace || InsertApplies;

    private bool InsertApplies => Insert.Length > 0 && InsertPosition >= 1;

    /// <inheritdoc />
    public NameParts Apply(NameParts name, ItemContext context)
    {
        var baseName = WordSpace ? SpaceWords(name.BaseName) : name.BaseName;
        if (InsertApplies)
        {
            baseName = baseName.Insert(TextPosition.ToInsertIndex(InsertPosition!.Value, baseName.Length, InsertFromEnd), Insert);
        }

        return name.WithBaseName(Prefix + baseName + Suffix);
    }

    /// <summary>Puts a space before each upper-case letter that follows a lower-case letter or a digit.</summary>
    private static string SpaceWords(string text)
    {
        var result = new StringBuilder(text.Length + 4);
        for (var i = 0; i < text.Length; i++)
        {
            if (i > 0 && char.IsUpper(text[i]) && (char.IsLower(text[i - 1]) || char.IsDigit(text[i - 1])))
            {
                result.Append(' ');
            }

            result.Append(text[i]);
        }

        return result.ToString();
    }
}
