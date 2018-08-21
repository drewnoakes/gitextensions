using System.Collections.Generic;
using System.Linq;

namespace GitUI.UserControls.RevisionGrid.Graph
{
    internal sealed class Edges
    {
        private readonly List<int> _countEnd = new List<int>();
        private readonly List<int> _countStart = new List<int>();
        private readonly List<Edge> _edges = new List<Edge>();

        public Edge[] GetEdges() => _edges.ToArray();

        public void Clear()
        {
            _countEnd.Clear();
            _countStart.Clear();
            _edges.Clear();
        }

        public LaneInfo Current(int lane, int item)
        {
            int found = 0;
            foreach (Edge e in _edges)
            {
                if (e.Start == lane)
                {
                    if (item == found)
                    {
                        return e.Data;
                    }

                    found++;
                }
            }

            return default;
        }

        public LaneInfo Next(int lane, int item)
        {
            int found = 0;
            foreach (Edge e in _edges)
            {
                if (e.End == lane)
                {
                    if (item == found)
                    {
                        return e.Data;
                    }

                    found++;
                }
            }

            return default;
        }

        public LaneInfo RemoveNext(int lane, int item, out int start, out int end)
        {
            int found = 0;
            for (int i = 0; i < _edges.Count; i++)
            {
                var edge = _edges[i];
                if (edge.End == lane)
                {
                    if (item == found)
                    {
                        start = edge.Start;
                        end = edge.End;
                        _countStart[start]--;
                        _countEnd[end]--;
                        _edges.RemoveAt(i);
                        return edge.Data;
                    }

                    found++;
                }
            }

            start = -1;
            end = -1;
            return default;
        }

        public void Add(int from, LaneInfo data)
        {
            var e = new Edge(data, from);
            _edges.Add(e);

            while (_countStart.Count <= e.Start)
            {
                _countStart.Add(0);
            }

            _countStart[e.Start]++;
            while (_countEnd.Count <= e.End)
            {
                _countEnd.Add(0);
            }

            _countEnd[e.End]++;
        }

        public void Clear(int lane)
        {
            for (int i = _edges.Count - 1; i >= 0; --i)
            {
                int start = _edges[i].Start;
                if (start == lane)
                {
                    int end = _edges[i].End;
                    _countStart[start]--;
                    _countEnd[end]--;
                    _edges.RemoveAt(i);
                }
            }
        }

        public int CountCurrent()
        {
            int count = _countStart.Count;
            while (count > 0 && _countStart[count - 1] == 0)
            {
                count--;
                _countStart.RemoveAt(count);
            }

            return count;
        }

        public int CountCurrent(int lane)
        {
            return _edges.Count(e => e.Start == lane);
        }

        public int CountNext()
        {
            int count = _countEnd.Count;
            while (count > 0 && _countEnd[count - 1] == 0)
            {
                count--;
                _countEnd.RemoveAt(count);
            }

            return count;
        }

        public int CountNext(int lane)
        {
            // This is called quite a bit (as much as 5% of background thread processing),
            // so avoid using Enumerable.Count(predicate).
            var count = 0;

            // ReSharper disable once LoopCanBeConvertedToQuery
            // ReSharper disable once ForCanBeConvertedToForeach
            for (var index = 0; index < _edges.Count; index++)
            {
                var e = _edges[index];

                if (e.End == lane)
                {
                    count++;
                }
            }

            return count;
        }
    }
}