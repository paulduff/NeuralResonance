namespace NRE.WorldSim;

public sealed class WorldTerrain
{
    public const int Size = 132;
    public const int SeaLevel = 3;
    public const double ShelterFoundationHalfExtent = 4.55;
    public const double ShelterEntranceHalfWidth = 1.75;
    public const double ShelterEntranceStart = 3.45;
    public const double ShelterEntranceEnd = 8.0;
    public const double ShelterGradeWidth = 2.5;
    private const int MinimumHeight = 1;
    private const int MaximumHeight = 18;
    private readonly short[,] heights = new short[Size, Size];
    private readonly List<WorldStaticObstacle> staticObstacles = [];
    private readonly List<WorldShelterSite> shelterSites = [];

    public WorldTerrain(int seed)
    {
        Seed = seed;
        Generate();
    }

    public int Seed { get; }
    public int ExplorableCellCount { get; private set; }
    public int StaticObstacleCount => staticObstacles.Count;
    public IReadOnlyList<WorldStaticObstacle> StaticObstacles => staticObstacles;
    public IReadOnlyList<WorldShelterSite> ShelterSites => shelterSites;

    public int HeightAtCell(int x, int z) => heights[Math.Clamp(x, 0, Size - 1), Math.Clamp(z, 0, Size - 1)];

    public double SurfaceAt(double worldX, double worldZ)
    {
        var half = (Size - 1) * 0.5;
        var gridX = Math.Clamp(worldX + half, 0.0, Size - 1.0);
        var gridZ = Math.Clamp(worldZ + half, 0.0, Size - 1.0);
        var x0 = (int)Math.Floor(gridX);
        var z0 = (int)Math.Floor(gridZ);
        var x1 = Math.Min(Size - 1, x0 + 1);
        var z1 = Math.Min(Size - 1, z0 + 1);
        var tx = SmoothStep(gridX - x0);
        var tz = SmoothStep(gridZ - z0);
        return Lerp(
            Lerp(heights[x0, z0], heights[x1, z0], tx),
            Lerp(heights[x0, z1], heights[x1, z1], tx),
            tz) - 0.5;
    }

    public bool IsWater(double worldX, double worldZ) => SurfaceAt(worldX, worldZ) + 0.5 < SeaLevel;

    public bool IsWalkable(double worldX, double worldZ) => IsInside(worldX, worldZ) && !IsWater(worldX, worldZ);

    public bool IsInside(double worldX, double worldZ)
    {
        var half = (Size - 1) * 0.5;
        return worldX >= -half && worldX <= half && worldZ >= -half && worldZ <= half;
    }

    public int CellKey(double worldX, double worldZ)
    {
        var half = (Size - 1) * 0.5;
        var x = Math.Clamp((int)Math.Round(worldX + half), 0, Size - 1);
        var z = Math.Clamp((int)Math.Round(worldZ + half), 0, Size - 1);
        return (x * Size) + z;
    }

    public bool CollidesWithStaticObstacle(double worldX, double worldZ, double radius)
    {
        foreach (var obstacle in staticObstacles)
        {
            var dx = worldX - obstacle.X;
            var dz = worldZ - obstacle.Z;
            var contactRadius = radius + obstacle.Radius;
            if ((dx * dx) + (dz * dz) <= contactRadius * contactRadius)
            {
                return true;
            }
        }
        return false;
    }

    public bool TryGetStaticObstacleContact(
        double worldX,
        double worldZ,
        double radius,
        out WorldObstacleContact contact)
    {
        foreach (var obstacle in staticObstacles)
        {
            var dx = worldX - obstacle.X;
            var dz = worldZ - obstacle.Z;
            var distance = Math.Sqrt((dx * dx) + (dz * dz));
            var contactRadius = radius + obstacle.Radius;
            if (distance > contactRadius)
            {
                continue;
            }

            var normalX = distance > 0.0001 ? dx / distance : 0.0;
            var normalZ = distance > 0.0001 ? dz / distance : -1.0;
            contact = new WorldObstacleContact(
                normalX,
                normalZ,
                Math.Max(0.0, contactRadius - distance));
            return true;
        }

        contact = default;
        return false;
    }

    public bool IsInsideShelterClearance(double worldX, double worldZ)
    {
        foreach (var site in shelterSites)
        {
            var localX = Math.Abs((worldX - site.X) / site.Scale);
            var localZ = (worldZ - site.Z) / site.Scale;
            var insideFoundation = localX <= ShelterFoundationHalfExtent &&
                                   Math.Abs(localZ) <= ShelterFoundationHalfExtent;
            var insideEntrance = localX <= ShelterEntranceHalfWidth &&
                                 localZ >= ShelterEntranceStart &&
                                 localZ <= ShelterEntranceEnd;
            if (insideFoundation || insideEntrance)
            {
                return true;
            }
        }

        return false;
    }

    private void Generate()
    {
        var center = (Size - 1) * 0.5;
        var maximumRadius = Size * 0.64;
        (double X, double Z, double Radius, double Gain)[] mountains =
        [
            (-center * 0.55, -center * 0.25, Size * 0.20, 5.6),
            (center * 0.42, center * 0.18, Size * 0.18, 4.8),
            (0, center * 0.46, Size * 0.16, 3.7)
        ];

        for (var x = 0; x < Size; x++)
        {
            for (var z = 0; z < Size; z++)
            {
                var wx = x - center;
                var wz = z - center;
                var radius = Math.Sqrt((wx * wx) + (wz * wz));
                var radialFalloff = Math.Clamp(radius / maximumRadius, 0.0, 1.0);
                var n1 = FractalNoise((wx * 0.075) + (Seed * 0.0013), (wz * 0.075) + (Seed * 0.0021), 4, 0.55);
                var n2 = FractalNoise((wx * 0.19) + (Seed * 0.0032), (wz * 0.19) + (Seed * 0.0019), 3, 0.45);
                var ridge = Math.Abs((n2 * 2.0) - 1.0);
                var sculpted = (n1 * 0.74) + ((1.0 - ridge) * 0.26) - (radialFalloff * 0.42);
                foreach (var mountain in mountains)
                {
                    var dx = wx - mountain.X;
                    var dz = wz - mountain.Z;
                    var distance = Math.Sqrt((dx * dx) + (dz * dz));
                    if (distance <= mountain.Radius)
                    {
                        var amount = 1.0 - (distance / mountain.Radius);
                        sculpted += (amount * amount) * (mountain.Gain / 10.0);
                    }
                }

                var valleyRadius = Size * 0.17;
                if (radius < valleyRadius)
                {
                    sculpted -= (1.0 - (radius / valleyRadius)) * 0.25;
                }

                var height = Math.Clamp(1 + (int)Math.Round(sculpted * 10.0), MinimumHeight, MaximumHeight);
                heights[x, z] = (short)height;
            }
        }

        GenerateShelterSites();
        PrepareShelterGround();
        ExplorableCellCount = CountExplorableCells();
        GenerateStaticObstacles();
    }

    private void GenerateShelterSites()
    {
        shelterSites.Clear();
        shelterSites.Add(new WorldShelterSite(0.0, 0.0, 1.0));
        var random = new Mulberry32(unchecked((uint)(Seed + 4127)));
        for (var index = 0; index < 11; index++)
        {
            var angle = (index / 11.0 * Math.PI * 2.0) + ((random.NextDouble() - 0.5) * 0.24);
            var radius = 18.0 + ((index % 3) * 10.0) + (random.NextDouble() * 4.0);
            shelterSites.Add(new WorldShelterSite(
                Math.Cos(angle) * radius,
                Math.Sin(angle) * radius,
                0.78));
        }
    }

    private void PrepareShelterGround()
    {
        var half = (Size - 1) * 0.5;
        foreach (var site in shelterSites)
        {
            var targetHeight = Math.Max(SeaLevel + 1, HeightAtWorld(site.X, site.Z));
            var gradeWidth = ShelterGradeWidth * site.Scale;
            for (var x = 0; x < Size; x++)
            {
                for (var z = 0; z < Size; z++)
                {
                    var localX = (x - half - site.X) / site.Scale;
                    var localZ = (z - half - site.Z) / site.Scale;
                    var foundationDistance = DistanceToRectangle(
                        localX,
                        localZ,
                        ShelterFoundationHalfExtent,
                        ShelterFoundationHalfExtent) * site.Scale;
                    var entranceCenter = (ShelterEntranceStart + ShelterEntranceEnd) * 0.5;
                    var entranceDistance = DistanceToRectangle(
                        localX,
                        localZ - entranceCenter,
                        ShelterEntranceHalfWidth,
                        (ShelterEntranceEnd - ShelterEntranceStart) * 0.5) * site.Scale;
                    var distance = Math.Min(foundationDistance, entranceDistance);
                    if (distance <= 0.0)
                    {
                        heights[x, z] = (short)targetHeight;
                    }
                    else if (distance < gradeWidth)
                    {
                        var blend = 1.0 - (distance / gradeWidth);
                        heights[x, z] = (short)Math.Clamp(
                            (int)Math.Round(Lerp(heights[x, z], targetHeight, blend), MidpointRounding.AwayFromZero),
                            MinimumHeight,
                            MaximumHeight);
                    }
                }
            }
        }
    }

    private int HeightAtWorld(double worldX, double worldZ)
    {
        var half = (Size - 1) * 0.5;
        var x = Math.Clamp((int)Math.Round(worldX + half), 0, Size - 1);
        var z = Math.Clamp((int)Math.Round(worldZ + half), 0, Size - 1);
        return heights[x, z];
    }

    private int CountExplorableCells()
    {
        var count = 0;
        foreach (var height in heights)
        {
            if (height >= SeaLevel)
            {
                count++;
            }
        }

        return count;
    }

    private static double DistanceToRectangle(
        double x,
        double z,
        double halfWidth,
        double halfDepth)
    {
        var dx = Math.Max(Math.Abs(x) - halfWidth, 0.0);
        var dz = Math.Max(Math.Abs(z) - halfDepth, 0.0);
        return Math.Sqrt((dx * dx) + (dz * dz));
    }

    private void GenerateStaticObstacles()
    {
        staticObstacles.Clear();
        var half = (Size - 1) * 0.5;
        var floraSeed = Seed + 991;
        for (var x = 2; x < Size - 2; x++)
        {
            for (var z = 2; z < Size - 2; z++)
            {
                if (heights[x, z] <= SeaLevel + 1)
                {
                    continue;
                }
                var worldX = x - half;
                var worldZ = z - half;
                if (IsInsideShelterClearance(worldX, worldZ))
                {
                    continue;
                }
                var placement = FractalNoise(
                    (x * 0.31) + (floraSeed * 0.013),
                    (z * 0.31) + (floraSeed * 0.017),
                    2,
                    0.5);
                if (placement >= 0.81 && Math.Sqrt(((x - half) * (x - half)) + ((z - half) * (z - half))) > 8.0)
                {
                    var size = 0.9 + (placement * 0.35);
                    staticObstacles.Add(new WorldStaticObstacle(
                        "tree",
                        worldX,
                        worldZ,
                        0.30 * size,
                        0.60 * size,
                        2.50 * size,
                        0.60 * size,
                        0.0,
                        0.0,
                        0.0));
                }
            }
        }

        var random = new Mulberry32(unchecked((uint)(Seed + 331)));
        var rocksAdded = 0;
        for (var attempt = 0; attempt < 200 && rocksAdded < 20; attempt++)
        {
            var x = -61.0 + (random.NextDouble() * 122.0);
            var z = -61.0 + (random.NextDouble() * 122.0);
            var radius = 0.45 + (random.NextDouble() * 0.8);
            var scaleY = radius * (0.65 + (random.NextDouble() * 0.5));
            var rotationX = random.NextDouble();
            var rotationY = random.NextDouble() * Math.PI;
            var rotationZ = random.NextDouble();
            if (IsInsideShelterClearance(x, z))
            {
                continue;
            }

            staticObstacles.Add(new WorldStaticObstacle(
                "rock",
                x,
                z,
                radius * 1.3,
                radius * 2.6,
                scaleY * 2.0,
                radius * 2.0,
                rotationX,
                rotationY,
                rotationZ));
            rocksAdded++;
        }
    }

    private static double FractalNoise(double x, double z, int octaves, double persistence)
    {
        var amplitude = 1.0;
        var frequency = 1.0;
        var total = 0.0;
        var maximum = 0.0;
        for (var index = 0; index < octaves; index++)
        {
            total += ValueNoise(x * frequency, z * frequency) * amplitude;
            maximum += amplitude;
            amplitude *= persistence;
            frequency *= 2.0;
        }

        return maximum <= 1e-9 ? 0.0 : total / maximum;
    }

    private static double ValueNoise(double x, double z)
    {
        var xi = (int)Math.Floor(x);
        var zi = (int)Math.Floor(z);
        var tx = x - xi;
        var tz = z - zi;
        var sx = SmoothStep(tx);
        var sz = SmoothStep(tz);
        return Lerp(
            Lerp(Hash01(xi, zi), Hash01(xi + 1, zi), sx),
            Lerp(Hash01(xi, zi + 1), Hash01(xi + 1, zi + 1), sx),
            sz);
    }

    private static double Hash01(int x, int z)
    {
        var value = unchecked((x * 374761393) + (z * 668265263));
        value = unchecked((value ^ (value >> 13)) * 1274126177);
        value ^= value >> 16;
        return (value & 0x7fffffff) / (double)0x7fffffff;
    }

    private static double SmoothStep(double value) => value * value * (3.0 - (2.0 * value));
    private static double Lerp(double left, double right, double amount) => left + ((right - left) * amount);

    private sealed class Mulberry32(uint state)
    {
        private uint state = state;

        public double NextDouble()
        {
            state = unchecked(state + 0x6D2B79F5u);
            var result = state;
            result = unchecked((result ^ (result >> 15)) * (result | 1u));
            result ^= unchecked(result + ((result ^ (result >> 7)) * (result | 61u)));
            return (result ^ (result >> 14)) / 4294967296.0;
        }
    }
}

public sealed record WorldShelterSite(double X, double Z, double Scale);

public sealed record WorldStaticObstacle(
    string Kind,
    double X,
    double Z,
    double Radius,
    double Width,
    double Height,
    double Depth,
    double RotationX,
    double RotationY,
    double RotationZ);

public readonly record struct WorldObstacleContact(
    double NormalX,
    double NormalZ,
    double PenetrationMeters);
