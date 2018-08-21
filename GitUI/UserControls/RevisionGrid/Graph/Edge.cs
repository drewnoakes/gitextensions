namespace GitUI.UserControls.RevisionGrid.Graph
{
    internal readonly struct Edge
    {
        public LaneInfo Data { get; }
        public int Start { get; }
        public int End => Data.ConnectLane;

        public Edge(LaneInfo data, int start)
        {
            Data = data;
            Start = start;
        }

#if DEBUG
        public override string ToString() => $"{Start}->{End}: {Data}";
#endif
    }
}