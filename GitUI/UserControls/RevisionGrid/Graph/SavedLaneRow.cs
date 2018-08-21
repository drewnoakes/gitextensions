using System;
using System.Linq;
using System.Text;
using JetBrains.Annotations;

namespace GitUI.UserControls.RevisionGrid.Graph
{
    /// <summary>
    /// Immutable representation of a node's row.
    /// </summary>
    internal sealed class SavedLaneRow : ILaneRow
    {
        [NotNull] private readonly Edge[] _edges;

        public Node Node { get; }
        public int NodeLane { get; }

        public SavedLaneRow([NotNull] Node node, int nodeLane, [NotNull] Edge[] edges)
        {
            Node = node;
            NodeLane = nodeLane;
            _edges = edges;
        }

        public LaneInfo this[int col, int row]
        {
            get
            {
                // Find the row'th edge that starts in col
                var count = 0;

                foreach (var edge in _edges)
                {
                    if (edge.Start == col)
                    {
                        if (count == row)
                        {
                            return edge.Data;
                        }

                        count++;
                    }
                }

                throw new Exception("Bad lane");
            }
        }

        public int Count
        {
            get
            {
                var count = -1;

                // ReSharper disable once LoopCanBeConvertedToQuery
                foreach (var edge in _edges)
                {
                    if (edge.Start > count)
                    {
                        count = edge.Start;
                    }
                }

                return count + 1;
            }
        }

        public int LaneInfoCount(int lane)
        {
            return _edges.Count(edge => edge.Start == lane);
        }

#if DEBUG
        public override string ToString()
        {
            var s = new StringBuilder()
                .Append(NodeLane).Append('/').Append(Count).Append(": ");

            for (var i = 0; i < Count; i++)
            {
                if (i == NodeLane)
                {
                    s.Append('*');
                }

                s.Append('{');

                for (var j = 0; j < LaneInfoCount(i); j++)
                {
                    s.Append(' ').Append(this[i, j]);
                }

                s.Append(" }, ");
            }

            s.Append(Node);

            return s.ToString();
        }
#endif
    }
}