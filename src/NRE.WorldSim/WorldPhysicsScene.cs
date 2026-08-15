using System.Numerics;
using System.Runtime.CompilerServices;
using BepuPhysics;
using BepuPhysics.Collidables;
using BepuPhysics.CollisionDetection;
using BepuPhysics.Constraints;
using BepuUtilities;
using BepuUtilities.Memory;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NRE.WorldSim;

public readonly record struct WorldPhysicsBox(
    string Kind,
    Vector3 Center,
    Vector3 Size,
    Quaternion Orientation);

public readonly record struct AvatarPhysicsContact(
    string Region,
    AvatarKinematicChain Chain,
    Vector3 BodyPosition,
    Vector3 BodyNormal,
    float ForceNewtons,
    float ImpulseNewtonSeconds,
    float PenetrationMeters,
    float TangentialSpeedMetersPerSecond,
    float ContactAreaSquareMillimeters,
    string InputSource);

public sealed record AvatarPhysicsResolution(
    Vector3 RootPosition,
    float HeadingDegrees,
    PhysicalArticulationFrame Articulation,
    float AllowedFraction,
    float RootProgressFraction,
    float HeadingProgressFraction,
    IReadOnlyList<AvatarPhysicsContact> Contacts,
    bool RootMotionConstrained,
    bool HeadingConstrained,
    IReadOnlyList<AvatarKinematicChain> ConstrainedChains)
{
    public bool Blocked => RootMotionConstrained || HeadingConstrained || ConstrainedChains.Count > 0;
}

/// <summary>
/// Collision-only physical world. Neural output and the musculoskeletal plant
/// propose motion; this scene returns the furthest nonpenetrating articulated
/// pose and physical contact measurements. It contains no movement policy.
/// </summary>
public sealed class WorldPhysicsScene : IDisposable
{
    private const float ContactSkinMeters = 0.0015f;
    private const float TerrainFaceThicknessMeters = 0.035f;
    private const float MinimumTimeStepSeconds = 0.001f;
    private const float MotionEpsilonMeters = 0.00005f;
    private const float ConstraintEpsilon = 0.0001f;
    private const int MaximumSlideIterations = 4;
    private const float SeparatingProbeFraction = 0.04f;
    private static readonly AvatarKinematicChain[] ResolutionOrder =
    [
        AvatarKinematicChain.Axial,
        AvatarKinematicChain.LeftLeg,
        AvatarKinematicChain.RightLeg,
        AvatarKinematicChain.LeftArm,
        AvatarKinematicChain.RightArm
    ];
    private readonly BufferPool bufferPool = new();
    private readonly Simulation simulation;
    private bool disposed;

    public WorldPhysicsScene(WorldTerrain terrain)
    {
        ArgumentNullException.ThrowIfNull(terrain);
        simulation = CreateSimulation(bufferPool);
        AddTerrainFaces(terrain);
        AddShelters(terrain);
        AddObstacles(terrain);
        AddWorldBoundary();
    }

    public WorldPhysicsScene(IEnumerable<WorldPhysicsBox> boxes)
    {
        ArgumentNullException.ThrowIfNull(boxes);
        simulation = CreateSimulation(bufferPool);
        foreach (var box in boxes)
        {
            AddBox(box);
        }
    }

    public AvatarPhysicsResolution ResolveAvatar(
        Vector3 previousRoot,
        float previousHeadingDegrees,
        PhysicalArticulationFrame previousArticulation,
        Vector3 proposedRoot,
        float proposedHeadingDegrees,
        PhysicalArticulationFrame proposedArticulation,
        float deltaSeconds)
    {
        ObjectDisposedException.ThrowIf(disposed, this);
        ArgumentNullException.ThrowIfNull(previousArticulation);
        ArgumentNullException.ThrowIfNull(proposedArticulation);

        var dt = MathF.Max(MinimumTimeStepSeconds, deltaSeconds);
        var contacts = new List<AvatarPhysicsContact>();
        var constrainedChains = new HashSet<AvatarKinematicChain>();
        var minimumFraction = 1f;
        var resolvedArticulation = CarryPhysicalMeasurements(previousArticulation, proposedArticulation);

        var rootMotion = ResolveRootTranslation(
            previousRoot,
            proposedRoot,
            previousHeadingDegrees,
            resolvedArticulation,
            proposedArticulation,
            dt);
        var resolvedRoot = rootMotion.RootPosition;
        minimumFraction = MathF.Min(minimumFraction, rootMotion.ProgressFraction);
        contacts.AddRange(rootMotion.Contacts);

        var headingSweep = CaptureSweep(
            resolvedRoot,
            previousHeadingDegrees,
            resolvedArticulation,
            resolvedRoot,
            proposedHeadingDegrees,
            resolvedArticulation,
            dt,
            static _ => true);
        var resolvedHeading = LerpDegrees(
            previousHeadingDegrees,
            proposedHeadingDegrees,
            headingSweep.AllowedFraction);
        var headingConstrained = headingSweep.HasConstraint;
        minimumFraction = MathF.Min(minimumFraction, headingSweep.AllowedFraction);
        contacts.AddRange(CreateContacts(
            headingSweep,
            resolvedRoot,
            resolvedHeading,
            proposedArticulation,
            dt));

        foreach (var chain in ResolutionOrder)
        {
            var target = AvatarColliderRig.RetargetChain(resolvedArticulation, proposedArticulation, chain);
            var chainSweep = CaptureSweep(
                resolvedRoot,
                resolvedHeading,
                resolvedArticulation,
                resolvedRoot,
                resolvedHeading,
                target,
                dt,
                collider => chain == AvatarKinematicChain.Axial || collider.Chain == chain);
            resolvedArticulation = AvatarColliderRig.InterpolateChain(
                resolvedArticulation,
                target,
                chainSweep.AllowedFraction,
                chain);
            minimumFraction = MathF.Min(minimumFraction, chainSweep.AllowedFraction);
            if (chainSweep.HasConstraint)
            {
                constrainedChains.Add(chain);
            }

            contacts.AddRange(CreateContacts(
                chainSweep,
                resolvedRoot,
                resolvedHeading,
                proposedArticulation,
                dt));
        }

        resolvedArticulation = CarryPhysicalMeasurements(resolvedArticulation, proposedArticulation);

        return new AvatarPhysicsResolution(
            resolvedRoot,
            resolvedHeading,
            resolvedArticulation,
            minimumFraction,
            rootMotion.ProgressFraction,
            headingSweep.AllowedFraction,
            CoalesceContacts(contacts),
            rootMotion.Constrained,
            headingConstrained,
            constrainedChains.OrderBy(static chain => chain).ToArray());
    }

    private RootMotionResolution ResolveRootTranslation(
        Vector3 previousRoot,
        Vector3 proposedRoot,
        float headingDegrees,
        PhysicalArticulationFrame articulation,
        PhysicalArticulationFrame forceFrame,
        float dt)
    {
        var desired = proposedRoot - previousRoot;
        if (desired.LengthSquared() <= MotionEpsilonMeters * MotionEpsilonMeters)
        {
            return new RootMotionResolution(previousRoot, 1f, false, []);
        }

        var current = previousRoot;
        var remaining = desired;
        var contacts = new List<AvatarPhysicsContact>();
        var constrained = false;
        for (var iteration = 0; iteration < MaximumSlideIterations; iteration++)
        {
            if (remaining.LengthSquared() <= MotionEpsilonMeters * MotionEpsilonMeters)
            {
                break;
            }

            var target = current + remaining;
            var sweep = CaptureSweep(
                current,
                headingDegrees,
                articulation,
                target,
                headingDegrees,
                articulation,
                dt,
                static _ => true);
            if (!sweep.HasConstraint)
            {
                current = target;
                break;
            }

            constrained = true;
            current += remaining * sweep.AllowedFraction;
            contacts.AddRange(CreateContacts(sweep, current, headingDegrees, forceFrame, dt));
            var residual = remaining * (1f - sweep.AllowedFraction);
            var projected = residual;
            foreach (var normal in sweep.ConstraintNormals)
            {
                var intoSurface = Vector3.Dot(projected, normal);
                if (intoSurface < 0f)
                {
                    projected -= normal * intoSurface;
                }
            }

            if (projected.LengthSquared() <= MotionEpsilonMeters * MotionEpsilonMeters ||
                Vector3.DistanceSquared(projected, residual) <= MotionEpsilonMeters * MotionEpsilonMeters)
            {
                break;
            }

            remaining = projected;
        }

        var progress = Math.Clamp(
            Vector3.Dot(current - previousRoot, desired) / MathF.Max(MotionEpsilonMeters, desired.LengthSquared()),
            0f,
            1f);
        constrained |= Vector3.DistanceSquared(current, proposedRoot) > ConstraintEpsilon * ConstraintEpsilon;
        return new RootMotionResolution(current, progress, constrained, CoalesceContacts(contacts));
    }

    private MotionSweep CaptureSweep(
        Vector3 previousRoot,
        float previousHeadingDegrees,
        PhysicalArticulationFrame previousArticulation,
        Vector3 proposedRoot,
        float proposedHeadingDegrees,
        PhysicalArticulationFrame proposedArticulation,
        float dt,
        Func<AvatarBodyCollider, bool> include)
    {
        var previousRig = AvatarColliderRig.Capture(previousArticulation);
        var proposedRig = AvatarColliderRig.Capture(proposedArticulation);
        if (previousRig.Count != proposedRig.Count)
        {
            throw new InvalidOperationException("Avatar collision rig changed topology during a physics step.");
        }

        var previousRootOrientation = HeadingOrientation(previousHeadingDegrees);
        var proposedRootOrientation = HeadingOrientation(proposedHeadingDegrees);
        var hits = new List<ColliderSweepHit>(previousRig.Count);
        var allowedFraction = 1f;
        for (var index = 0; index < previousRig.Count; index++)
        {
            var previous = previousRig[index];
            var proposed = proposedRig[index];
            if (!string.Equals(previous.Region, proposed.Region, StringComparison.Ordinal))
            {
                throw new InvalidOperationException("Avatar collision rig changed ordering during a physics step.");
            }

            if (!include(previous))
            {
                continue;
            }

            var previousPose = ToWorldPose(previous, previousRoot, previousRootOrientation);
            var proposedPose = ToWorldPose(proposed, proposedRoot, proposedRootOrientation);
            var linearVelocity = (proposedPose.Position - previousPose.Position) / dt;
            var angularVelocity = AngularVelocity(previousPose.Orientation, proposedPose.Orientation, dt);
            if (linearVelocity.LengthSquared() < 0.00000001f &&
                angularVelocity.LengthSquared() < 0.00000001f)
            {
                continue;
            }

            var velocity = new BodyVelocity(linearVelocity, angularVelocity);
            var handler = new ClosestSweepHitHandler();
            SweepWithSeparatingProbe(previous, previousPose, proposedPose, velocity, dt, ref handler);
            if (!handler.Hit)
            {
                continue;
            }

            var rawFraction = Math.Clamp(handler.Time / dt, 0f, 1f);
            var travel = linearVelocity.Length() * dt +
                         angularVelocity.Length() * BoundingRadius(previous) * dt;
            var skinFraction = ContactSkinMeters / MathF.Max(0.05f, travel);
            var safeFraction = Math.Clamp(rawFraction - skinFraction, 0f, 1f);
            allowedFraction = MathF.Min(allowedFraction, safeFraction);
            hits.Add(new ColliderSweepHit(
                previous,
                proposed,
                previousPose,
                proposedPose,
                linearVelocity,
                angularVelocity,
                handler.Time,
                rawFraction,
                handler.Location,
                OrientContactNormal(handler.Normal, linearVelocity)));
        }

        if (hits.Count == 0)
        {
            return MotionSweep.Empty;
        }

        var activeHits = hits
            .Where(hit => hit.RawFraction <= allowedFraction + 0.015f)
            .ToArray();
        var normals = activeHits
            .Select(static hit => hit.Normal)
            .Where(static normal => normal.LengthSquared() > 0.5f)
            .Aggregate(
                new List<Vector3>(),
                static (unique, normal) =>
                {
                    if (!unique.Any(existing => Vector3.Dot(existing, normal) > 0.97f))
                    {
                        unique.Add(normal);
                    }

                    return unique;
                });
        return new MotionSweep(allowedFraction, activeHits, normals);
    }

    private IReadOnlyList<AvatarPhysicsContact> CreateContacts(
        MotionSweep sweep,
        Vector3 resolvedRoot,
        float resolvedHeading,
        PhysicalArticulationFrame forceFrame,
        float dt)
    {
        if (!sweep.HasConstraint)
        {
            return [];
        }

        var inverseRootOrientation = Quaternion.Conjugate(HeadingOrientation(resolvedHeading));
        var contacts = new List<AvatarPhysicsContact>(sweep.Hits.Count);
        foreach (var hit in sweep.Hits)
        {
            var hitLocation = hit.Location == default ? hit.PreviousPose.Position : hit.Location;
            var localPosition = Vector3.Transform(hitLocation - resolvedRoot, inverseRootOrientation);
            var localNormal = Vector3.Normalize(Vector3.Transform(hit.Normal, inverseRootOrientation));
            var contactVelocity = hit.LinearVelocity + Vector3.Cross(
                hit.AngularVelocity,
                hitLocation - hit.PreviousPose.Position);
            var normalSpeed = MathF.Max(0f, -Vector3.Dot(contactVelocity, hit.Normal));
            var tangentialVelocity = contactVelocity - (Vector3.Dot(contactVelocity, hit.Normal) * hit.Normal);
            var muscleForce = ResolveMuscleEffort(forceFrame.Musculoskeletal, hit.Proposed.Chain);
            var impactForce = hit.Proposed.EffectiveMassKilograms * normalSpeed / dt;
            var force = Math.Clamp(8f + impactForce + (muscleForce * 0.42f), 0f, 5_000f);
            var sector = ContactNormalSector(localNormal);

            contacts.Add(new AvatarPhysicsContact(
                hit.Proposed.Region,
                hit.Proposed.Chain,
                localPosition,
                localNormal,
                force,
                force * dt,
                ContactSkinMeters * 0.20f,
                tangentialVelocity.Length(),
                hit.Proposed.ContactAreaSquareMillimeters,
                $"avatar_world_{hit.Proposed.Region}_contact_{sector}"));
        }

        return contacts;
    }

    private static IReadOnlyList<AvatarPhysicsContact> CoalesceContacts(
        IEnumerable<AvatarPhysicsContact> contacts) =>
        contacts
            .GroupBy(static contact => contact.InputSource, StringComparer.Ordinal)
            .Select(static group =>
            {
                var samples = group.ToArray();
                var strongest = samples.MaxBy(static contact => contact.ForceNewtons);
                var totalWeight = samples.Sum(static contact => MathF.Max(1f, contact.ForceNewtons));
                var position = samples.Aggregate(
                    Vector3.Zero,
                    (sum, contact) => sum + (contact.BodyPosition * MathF.Max(1f, contact.ForceNewtons))) /
                    totalWeight;
                var normal = samples.Aggregate(
                    Vector3.Zero,
                    (sum, contact) => sum + (contact.BodyNormal * MathF.Max(1f, contact.ForceNewtons)));
                normal = normal.LengthSquared() > 0.000001f ? Vector3.Normalize(normal) : strongest.BodyNormal;
                return strongest with
                {
                    BodyPosition = position,
                    BodyNormal = normal,
                    ForceNewtons = samples.Max(static contact => contact.ForceNewtons),
                    ImpulseNewtonSeconds = samples.Max(static contact => contact.ImpulseNewtonSeconds),
                    PenetrationMeters = samples.Max(static contact => contact.PenetrationMeters),
                    TangentialSpeedMetersPerSecond = samples.Max(
                        static contact => contact.TangentialSpeedMetersPerSecond),
                    ContactAreaSquareMillimeters = samples.Max(
                        static contact => contact.ContactAreaSquareMillimeters)
                };
            })
            .ToArray();

    private static PhysicalArticulationFrame CarryPhysicalMeasurements(
        PhysicalArticulationFrame geometry,
        PhysicalArticulationFrame measurements)
    {
        var geometryBody = geometry.Musculoskeletal ?? MusculoskeletalStateFrame.Neutral;
        var measurementBody = measurements.Musculoskeletal ?? MusculoskeletalStateFrame.Neutral;
        var geometryBalance = geometryBody.Balance ?? PhysicalBalanceStateFrame.Neutral;
        var measurementBalance = measurementBody.Balance ?? PhysicalBalanceStateFrame.Neutral;
        return geometry with
        {
            LeftFootLoadNewtons = measurements.LeftFootLoadNewtons,
            RightFootLoadNewtons = measurements.RightFootLoadNewtons,
            LeftHandLoadNewtons = measurements.LeftHandLoadNewtons,
            RightHandLoadNewtons = measurements.RightHandLoadNewtons,
            ManipulatorExtensionFraction = measurements.ManipulatorExtensionFraction,
            Musculoskeletal = geometryBody with
            {
                SupportFraction = measurementBody.SupportFraction,
                Muscles = measurementBody.Muscles,
                Balance = measurementBalance with
                {
                    FallPitchRadians = geometryBalance.FallPitchRadians,
                    FallRollRadians = geometryBalance.FallRollRadians,
                    FallPitchVelocityRadiansPerSecond = geometryBalance.FallPitchVelocityRadiansPerSecond,
                    FallRollVelocityRadiansPerSecond = geometryBalance.FallRollVelocityRadiansPerSecond,
                    Phase = geometryBalance.Phase
                }
            }
        };
    }

    private void SweepWithSeparatingProbe(
        AvatarBodyCollider collider,
        RigidPose previousPose,
        RigidPose proposedPose,
        BodyVelocity velocity,
        float maximumTime,
        ref ClosestSweepHitHandler handler)
    {
        Sweep(collider, previousPose, velocity, maximumTime, ref handler);
        if (!handler.Hit || handler.Time > 0.000001f)
        {
            return;
        }

        var probePose = new RigidPose(
            Vector3.Lerp(previousPose.Position, proposedPose.Position, SeparatingProbeFraction),
            Quaternion.Normalize(Quaternion.Slerp(
                previousPose.Orientation,
                proposedPose.Orientation,
                SeparatingProbeFraction)));
        var remainingTime = maximumTime * (1f - SeparatingProbeFraction);
        var probeHandler = new ClosestSweepHitHandler();
        Sweep(collider, probePose, velocity, remainingTime, ref probeHandler);
        if (!probeHandler.Hit)
        {
            handler = default;
            return;
        }

        if (probeHandler.Time > 0.000001f)
        {
            probeHandler.Time += maximumTime * SeparatingProbeFraction;
        }

        handler = probeHandler;
    }

    private static string ContactNormalSector(Vector3 normal)
    {
        var absolute = Vector3.Abs(normal);
        if (absolute.X >= absolute.Y && absolute.X >= absolute.Z)
        {
            return normal.X < 0f ? "x_neg" : "x_pos";
        }

        if (absolute.Y >= absolute.Z)
        {
            return normal.Y < 0f ? "y_neg" : "y_pos";
        }

        return normal.Z < 0f ? "z_neg" : "z_pos";
    }

    public void Dispose()
    {
        if (disposed)
        {
            return;
        }

        disposed = true;
        simulation.Dispose();
        bufferPool.Clear();
    }

    private static Simulation CreateSimulation(BufferPool pool) =>
        Simulation.Create(
            pool,
            new NarrowPhaseCallbacks(),
            new PoseIntegratorCallbacks(),
            new SolveDescription(1, 1));

    private void AddTerrainFaces(WorldTerrain terrain)
    {
        var half = (WorldTerrain.Size - 1) * 0.5f;
        for (var x = 0; x < WorldTerrain.Size; x++)
        {
            for (var z = 0; z < WorldTerrain.Size; z++)
            {
                var height = terrain.HeightAtCell(x, z) - 0.5f;
                if (x + 1 < WorldTerrain.Size)
                {
                    var adjacent = terrain.HeightAtCell(x + 1, z) - 0.5f;
                    AddTerrainFace(
                        new Vector3((x - half) + 0.5f, 0f, z - half),
                        new Vector3(TerrainFaceThicknessMeters, 0f, 1.002f),
                        height,
                        adjacent);
                }

                if (z + 1 < WorldTerrain.Size)
                {
                    var adjacent = terrain.HeightAtCell(x, z + 1) - 0.5f;
                    AddTerrainFace(
                        new Vector3(x - half, 0f, (z - half) + 0.5f),
                        new Vector3(1.002f, 0f, TerrainFaceThicknessMeters),
                        height,
                        adjacent);
                }
            }
        }
    }

    private void AddTerrainFace(Vector3 center, Vector3 size, float firstHeight, float secondHeight)
    {
        var lower = MathF.Min(firstHeight, secondHeight);
        var upper = MathF.Max(firstHeight, secondHeight);
        var height = upper - lower;
        if (height < 0.01f)
        {
            return;
        }

        center.Y = lower + (height * 0.5f);
        size.Y = height;
        AddBox(new WorldPhysicsBox("terrain_face", center, size, Quaternion.Identity));
    }

    private void AddShelters(WorldTerrain terrain)
    {
        for (var index = 0; index < terrain.ShelterSites.Count; index++)
        {
            var site = terrain.ShelterSites[index];
            var scale = (float)site.Scale;
            var origin = new Vector3(
                (float)site.X,
                (float)terrain.SurfaceAt(site.X, site.Z),
                (float)site.Z);
            AddShelterBox(origin, scale, "shelter_back", 0f, 1.2f, -3.8f, 8f, 2.4f, 0.32f);
            AddShelterBox(origin, scale, "shelter_left", -3.8f, 1.2f, 0f, 0.32f, 2.4f, 7.3f);
            AddShelterBox(origin, scale, "shelter_right", 3.8f, 1.2f, 0f, 0.32f, 2.4f, 7.3f);
            AddShelterBox(origin, scale, "shelter_front_left", -2.5f, 1.2f, 3.8f, 2.8f, 2.4f, 0.32f);
            AddShelterBox(origin, scale, "shelter_front_right", 2.5f, 1.2f, 3.8f, 2.8f, 2.4f, 0.32f);
            AddShelterBox(origin, scale, "shelter_lintel", 0f, 2.2f, 3.8f, 2.2f, 0.4f, 0.32f);
            AddShelterBox(origin, scale, "shelter_roof", 0f, 2.55f, 0f, 6.4f, 0.28f, 6.4f);
            if (index == 0)
            {
                var coreShape = simulation.Shapes.Add(new Sphere(0.72f));
                simulation.Statics.Add(new StaticDescription(origin + new Vector3(0f, 1.22f, 0f), coreShape));
            }
        }
    }

    private void AddShelterBox(
        Vector3 origin,
        float scale,
        string kind,
        float x,
        float y,
        float z,
        float width,
        float height,
        float depth) =>
        AddBox(new WorldPhysicsBox(
            kind,
            origin + (new Vector3(x, y, z) * scale),
            new Vector3(width, height, depth) * scale,
            Quaternion.Identity));

    private void AddObstacles(WorldTerrain terrain)
    {
        foreach (var obstacle in terrain.StaticObstacles)
        {
            var surface = (float)terrain.SurfaceAt(obstacle.X, obstacle.Z);
            if (string.Equals(obstacle.Kind, "tree", StringComparison.Ordinal))
            {
                var radius = (float)obstacle.Width * 0.5f;
                var length = (float)obstacle.Height;
                var shape = simulation.Shapes.Add(new Cylinder(radius, length));
                simulation.Statics.Add(new StaticDescription(
                    new Vector3((float)obstacle.X, surface + (length * 0.5f), (float)obstacle.Z),
                    Quaternion.Identity,
                    shape));
                continue;
            }

            var originalRadius = (float)obstacle.Width / 2.6f;
            var orientation = Quaternion.CreateFromYawPitchRoll(
                (float)obstacle.RotationY,
                (float)obstacle.RotationX,
                (float)obstacle.RotationZ);
            AddBox(new WorldPhysicsBox(
                obstacle.Kind,
                new Vector3(
                    (float)obstacle.X,
                    surface + (originalRadius * 0.55f),
                    (float)obstacle.Z),
                new Vector3((float)obstacle.Width, (float)obstacle.Height, (float)obstacle.Depth),
                orientation));
        }
    }

    private void AddWorldBoundary()
    {
        var half = WorldTerrain.Size * 0.5f;
        const float wallHeight = 40f;
        const float thickness = 0.2f;
        AddBox(new WorldPhysicsBox("world_west", new Vector3(-half, wallHeight * 0.5f, 0f),
            new Vector3(thickness, wallHeight, WorldTerrain.Size), Quaternion.Identity));
        AddBox(new WorldPhysicsBox("world_east", new Vector3(half, wallHeight * 0.5f, 0f),
            new Vector3(thickness, wallHeight, WorldTerrain.Size), Quaternion.Identity));
        AddBox(new WorldPhysicsBox("world_north", new Vector3(0f, wallHeight * 0.5f, -half),
            new Vector3(WorldTerrain.Size, wallHeight, thickness), Quaternion.Identity));
        AddBox(new WorldPhysicsBox("world_south", new Vector3(0f, wallHeight * 0.5f, half),
            new Vector3(WorldTerrain.Size, wallHeight, thickness), Quaternion.Identity));
    }

    private void AddBox(WorldPhysicsBox box)
    {
        if (box.Size.X <= 0f || box.Size.Y <= 0f || box.Size.Z <= 0f)
        {
            throw new ArgumentOutOfRangeException(nameof(box), "Physics boxes must have positive dimensions.");
        }

        var shape = simulation.Shapes.Add(new Box(box.Size.X, box.Size.Y, box.Size.Z));
        simulation.Statics.Add(new StaticDescription(
            box.Center,
            Quaternion.Normalize(box.Orientation),
            shape));
    }

    private void Sweep(
        AvatarBodyCollider collider,
        RigidPose pose,
        BodyVelocity velocity,
        float maximumTime,
        ref ClosestSweepHitHandler handler)
    {
        switch (collider.Shape)
        {
            case AvatarColliderShape.Sphere:
                var sphere = new Sphere(collider.Size.X);
                simulation.Sweep(in sphere, in pose, in velocity, maximumTime, bufferPool, ref handler);
                break;
            case AvatarColliderShape.Capsule:
                var capsule = new Capsule(collider.Size.X, collider.Size.Y);
                simulation.Sweep(in capsule, in pose, in velocity, maximumTime, bufferPool, ref handler);
                break;
            case AvatarColliderShape.Box:
                var box = new Box(collider.Size.X, collider.Size.Y, collider.Size.Z);
                simulation.Sweep(in box, in pose, in velocity, maximumTime, bufferPool, ref handler);
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(collider), collider.Shape, "Unknown avatar collider shape.");
        }
    }

    private static RigidPose ToWorldPose(
        AvatarBodyCollider collider,
        Vector3 root,
        Quaternion rootOrientation) =>
        new(
            root + Vector3.Transform(collider.Position, rootOrientation),
            Quaternion.Normalize(rootOrientation * collider.Orientation));

    private static Quaternion HeadingOrientation(float headingDegrees) =>
        Quaternion.CreateFromAxisAngle(Vector3.UnitY, headingDegrees * (MathF.PI / 180f));

    private static Vector3 AngularVelocity(Quaternion from, Quaternion to, float dt)
    {
        var delta = Quaternion.Normalize(to * Quaternion.Conjugate(from));
        if (delta.W < 0f)
        {
            delta = new Quaternion(-delta.X, -delta.Y, -delta.Z, -delta.W);
        }

        var w = Math.Clamp(delta.W, -1f, 1f);
        var angle = 2f * MathF.Acos(w);
        var denominator = MathF.Sqrt(MathF.Max(0f, 1f - (w * w)));
        if (angle < 0.00001f || denominator < 0.00001f)
        {
            return Vector3.Zero;
        }

        return new Vector3(delta.X, delta.Y, delta.Z) * (angle / denominator / dt);
    }

    private static Vector3 OrientContactNormal(Vector3 normal, Vector3 linearVelocity)
    {
        if (normal.LengthSquared() < 0.000001f)
        {
            return linearVelocity.LengthSquared() > 0.000001f
                ? -Vector3.Normalize(linearVelocity)
                : Vector3.UnitY;
        }

        normal = Vector3.Normalize(normal);
        return Vector3.Dot(normal, linearVelocity) > 0f ? -normal : normal;
    }

    private static float BoundingRadius(AvatarBodyCollider collider) => collider.Shape switch
    {
        AvatarColliderShape.Sphere => collider.Size.X,
        AvatarColliderShape.Capsule => collider.Size.X + (collider.Size.Y * 0.5f),
        AvatarColliderShape.Box => collider.Size.Length() * 0.5f,
        _ => 0.25f
    };

    private static float ResolveMuscleEffort(
        MusculoskeletalStateFrame? state,
        AvatarKinematicChain chain)
    {
        if (state?.Muscles is null || state.Muscles.Count == 0)
        {
            return 0f;
        }

        var side = chain switch
        {
            AvatarKinematicChain.LeftArm or AvatarKinematicChain.LeftLeg => "L",
            AvatarKinematicChain.RightArm or AvatarKinematicChain.RightLeg => "R",
            _ => "M"
        };
        var arm = chain is AvatarKinematicChain.LeftArm or AvatarKinematicChain.RightArm;
        var leg = chain is AvatarKinematicChain.LeftLeg or AvatarKinematicChain.RightLeg;
        var total = 0f;
        foreach (var muscle in state.Muscles)
        {
            var isArm = muscle.Name is "AnteriorDeltoid" or "LatissimusDorsi" or "BicepsBrachii" or "TricepsBrachii";
            var isLeg = muscle.Name is "Iliopsoas" or "GluteusMaximus" or "Hamstrings" or "Quadriceps" or
                "TibialisAnterior" or "GastrocnemiusSoleus";
            var included = chain == AvatarKinematicChain.Axial
                ? muscle.Side == "M" || muscle.Name is "Obliques"
                : string.Equals(muscle.Side, side, StringComparison.Ordinal) &&
                  ((arm && isArm) || (leg && isLeg));
            if (included)
            {
                total += MathF.Max(0f, muscle.ForceNewtons);
            }
        }

        return total;
    }

    private static float LerpDegrees(float from, float to, float fraction)
    {
        var delta = ((to - from + 540f) % 360f) - 180f;
        return (from + (delta * fraction) + 360f) % 360f;
    }

    private sealed record MotionSweep(
        float AllowedFraction,
        IReadOnlyList<ColliderSweepHit> Hits,
        IReadOnlyList<Vector3> ConstraintNormals)
    {
        public static MotionSweep Empty { get; } = new(1f, [], []);
        public bool HasConstraint => Hits.Count > 0 && AllowedFraction < 0.9999f;
    }

    private readonly record struct RootMotionResolution(
        Vector3 RootPosition,
        float ProgressFraction,
        bool Constrained,
        IReadOnlyList<AvatarPhysicsContact> Contacts);

    private readonly record struct ColliderSweepHit(
        AvatarBodyCollider Previous,
        AvatarBodyCollider Proposed,
        RigidPose PreviousPose,
        RigidPose ProposedPose,
        Vector3 LinearVelocity,
        Vector3 AngularVelocity,
        float Time,
        float RawFraction,
        Vector3 Location,
        Vector3 Normal);

    private struct ClosestSweepHitHandler : ISweepHitHandler
    {
        public bool Hit;
        public float Time;
        public Vector3 Location;
        public Vector3 Normal;

        public readonly bool AllowTest(CollidableReference collidable) => true;
        public readonly bool AllowTest(CollidableReference collidable, int childIndex) => true;

        public void OnHit(
            ref float maximumT,
            float t,
            in Vector3 hitLocation,
            in Vector3 hitNormal,
            CollidableReference collidable)
        {
            if (Hit && t >= Time)
            {
                return;
            }

            Hit = true;
            Time = t;
            Location = hitLocation;
            Normal = hitNormal;
            maximumT = t;
        }

        public void OnHitAtZeroT(ref float maximumT, CollidableReference collidable)
        {
            if (Hit && Time <= 0f)
            {
                return;
            }

            Hit = true;
            Time = 0f;
            Location = default;
            Normal = default;
            maximumT = 0f;
        }
    }

    private struct NarrowPhaseCallbacks : INarrowPhaseCallbacks
    {
        public void Initialize(Simulation simulation)
        {
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool AllowContactGeneration(
            int workerIndex,
            CollidableReference a,
            CollidableReference b,
            ref float speculativeMargin) => true;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool AllowContactGeneration(
            int workerIndex,
            CollidablePair pair,
            int childIndexA,
            int childIndexB) => true;

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool ConfigureContactManifold<TManifold>(
            int workerIndex,
            CollidablePair pair,
            ref TManifold manifold,
            out PairMaterialProperties pairMaterial)
            where TManifold : unmanaged, IContactManifold<TManifold>
        {
            pairMaterial = new PairMaterialProperties
            {
                FrictionCoefficient = 0.9f,
                MaximumRecoveryVelocity = 2f,
                SpringSettings = new SpringSettings(30f, 1f)
            };
            return true;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public readonly bool ConfigureContactManifold(
            int workerIndex,
            CollidablePair pair,
            int childIndexA,
            int childIndexB,
            ref ConvexContactManifold manifold) => true;

        public readonly void Dispose()
        {
        }
    }

    private struct PoseIntegratorCallbacks : IPoseIntegratorCallbacks
    {
        public readonly AngularIntegrationMode AngularIntegrationMode => AngularIntegrationMode.Nonconserving;
        public readonly bool AllowSubstepsForUnconstrainedBodies => false;
        public readonly bool IntegrateVelocityForKinematics => false;

        public void Initialize(Simulation simulation)
        {
        }

        public void PrepareForIntegration(float dt)
        {
        }

        public readonly void IntegrateVelocity(
            Vector<int> bodyIndices,
            Vector3Wide position,
            QuaternionWide orientation,
            BodyInertiaWide localInertia,
            Vector<int> integrationMask,
            int workerIndex,
            Vector<float> dt,
            ref BodyVelocityWide velocity)
        {
        }
    }
}
