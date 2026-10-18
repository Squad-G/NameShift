namespace NameShift.Core;

/// <summary>One step of the rename pipeline (RegEx, Replace, Case, Remove, Add, Numbering…).</summary>
public interface IRenameRule
{
    bool Enabled { get; }

    /// <summary>Transforms the name (without extension) of the item at <paramref name="index"/> in the list.</summary>
    string Apply(string name, int index);
}
