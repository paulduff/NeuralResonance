using System.Numerics;

namespace NRE.Core.Engine;

/// <summary>
/// Very lightweight DBSCAN-ish clustering in voxel space.
/// Intended for "thought object" extraction from resonant voxels.
/// </summary>
public static class ThoughtClusterer
{
    public sealed record Cluster(int Id, List<ResonanceDetector.ResonantVoxel> Voxels)
    {
        public int Size => Voxels.Count;

        public Vector3 Centroid
        {
            get
            {
                if (Voxels.Count == 0) return Vector3.Zero;
                Vector3 s = Vector3.Zero;
                for (int i = 0; i < Voxels.Count; i++) s += Voxels[i].Pos;
                return s / Voxels.Count;
            }
        }

        public float MeanDensity01
        {
            get
            {
                if (Voxels.Count == 0) return 0f;
                float s = 0f;
                for (int i = 0; i < Voxels.Count; i++) s += Voxels[i].Density01;
                return s / Voxels.Count;
            }
        }
    }

    public static Cluster[] ClusterVoxels(
        ResonanceDetector.ResonantVoxel[] voxels,
        float radius = 2.2f,
        int minPts = 5,
        int maxClusters = 12)
    {
        if (voxels.Length == 0) return Array.Empty<Cluster>();

        // Index by integer voxel coord for O(1) neighbor membership test
        var map = new Dictionary<(int x, int y, int z), int>(voxels.Length);
        for (int i = 0; i < voxels.Length; i++)
        {
            var p = voxels[i].Pos;
            map[((int)p.X, (int)p.Y, (int)p.Z)] = i;
        }

        var visited = new bool[voxels.Length];
        var clusters = new List<Cluster>(16);

        float r2 = radius * radius;

        int id = 0;

        for (int i = 0; i < voxels.Length; i++)
        {
            if (visited[i]) continue;
            visited[i] = true;

            var neigh = GetNeighbors(voxels, map, i, r2);
            if (neigh.Count < minPts) continue;

            var clusterVox = new List<ResonanceDetector.ResonantVoxel>(neigh.Count + 8);
            ExpandCluster(voxels, map, visited, i, neigh, clusterVox, r2, minPts);

            clusters.Add(new Cluster(id++, clusterVox));
            if (clusters.Count >= maxClusters) break;
        }

        // Sort by size desc (most "thought-like" first)
        clusters.Sort((a, b) => b.Size.CompareTo(a.Size));
        return clusters.ToArray();
    }

    private static void ExpandCluster(
        ResonanceDetector.ResonantVoxel[] voxels,
        Dictionary<(int x, int y, int z), int> map,
        bool[] visited,
        int seedIdx,
        List<int> seedNeighbors,
        List<ResonanceDetector.ResonantVoxel> outVox,
        float r2,
        int minPts)
    {
        // Use HashSet to track which indices are already in cluster
        var inCluster = new HashSet<int> { seedIdx };
        outVox.Add(voxels[seedIdx]);

        var queue = new Queue<int>(seedNeighbors);
        while (queue.Count > 0)
        {
            int j = queue.Dequeue();
            if (!visited[j])
            {
                visited[j] = true;
                var neigh2 = GetNeighbors(voxels, map, j, r2);
                if (neigh2.Count >= minPts)
                {
                    for (int k = 0; k < neigh2.Count; k++)
                        queue.Enqueue(neigh2[k]);
                }
            }

            // Only add if not already in cluster
            if (inCluster.Add(j))
                outVox.Add(voxels[j]);
        }
    }

    private static List<int> GetNeighbors(
        ResonanceDetector.ResonantVoxel[] voxels,
        Dictionary<(int x, int y, int z), int> map,
        int idx,
        float r2)
    {
        var p = voxels[idx].Pos;
        int x0 = (int)p.X;
        int y0 = (int)p.Y;
        int z0 = (int)p.Z;

        // Integer neighborhood bounds (radius ceil)
        int r = (int)MathF.Ceiling(MathF.Sqrt(r2));

        var list = new List<int>(32);

        for (int z = z0 - r; z <= z0 + r; z++)
        for (int y = y0 - r; y <= y0 + r; y++)
        for (int x = x0 - r; x <= x0 + r; x++)
        {
            if (!map.TryGetValue((x, y, z), out int j)) continue;
            var q = voxels[j].Pos;
            float dx = q.X - p.X;
            float dy = q.Y - p.Y;
            float dz = q.Z - p.Z;
            float d2 = dx * dx + dy * dy + dz * dz;
            if (d2 <= r2 && j != idx) list.Add(j);
        }

        return list;
    }
}
