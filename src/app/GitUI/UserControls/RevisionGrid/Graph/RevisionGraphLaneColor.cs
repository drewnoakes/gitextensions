using System.Diagnostics;
using GitExtUtils.GitUI.Theming;
using GitUI.Theming;

namespace GitUI.UserControls.RevisionGrid.Graph;

public static class RevisionGraphLaneColor
{
    public static int GetColorForLane(int seed)
    {
        return Math.Abs(seed) % PresetGraphBrushes.Count;
    }

    /// <summary>
    ///  Returns a stable color index for the given branch local name using a deterministic hash.
    ///  The index is in the range <c>[0, PresetGraphBrushes.Count)</c>.
    /// </summary>
    public static int GetColorIndexForBranchName(string branchName)
    {
        unchecked
        {
            // djb2 hash — deterministic, does not rely on runtime randomization.
            int hash = 5381;
            foreach (char c in branchName)
            {
                hash = (hash << 5) + hash + c;
            }

            return Math.Abs(hash) % PresetGraphBrushes.Count;
        }
    }

    /// <summary>Returns the preset <see cref="Color"/> for the given lane color index.</summary>
    public static Color GetColorForLaneIndex(int index)
        => ((SolidBrush)PresetGraphBrushes[index]).Color;

    /// <summary>
    ///  Returns a muted variant of the preset color at <paramref name="laneColorIndex"/>,
    ///  used for remote-tracking branch labels to visually pair them with their local counterpart
    ///  while remaining visually distinct.
    /// </summary>
    public static Color GetRemoteVariantColor(int laneColorIndex)
    {
        Color baseColor = GetColorForLaneIndex(laneColorIndex);
        const int midGray = 0x80;
        return Color.FromArgb(
            (baseColor.R + midGray) / 2,
            (baseColor.G + midGray) / 2,
            (baseColor.B + midGray) / 2);
    }

    public static Color NonRelativeColor { get; } = AppColor.GraphNonRelativeBranch.GetThemeColor();

    internal static Brush NonRelativeBrush { get; }

    internal static readonly List<Brush> PresetGraphBrushes = [];

    static RevisionGraphLaneColor()
    {
        Color[] branchColors = [.. Enum.GetNames<AppColor>()
            .Where(name => name.StartsWith(nameof(AppColor.GraphBranch1)[..^1]))
            .Select(name => Enum.Parse<AppColor>(name).GetThemeColor())
            .Where(color => !color.IsEmpty)
            .Distinct()];

        const int minBranchColors = 4;
        if (branchColors.Length < minBranchColors)
        {
            Trace.WriteLine(@"At least {minBranchColors} different graph colors must be configured - using crying fallback");
            branchColors = [Color.Cyan, Color.Magenta, Color.Yellow, Color.Lime];
        }

        foreach (Color color in branchColors)
        {
            PresetGraphBrushes.Add(new SolidBrush(color));
        }

        NonRelativeBrush = new SolidBrush(NonRelativeColor);
    }

    public static Brush GetBrushForLane(int laneColor)
    {
        return PresetGraphBrushes[laneColor];
    }
}
