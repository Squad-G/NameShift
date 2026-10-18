namespace NameShift.Ui;

/// <summary>What happened to a path typed in the path bar.</summary>
public enum PathBarResult
{
    Navigated,
    Unchanged,
    Empty,
    Invalid,
    NotFound,
    NotAFolder,
    Superseded,
}
