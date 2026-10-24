using System;
using System.Collections.Generic;
using NameShift.Core;

namespace NameShift.Ui.Rules;

/// <summary>The settings of the Move or copy panel; BuildRule maps them to the Core <see cref="MoveCopyRule"/>.</summary>
/// <remarks>The numbers are held as typed. An unusable count (or an unusable position, when it applies) makes
/// BuildRule return null, so nothing is moved or copied until the value is fixed.</remarks>
public sealed class MoveCopyPanelModel : RulePanelModel
{
    private static readonly MoveCopyMode[] ModeList = { MoveCopyMode.Move, MoveCopyMode.Copy };

    private static readonly MoveCopySource[] SourceList = { MoveCopySource.Start, MoveCopySource.End };

    private static readonly MoveCopyTarget[] TargetList =
    {
        MoveCopyTarget.Start, MoveCopyTarget.End, MoveCopyTarget.Position,
    };

    private MoveCopyMode _mode = MoveCopyMode.Move;
    private MoveCopySource _source = MoveCopySource.Start;
    private string _countText = "";
    private MoveCopyTarget _target = MoveCopyTarget.End;
    private string _positionText = "";
    private string _separator = "";

    public MoveCopyPanelModel()
        : base(RuleStage.MoveCopy, Strings.MoveCopyTitle)
    {
    }

    /// <summary>The list order: Move, Copy.</summary>
    public static IReadOnlyList<MoveCopyMode> Modes => ModeList;

    /// <summary>The list order: Start, End.</summary>
    public static IReadOnlyList<MoveCopySource> Sources => SourceList;

    /// <summary>The list order: Start, End, Position.</summary>
    public static IReadOnlyList<MoveCopyTarget> Targets => TargetList;

    /// <summary>The mode's position in <see cref="Modes"/>, or -1 when it is not listed.</summary>
    public static int IndexOf(MoveCopyMode mode) => Array.IndexOf(ModeList, mode);

    /// <summary>The source's position in <see cref="Sources"/>, or -1 when it is not listed.</summary>
    public static int IndexOf(MoveCopySource source) => Array.IndexOf(SourceList, source);

    /// <summary>The target's position in <see cref="Targets"/>, or -1 when it is not listed.</summary>
    public static int IndexOf(MoveCopyTarget target) => Array.IndexOf(TargetList, target);

    /// <exception cref="ArgumentOutOfRangeException">The mode is not a defined value (a programmer error).</exception>
    public MoveCopyMode Mode
    {
        get => _mode;
        set
        {
            if (Array.IndexOf(ModeList, value) < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown move or copy mode.");
            }

            SetField(ref _mode, value);
        }
    }

    /// <exception cref="ArgumentOutOfRangeException">The source is not a defined value (a programmer error).</exception>
    public MoveCopySource Source
    {
        get => _source;
        set
        {
            if (Array.IndexOf(SourceList, value) < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown move or copy source.");
            }

            SetField(ref _source, value);
        }
    }

    /// <exception cref="ArgumentOutOfRangeException">The target is not a defined value (a programmer error).</exception>
    public MoveCopyTarget Target
    {
        get => _target;
        set
        {
            if (Array.IndexOf(TargetList, value) < 0)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "Unknown move or copy target.");
            }

            SetField(ref _target, value);
        }
    }

    /// <summary>The count of characters as typed.</summary>
    public string CountText
    {
        get => _countText;
        set => SetField(ref _countText, value ?? "");
    }

    /// <summary>The position as typed (Position target only).</summary>
    public string PositionText
    {
        get => _positionText;
        set => SetField(ref _positionText, value ?? "");
    }

    /// <summary>The text put between the taken characters and the rest; never trimmed.</summary>
    public string Separator
    {
        get => _separator;
        set => SetField(ref _separator, value ?? "");
    }

    /// <summary>The typed count, or null when it is empty or not a whole number from 1.</summary>
    public int? Count => TextPosition.Parse(CountText);

    /// <summary>The typed position, or null when it is empty or not a whole number from 1.</summary>
    public int? Position => TextPosition.Parse(PositionText);

    /// <summary>True when the count is typed and cannot be used; an empty box is not marked.</summary>
    public bool CountInvalid => !string.IsNullOrWhiteSpace(CountText) && Count == null;

    /// <summary>The position is used for the Position target only.</summary>
    public bool PositionApplies => Target == MoveCopyTarget.Position;

    /// <summary>True when the position applies and cannot be used; an empty box is marked then.</summary>
    public bool PositionInvalid => PositionApplies && Position == null;

    public override bool IsDefault =>
        Mode == MoveCopyMode.Move && Source == MoveCopySource.Start && CountText.Length == 0
        && Target == MoveCopyTarget.End && PositionText.Length == 0 && Separator.Length == 0;

    /// <summary>The rule, or null while the count (or the position, when it applies) is unusable.</summary>
    public override IRenameRule? BuildRule()
    {
        if (Count is not int count)
        {
            return null;
        }

        if (PositionApplies && Position == null)
        {
            return null;
        }

        return new MoveCopyRule(Mode, Source, count, Target, PositionApplies ? Position : null, Separator);
    }

    protected override void ResetSettings()
    {
        Mode = MoveCopyMode.Move;
        Source = MoveCopySource.Start;
        CountText = "";
        Target = MoveCopyTarget.End;
        PositionText = "";
        Separator = "";
    }
}
