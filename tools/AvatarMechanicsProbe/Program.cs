using System.Numerics;
using System.Text.Json;
using NRE.SimAvatar;
using NRE.WorldSim;

// Offline physical qualification. Tonic input is a test stimulus and is never
// sent to DNNE or a running Avatar. Completion and gait quality are separate.
if (args.Length != 1)
{
    Console.Error.WriteLine("Usage: AvatarMechanicsProbe <output-json>");
    return 1;
}
var results = new List<object>();
foreach (var physicalFloor in new[] { false, true })
{
    foreach (var dt in new[] { 0.020, 0.025, 0.050 })
    {
        using var scene = new WorldPhysicsScene(physicalFloor
            ? [new WorldPhysicsBox("floor", new Vector3(0f, -0.10f, 0f),
                new Vector3(100f, 0.20f, 100f), Quaternion.Identity)]
            : []);
        var body = new AvatarArticulatedBody();
        var root = new Vector3(0f, 0.03f, 0f);
        var speed = 0.0;
        var blocked = false;
        var leftSwing = false;
        var rightSwing = false;
        var peakLeftClearance = 0f;
        var peakRightClearance = 0f;
        var leftConstraints = 0;
        var rightConstraints = 0;
        var fallingSamples = 0;
        var peakSupportLoad = 0.0;
        var physiology = new AvatarPhysiologyState(8_000_000.0, 1.0, 1.0);
        var contactDurations = new Dictionary<string, double>(StringComparer.Ordinal);
        var steps = (int)Math.Round(7.5 / dt);
        for (var step = 0; step < steps; step++)
        {
            var previous = body.CaptureFrame();
            // The live host supplies the drive-derived requested speed to this
            // parameter, historically named achievedForwardSpeed.
            var mechanical = body.Advance(dt, 0.9, 0.9, 1.4, 0.0, 0.0, true, blocked);
            var planar = AvatarPlanarDynamics.Advance(new AvatarPlanarMotionState(speed, 0.0),
                mechanical.ForwardSpeedMetersPerSecond, 0.0, body.CurrentPosture, true, dt);
            var proposed = body.CaptureFrame();
            var resolution = scene.ResolveAvatar(root, 0f, previous,
                root + new Vector3(0f, 0f, (float)(planar.ForwardVelocityMetersPerSecond * dt)),
                0f, proposed, (float)dt);
            speed = (resolution.RootPosition.Z - root.Z) / dt;
            root = resolution.RootPosition;
            blocked = resolution.RootMotionConstrained;
            leftConstraints += resolution.ConstrainedChains.Contains(AvatarKinematicChain.LeftLeg) ? 1 : 0;
            rightConstraints += resolution.ConstrainedChains.Contains(AvatarKinematicChain.RightLeg) ? 1 : 0;
            body.ReconcileResolvedFrame(previous, proposed, resolution.Articulation, dt);
            var active = new HashSet<string>(StringComparer.Ordinal);
            foreach (var contact in resolution.Contacts)
            {
                body.ApplyExternalContact(new AvatarExternalBodyContact(contact.Region,
                    contact.BodyPosition, contact.BodyNormal, contact.ForceNewtons,
                    contact.ImpulseNewtonSeconds, contact.ContactAreaSquareMillimeters));
                active.Add(contact.InputSource);
                var duration = contactDurations.GetValueOrDefault(contact.InputSource) + dt;
                contactDurations[contact.InputSource] = duration;
                physiology = AvatarWorldDynamics.ApplyPhysicalContact(physiology,
                    new AvatarPhysicalContactExposure(contact.Region, contact.ForceNewtons,
                        contact.ImpulseNewtonSeconds, contact.ContactAreaSquareMillimeters,
                        duration, dt, contact.ImpactImpulseNewtonSeconds)).State;
            }
            foreach (var source in contactDurations.Keys.Where(source => !active.Contains(source)).ToArray())
                contactDurations.Remove(source);
            var frame = body.CaptureFrame();
            var feet = AvatarColliderRig.CaptureResolved(frame);
            var left = feet.Single(collider => collider.Region == "left_foot");
            var right = feet.Single(collider => collider.Region == "right_foot");
            var leftClearance = AvatarColliderRig.LowestSurfaceY(left) - AvatarColliderRig.LowestSurfaceY(right);
            peakLeftClearance = Math.Max(peakLeftClearance, leftClearance);
            peakRightClearance = Math.Max(peakRightClearance, -leftClearance);
            leftSwing |= leftClearance > 0.015f && frame.LeftFootLoadNewtons < frame.RightFootLoadNewtons;
            rightSwing |= leftClearance < -0.015f && frame.RightFootLoadNewtons < frame.LeftFootLoadNewtons;
            fallingSamples += frame.Musculoskeletal?.Balance?.Phase is "falling" or "fallen" ? 1 : 0;
            var support = body.CaptureGroundContacts();
            peakSupportLoad = Math.Max(peakSupportLoad, support.Sum(contact => contact.LoadNewtons));
            foreach (var contact in support)
            {
                physiology = AvatarWorldDynamics.ApplyPhysicalContact(physiology,
                    new AvatarPhysicalContactExposure(contact.Region, contact.LoadNewtons,
                        contact.LoadNewtons * 0.05, contact.AreaSquareMillimeters, dt, dt,
                        ImpactImpulseNewtonSeconds: 0.0)).State;
            }
        }
        var qualified = leftSwing && rightSwing && root.Z > 0.25f && fallingSamples == 0 &&
            physiology.TissueIntegrityFraction == 1.0;
        var result = new { PhysicalFloor = physicalFloor, StepSeconds = dt, SimulatedSeconds = steps * dt,
            Qualified = qualified, LeftSwing = leftSwing, RightSwing = rightSwing,
            DistanceMeters = root.Z, PeakLeftClearanceMeters = peakLeftClearance,
            PeakRightClearanceMeters = peakRightClearance, LeftConstraintSamples = leftConstraints,
            RightConstraintSamples = rightConstraints, FallingSamples = fallingSamples,
            PeakSupportLoadNewtons = peakSupportLoad, TissueIntegrity = physiology.TissueIntegrityFraction };
        results.Add(result);
        Console.WriteLine(JsonSerializer.Serialize(result));
    }
}
var output = Path.GetFullPath(args[0]);
Directory.CreateDirectory(Path.GetDirectoryName(output)!);
File.WriteAllText(output, JsonSerializer.Serialize(new { CompletedUtc = DateTimeOffset.UtcNow,
    Purpose = "Offline body qualification; no neuronal learning or live control", Results = results },
    new JsonSerializerOptions { WriteIndented = true }));
Console.WriteLine($"All comparisons completed. Gait qualification is recorded separately: {output}");
return 0;
