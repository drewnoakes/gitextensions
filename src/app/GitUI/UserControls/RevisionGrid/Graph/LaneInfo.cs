using GitExtensions.Extensibility.Git;

namespace GitUI.UserControls.RevisionGrid.Graph;

public sealed class LaneInfo
{
    public LaneInfo(RevisionGraphSegment startSegment, RevisionGraphSegment? segmentToTheLeft, RevisionGraphSegment? segmentToTheRight = null)
    {
        StartRevision = startSegment.Child;
        Color = GetColor(
            colorSeed: StartRevision.Objectid.GetHashCode() ^ startSegment.Parent.Objectid.GetHashCode(),
            startRevision: StartRevision,
            segmentToTheLeft,
            segmentToTheRight);
    }

    public LaneInfo(RevisionGraphSegment startSegment, RevisionGraphSegment? segmentToTheLeft, RevisionGraphSegment? segmentToTheRight, LaneInfo derivedFrom)
    {
        StartRevision = startSegment.Parent;
        Color = GetColor(
            colorSeed: StartRevision.Objectid.GetHashCode(),
            startRevision: StartRevision,
            segmentToTheLeft,
            segmentToTheRight,
            derivedFrom.Color);
    }

    public int Color { get; }

    public RevisionGraphRevision StartRevision { get; }

    public int StartScore => StartRevision.Score;

    private static int GetColor(int colorSeed, RevisionGraphRevision? startRevision, RevisionGraphSegment? segmentToTheLeft, RevisionGraphSegment? segmentToTheRight, int? derivedFromColor = null)
    {
        int? leftLaneColor = segmentToTheLeft?.LaneInfo?.Color;
        int? rightLaneColor = segmentToTheRight?.LaneInfo?.Color;

        // When the lane starts at a named branch, always use that branch's stable color
        // regardless of neighbor conflicts — this keeps the lane color consistent with
        // the branch label capsule, which also uses the branch-name hash.
        int? preferredColor = GetColorIndexFromBranchRef(startRevision);
        if (preferredColor is not null)
        {
            return preferredColor.Value;
        }

        // Fall back to hash-based assignment, avoiding neighbor and derived colors.
        for (; ; ++colorSeed)
        {
            int color = RevisionGraphLaneColor.GetColorForLane(colorSeed);
            if (color != leftLaneColor && color != rightLaneColor && color != derivedFromColor)
            {
                return color;
            }
        }
    }

    private static int? GetColorIndexFromBranchRef(RevisionGraphRevision? revision)
    {
        IReadOnlyList<IGitRef>? refs = revision?.GitRevision?.Refs;
        if (refs is null or { Count: 0 })
        {
            return null;
        }

        // Prefer a local branch name; fall back to a remote branch's local name.
        string? branchName = null;
        foreach (IGitRef r in refs)
        {
            if (r.IsHead)
            {
                // Prefer the first local branch alphabetically for stability.
                if (branchName is null || string.CompareOrdinal(r.LocalName, branchName) < 0)
                {
                    branchName = r.LocalName;
                }
            }
        }

        if (branchName is null)
        {
            foreach (IGitRef r in refs)
            {
                if (r.IsRemote)
                {
                    if (branchName is null || string.CompareOrdinal(r.LocalName, branchName) < 0)
                    {
                        branchName = r.LocalName;
                    }
                }
            }
        }

        return branchName is not null
            ? RevisionGraphLaneColor.GetColorIndexForBranchName(branchName)
            : null;
    }
}
