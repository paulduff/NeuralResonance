using System.Text.Json;
using System.Text.Json.Nodes;
using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace NeuralResonanceEngine.DNNE.Tests;

// Counter/contact fixtures, not physical solves or afferent neurons.
public sealed class PhysicalObservationTests
{
    private static readonly PhysicalSampleClock Clock = new(Guid.Parse("30cbca20-f89f-4e28-b38b-3243e5b11586"),
        "body-1", "avatar_unity_body", 0, (double).01f * 1000, 100, 500);
    private static readonly PhysicalContactSite[] Sites = { new("left-foot", "left-contact"), new("right-foot", "right-contact") };
    private static PhysicalObservationLayout Layout(PhysicalSampleClock? clock = null) => new(clock ?? Clock, "fixture-body-coordinates-v1", Sites);
    private static PhysicalObservationFrame Frame(long step = 0, long wall = 10_000) => new(
        new(step, wall, -.5f, 0, 0, 0, 0, 0, 6_000_000, .99f, 37, .98f, .75f, Clock.SourceId,
            PhysicalArticulationFrame.Neutral with { Musculoskeletal = MusculoskeletalStateFrame.Neutral with
            { Muscles = new[] { new PhysicalMuscleMeasurement("soleus", "L", .3f, 20, .9f, -.25f, .2f) } } }),
        Sites.Select((s, i) => new PhysicalContactObservation(s.SiteId,
            new(step, wall + i + 1, -.2f, .1f, .3f, -1, 0, 0, i == 0 ? 25 : 0,
                i == 0 ? .25f : 0, 0, 0, 8000, i == 0 ? 10 : 0, s.SourceId))).ToArray());
    private static T Wire<T>(T value) => JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(value))!;
    private static PhysicalObservationBatch Packet(long first = 0, int count = 1, long sequence = 1)
    {
        var layout = Layout();
        return new(PhysicalObservationProtocol.Version, Clock.SessionId, Clock.SourceId, layout.Fingerprint,
            sequence, first + count - 1, Enumerable.Range(0, count)
                .Select(i => PhysicalObservationProtocol.Capture(layout, first + i, Frame(first + i))).ToArray());
    }
    private static PhysicalObservationSample Rewrite(PhysicalObservationSample sample, Action<JsonObject> edit)
    {
        var node = JsonNode.Parse(sample.ObservationJson)!.AsObject(); edit(node);
        string json = node.ToJsonString();
        return sample with { ObservationJson = json, ObservationSha256 = PhysicalSampleProtocol.Digest(json) };
    }
    private static string State(AvatarPhysicalObservationReceiver r) => JsonSerializer.Serialize(new
    { r.PendingSamples, r.PendingObservationBytes, r.AcceptedSamples, r.ConsumedSamples,
        r.SampleCompleteThroughStep, r.ConsumedThroughStep, r.Faulted });
    private sealed class Body : IAvatarPhysicalObservationBody
    {
        public long CompletedPhysicsStep { get; set; }
        public double PhysicsStepMs { get; set; } = Clock.PhysicsStepMs;
        public int Advances, Captures;
        public Action? AdvanceHook, CaptureHook;
        public Func<PhysicalObservationFrame, PhysicalObservationFrame>? Change;
        public void AdvanceOneStep() { ++Advances; ++CompletedPhysicsStep; AdvanceHook?.Invoke(); }
        public PhysicalObservationFrame CaptureObservation()
        {
            ++Captures; CaptureHook?.Invoke(); var frame = Frame(CompletedPhysicsStep);
            return Change?.Invoke(frame) ?? frame;
        }
    }

    [Fact]
    public void DeclaredSitesAreFrozenAndOrderIndependent()
    {
        var sites = Sites.Reverse().ToArray(); var layout = new PhysicalObservationLayout(Clock, "fixture-body-coordinates-v1", sites);
        sites[0] = new("other", "other-source");
        Assert.Equal(Layout().Fingerprint, layout.Fingerprint); Assert.Equal(Sites, layout.ContactSites);
        Assert.Throws<ArgumentException>(() => new PhysicalObservationLayout(Clock, "", Sites));
        Assert.Throws<ArgumentException>(() => new PhysicalObservationLayout(Clock, "v1", Array.Empty<PhysicalContactSite>()));
        Assert.Throws<ArgumentException>(() => new PhysicalObservationLayout(Clock, "v1", new[] { Sites[0], Sites[0] }));
        Assert.Throws<ArgumentException>(() => new PhysicalObservationLayout(Clock, "v1", new[] { Sites[0], Sites[1] with { SourceId = Sites[0].SourceId } }));
        Assert.Throws<ArgumentException>(() => new PhysicalObservationLayout(Clock, "v1", new[] { Sites[0] with { SourceId = Clock.SourceId } }));
    }

    [Fact]
    public void LayoutAndClockChangesCannotAliasSamples()
    {
        var original = Layout(); var sample = PhysicalObservationProtocol.Capture(original, 0, Frame());
        var different = new[] { Layout(Clock with { SessionId = Guid.NewGuid() }), Layout(Clock with { BodyId = "body-2" }),
            Layout(Clock with { NeuralEpochMs = 600 }), Layout(Clock with { PhysicsStepMs = 1 }),
            new PhysicalObservationLayout(Clock, "changed-encoding", Sites),
            new PhysicalObservationLayout(Clock, original.ContactEncoding, new[] { Sites[0], Sites[1] with { SiteId = "right-hand" } }),
            new PhysicalObservationLayout(Clock, original.ContactEncoding, new[] { Sites[0], Sites[1] with { SourceId = "replacement" } }) };
        foreach (var layout in different)
        {
            Assert.NotEqual(original.Fingerprint, layout.Fingerprint);
            Assert.NotEqual(sample.SampleId, PhysicalObservationProtocol.Identity(layout, 0));
            Assert.Throws<ArgumentException>(() => PhysicalObservationProtocol.Decode(layout, sample));
        }
        Assert.NotEqual(PhysicalSampleProtocol.Identity(Clock, 0), sample.SampleId);
    }

    [Fact]
    public void CompleteFramePreservesSignsZeroLoadAndOriginalWallStamps()
    {
        var frame = Frame(); var sample = PhysicalObservationProtocol.Capture(Layout(), 0, frame);
        Assert.Equal(sample, PhysicalObservationProtocol.Capture(Layout(), 0, frame with { Contacts = frame.Contacts.Reverse().ToArray() }));
        var decoded = PhysicalObservationProtocol.Decode(Layout(), sample);
        Assert.Equal(frame.Body, decoded.Body with { Articulation = frame.Body.Articulation });
        Assert.Equal(frame.Contacts, decoded.Contacts);
        Assert.Equal(-.25f, Assert.Single(decoded.Body.Articulation!.Musculoskeletal!.Muscles).VelocityPerSecond);
        Assert.Equal(-1, decoded.Contacts[0].Observation.SurfaceNormalX);
        Assert.Equal(0, decoded.Contacts[1].Observation.ForceNewtons);
        var late = PhysicalObservationProtocol.Capture(Layout(), 0, Frame(wall: 9_000_000));
        Assert.Equal(sample.SampleId, late.SampleId); Assert.Equal(sample.NeuralTimeMs, late.NeuralTimeMs);
        Assert.NotEqual(sample.ObservationSha256, late.ObservationSha256);
    }

    [Fact]
    public void SubstrateInputsRoundTripExactlyAndValidityIsRecomputed()
    {
        var frame = Frame(); var layout = Layout();
        Assert.Null(PhysicalObservationProtocol.Decode(layout,
            PhysicalObservationProtocol.Capture(layout, 0, frame)).Body.NeuralSubstrates);
        foreach (var supply in new[] { new NeuralSubstrateSupply(.25, .5, .75, "measured-body"),
            new NeuralSubstrateSupply(.25, .5, 1.2, "out-of-range-observation") })
        {
            var sample = PhysicalObservationProtocol.Capture(layout, 0, frame with
            { Body = frame.Body with { NeuralSubstrates = supply } });
            var fields = JsonNode.Parse(sample.ObservationJson)!["body"]!["neuralSubstrates"]!.AsObject();
            Assert.Equal(new[] { "glucoseAvailability", "oxygenAvailability", "perfusionAvailability", "source" },
                fields.Select(p => p.Key).OrderBy(k => k, StringComparer.Ordinal));
            var decoded = PhysicalObservationProtocol.Decode(layout, sample).Body.NeuralSubstrates!;
            Assert.Equal(supply, decoded);
            Assert.Equal(supply.PerfusionAvailability <= 1, decoded.IsValid);
        }
    }

    [Fact]
    public void MissingSubstrateInputsAndForgedValidityRejectDespiteMatchingDigest()
    {
        var frame = Frame(); var layout = Layout();
        var sample = PhysicalObservationProtocol.Capture(layout, 0, frame with
        { Body = frame.Body with { NeuralSubstrates = new(.25, .5, .75, "measured-body") } });
        foreach (string member in new[] { "glucoseAvailability", "oxygenAvailability", "perfusionAvailability", "source" })
        {
            var missing = Rewrite(sample, n => n["body"]!["neuralSubstrates"]!.AsObject().Remove(member));
            Assert.Throws<JsonException>(() => PhysicalObservationProtocol.Decode(layout, missing));
        }
        var forged = Rewrite(sample, n => n["body"]!["neuralSubstrates"]!["isValid"] = true);
        Assert.Throws<JsonException>(() => PhysicalObservationProtocol.Decode(layout, forged));
        var missingSupply = Rewrite(sample, n => n["body"]!.AsObject().Remove("neuralSubstrates"));
        Assert.Throws<JsonException>(() => PhysicalObservationProtocol.Decode(layout, missingSupply));
    }

    [Fact]
    public void MissingExtraDuplicateStaleAndSubstitutedContactsRejectBeforeCapture()
    {
        var good = Frame(); var sender = new AvatarPhysicalObservationOutbox(Layout());
        var bad = new[] { good with { Contacts = Array.Empty<PhysicalContactObservation>() },
            good with { Contacts = good.Contacts.Concat(new[] { good.Contacts[0] }).ToArray() },
            good with { Contacts = new[] { good.Contacts[0], good.Contacts[0] } },
            good with { Contacts = new[] { good.Contacts[0], good.Contacts[1] with { SiteId = "unknown" } } },
            good with { Contacts = new[] { good.Contacts[0], good.Contacts[1] with { Observation = good.Contacts[1].Observation with { Sequence = 1 } } } },
            good with { Contacts = new[] { good.Contacts[0], good.Contacts[1] with { Observation = good.Contacts[1].Observation with { InputSource = "wrong" } } } },
            good with { Body = good.Body with { Sequence = 1 } } };
        foreach (var frame in bad)
        {
            Assert.Throws<ArgumentException>(() => sender.Capture(0, frame));
            Assert.Null(sender.CapturedThroughStep); Assert.Equal(0, sender.RetainedSamples); Assert.False(sender.Faulted);
        }
        sender.Capture(0, good); Assert.Equal(0, sender.CapturedThroughStep);
    }

    [Fact]
    public void MissingNumericMembersUnknownFieldsAndDuplicateKeysCannotBecomeZeroContact()
    {
        var sample = PhysicalObservationProtocol.Capture(Layout(), 0, Frame());
        var missing = Rewrite(sample, n => n["contacts"]![1]!["observation"]!.AsObject().Remove("forceNewtons"));
        var unknown = Rewrite(sample, n => n["contacts"]![1]!["observation"]!["futureForce"] = 1);
        var bodyMissing = Rewrite(sample, n => n["body"]!.AsObject().Remove("sequence"));
        foreach (var s in new[] { missing, unknown, bodyMissing })
            Assert.Throws<JsonException>(() => PhysicalObservationProtocol.Decode(Layout(), s));
        string duplicate = sample.ObservationJson.Replace("\"forceNewtons\":25", "\"forceNewtons\":25,\"forceNewtons\":0");
        Assert.NotEqual(sample.ObservationJson, duplicate);
        Assert.Throws<ArgumentException>(() => PhysicalObservationProtocol.Decode(Layout(), sample with
        { ObservationJson = duplicate, ObservationSha256 = PhysicalSampleProtocol.Digest(duplicate) }));
    }

    [Fact]
    public void NonfiniteMeasurementsAndOversizeNestedBodiesRejectWithoutStateChange()
    {
        var f = Frame(); var sender = new AvatarPhysicalObservationOutbox(Layout());
        var nanContact = f.Contacts[0] with { Observation = f.Contacts[0].Observation with { ForceNewtons = float.NaN } };
        Assert.Throws<ArgumentException>(() => sender.Capture(0, f with { Contacts = new[] { nanContact, f.Contacts[1] } }));
        var huge = f.Body.Articulation!.Musculoskeletal! with { Muscles = new[] {
            new PhysicalMuscleMeasurement(new string('x', PhysicalObservationProtocol.MaximumObservationBytes), "L", 0, 0, 1, 0, 0) } };
        Assert.Throws<ArgumentException>(() => sender.Capture(0, f with { Body = f.Body with
        { Articulation = f.Body.Articulation with { Musculoskeletal = huge } } }));
        Assert.Equal(0, sender.RetainedObservationBytes); sender.Capture(0, f);
    }

    [Fact]
    public void MutableInputAndPacketArraysCannotChangeRetainedMeasurements()
    {
        var f = Frame(); var contacts = f.Contacts.ToArray();
        var muscles = f.Body.Articulation!.Musculoskeletal!.Muscles.ToArray();
        var sender = new AvatarPhysicalObservationOutbox(Layout());
        var sample = sender.Capture(0, f with { Contacts = contacts, Body = f.Body with { Articulation = f.Body.Articulation with
        { Musculoskeletal = f.Body.Articulation.Musculoskeletal with { Muscles = muscles } } } });
        contacts[0] = contacts[1]; muscles[0] = muscles[0] with { VelocityPerSecond = 100 };
        var receiver = new AvatarPhysicalObservationReceiver(Layout()); var packet = sender.Publish(); var array = packet.Samples.ToArray();
        receiver.Accept(packet with { Samples = array }); array[0] = sample with { ObservationJson = "changed" };
        Assert.Equal(sample, Assert.Single(receiver.ConsumeThroughStep(0)));
        var decoded = PhysicalObservationProtocol.Decode(Layout(), sample);
        Assert.Equal(-.25f, Assert.Single(decoded.Body.Articulation!.Musculoskeletal!.Muscles).VelocityPerSecond);
        Assert.Equal(25, decoded.Contacts[0].Observation.ForceNewtons);
    }

    [Fact]
    public void BadFinalContactRejectsWholePacketBeforeAcceptance()
    {
        var packet = Packet(count: 2); var receiver = new AvatarPhysicalObservationReceiver(Layout()); var before = State(receiver);
        var invalid = Rewrite(packet.Samples[1], n => n["contacts"]!.AsArray().RemoveAt(1));
        Assert.Throws<ArgumentException>(() => receiver.Accept(packet with { Samples = new[] { packet.Samples[0], invalid } }));
        Assert.Equal(before, State(receiver)); receiver.Accept(packet); Assert.Equal(2, receiver.ConsumeThroughStep(1).Count);
    }

    [Fact]
    public void EmptyPacketsFalseCompletenessAndGridGapsCannotAssertSilence()
    {
        var r = new AvatarPhysicalObservationReceiver(Layout()); var p = Packet(); var before = State(r);
        Assert.Throws<ArgumentOutOfRangeException>(() => r.ConsumeThroughStep(0));
        Assert.Throws<ArgumentException>(() => r.Accept(p with { Samples = Array.Empty<PhysicalObservationSample>() }));
        Assert.Throws<ArgumentException>(() => r.Accept(p with { CompleteThroughStep = 1 }));
        Assert.Throws<ArgumentException>(() => r.Accept(Packet(first: 1)));
        Assert.Throws<ArgumentException>(() => r.Accept(p with { Sequence = 2 }));
        Assert.Equal(before, State(r)); r.Accept(p); Assert.Single(r.ConsumeThroughStep(0));
        Assert.Throws<ArgumentOutOfRangeException>(() => r.ConsumeThroughStep(1));
    }

    [Fact]
    public void WrongStreamClockSampleIdentityAndPayloadAreRejected()
    {
        var p = Packet(); var r = new AvatarPhysicalObservationReceiver(Layout()); var before = State(r);
        foreach (var bad in new[] { p with { Version = 2 }, p with { SessionId = Guid.NewGuid() },
            p with { SourceId = "other" }, p with { LayoutFingerprint = Clock.Fingerprint } })
            Assert.Throws<ArgumentException>(() => r.Accept(bad));
        foreach (var bad in new[] { p.Samples[0] with { SampleId = Guid.NewGuid() }, p.Samples[0] with { NeuralTimeMs = 999 },
            p.Samples[0] with { ObservationSha256 = "changed" } })
            Assert.Throws<ArgumentException>(() => r.Accept(p with { Samples = new[] { bad } }));
        Assert.Equal(before, State(r)); r.Accept(p);
    }

    [Fact]
    public void PacketSizesAndExactRetriesPreserveTheEntireBodyContactSequence()
    {
        PhysicalObservationSample[] Run(int size, bool retry)
        {
            var sender = new AvatarPhysicalObservationOutbox(Layout()); var r = new AvatarPhysicalObservationReceiver(Layout());
            var consumed = new List<PhysicalObservationSample>();
            for (int step = 0; step < 31; ++step)
            {
                sender.Capture(step, Frame(step, 100_000 + step * 97));
                if (sender.UnsentSamples < size && step != 30) continue;
                var p = sender.Publish(size); var receipt = r.Accept(Wire(p));
                consumed.AddRange(r.ConsumeThroughStep(p.CompleteThroughStep));
                if (retry) { Assert.Equal(receipt, r.Accept(Wire(Assert.Single(sender.Unacknowledged())))); Assert.Empty(r.ConsumeThroughStep(p.CompleteThroughStep)); }
                sender.Acknowledge(Wire(receipt)); sender.Acknowledge(Wire(receipt));
            }
            Assert.Equal(31, r.AcceptedSamples); Assert.Equal(31, r.ConsumedSamples);
            Assert.Equal(0, sender.RetainedObservationBytes); Assert.Equal(0, r.PendingObservationBytes);
            return consumed.ToArray();
        }
        Assert.Equal(Run(1, false), Run(3, true)); Assert.Equal(Run(1, false), Run(7, false));
    }

    [Fact]
    public void ReceiverCapacityRejectsAtomicallyAndRetainsOriginalForRetry()
    {
        var sender = new AvatarPhysicalObservationOutbox(Layout()); var r = new AvatarPhysicalObservationReceiver(Layout(), maximumSamples: 1);
        sender.Capture(0, Frame()); var first = sender.Publish(); sender.Capture(1, Frame(1)); var second = sender.Publish();
        r.Accept(first); var before = State(r);
        Assert.Throws<InvalidOperationException>(() => r.Accept(second)); Assert.Equal(before, State(r));
        Assert.Equal(2, sender.RetainedSamples); r.ConsumeThroughStep(0); var receipt = r.Accept(second);
        sender.Acknowledge(receipt); Assert.Equal(0, sender.RetainedSamples); Assert.Single(r.ConsumeThroughStep(1));
    }

    [Fact]
    public void ReceiverByteLimitRejectsWholeBatchAndThenWaitsForConsumption()
    {
        PhysicalObservationSample Large(long step)
        {
            var f = Frame(step); var muscle = f.Body.Articulation!.Musculoskeletal! with { Posture = new string('x', 600_000) };
            return PhysicalObservationProtocol.Capture(Layout(), step, f with { Body = f.Body with
            { Articulation = f.Body.Articulation with { Musculoskeletal = muscle } } });
        }
        var samples = new[] { Large(0), Large(1) };
        var r = new AvatarPhysicalObservationReceiver(Layout(), maximumObservationBytes: PhysicalObservationProtocol.MaximumObservationBytes);
        var before = State(r);
        Assert.Throws<InvalidOperationException>(() => r.Accept(Packet(count: 2) with { Samples = samples })); Assert.Equal(before, State(r));
        r.Accept(Packet() with { Samples = new[] { samples[0] } }); before = State(r);
        var next = Packet(first: 1, sequence: 2) with { Samples = new[] { samples[1] } };
        Assert.Throws<InvalidOperationException>(() => r.Accept(next)); Assert.Equal(before, State(r));
        r.ConsumeThroughStep(0); r.Accept(next); Assert.Single(r.ConsumeThroughStep(1)); Assert.Equal(0, r.PendingObservationBytes);
    }

    [Fact]
    public void PendingBatchLimitRetainsUnsentContactDataUntilReceipt()
    {
        var sender = new AvatarPhysicalObservationOutbox(Layout(), maximumBatches: 1);
        var r = new AvatarPhysicalObservationReceiver(Layout());
        sender.Capture(0, Frame()); var first = sender.Publish(); var next = sender.Capture(1, Frame(1));
        Assert.Throws<InvalidOperationException>(() => sender.Publish());
        Assert.Equal(1, sender.UnsentSamples); Assert.Equal(2, sender.RetainedSamples);
        sender.Acknowledge(r.Accept(first)); var second = sender.Publish(); Assert.Equal(2, second.Sequence);
        Assert.Equal(next, Assert.Single(second.Samples)); sender.Acknowledge(r.Accept(second));
        Assert.Equal(2, r.ConsumeThroughStep(1).Count); Assert.Equal(0, sender.RetainedSamples);
    }

    [Fact]
    public void ChangedContactCannotBeAcknowledgedAsAnExactReplay()
    {
        var sender = new AvatarPhysicalObservationOutbox(Layout()); var r = new AvatarPhysicalObservationReceiver(Layout());
        sender.Capture(0, Frame()); var packet = sender.Publish(); var receipt = r.Accept(packet);
        var altered = Rewrite(packet.Samples[0], n => n["contacts"]![0]!["observation"]!["forceNewtons"] = 30);
        var conflict = packet with { Samples = new[] { altered } }; var before = State(r);
        Assert.Throws<ArgumentException>(() => r.Accept(conflict)); Assert.Equal(before, State(r));
        Assert.Throws<ArgumentException>(() => sender.Acknowledge(PhysicalObservationProtocol.Receipt(conflict)));
        Assert.Equal(1, sender.RetainedSamples); sender.Acknowledge(receipt); Assert.Equal(0, sender.RetainedSamples);
    }

    [Fact]
    public void ExpiredReceiptsDoNotResetSessionOrResurrectConsumedSamples()
    {
        var sender = new AvatarPhysicalObservationOutbox(Layout(), retainedReceipts: 1);
        var r = new AvatarPhysicalObservationReceiver(Layout(), retainedBatches: 1);
        sender.Capture(0, Frame()); var first = sender.Publish(); var old = r.Accept(first); r.ConsumeThroughStep(0); sender.Acknowledge(old);
        sender.Capture(1, Frame(1)); var current = r.Accept(sender.Publish()); r.ConsumeThroughStep(1); sender.Acknowledge(current);
        Assert.Throws<ArgumentException>(() => r.Accept(first)); Assert.Throws<ArgumentException>(() => sender.Acknowledge(old));
        Assert.Equal(2, r.ConsumedSamples); Assert.Equal(1, r.SampleCompleteThroughStep);
    }

    [Fact]
    public void ProducerStartsPausedAndRequiresCompleteInitialCapture()
    {
        var body = new Body(); var p = new AvatarPhysicalObservationProducer(Layout(), body);
        Assert.Equal(PhysicalSampleAdvance.Paused, p.TryAdvance()); Assert.Throws<InvalidOperationException>(p.Resume);
        Assert.Equal(0, body.Advances); Assert.Equal(0, body.Captures); p.CaptureInitial();
        Assert.False(p.State.Running); Assert.Throws<InvalidOperationException>(() => p.CaptureInitial());
        p.Resume(); Assert.Equal(PhysicalSampleAdvance.Advanced, p.TryAdvance()); p.Pause();
        var before = p.State; Assert.Equal(PhysicalSampleAdvance.Paused, p.TryAdvance()); Assert.Equal(before, p.State);
    }

    [Fact]
    public void FullSourceHoldsPhysicsUntilBothBodyAndContactReceiptMatches()
    {
        var body = new Body(); var p = new AvatarPhysicalObservationProducer(Layout(), body, maximumSamples: 2);
        p.CaptureInitial(); p.Resume(); p.TryAdvance(); var before = p.State;
        for (int i = 0; i < 10; ++i) Assert.Equal(PhysicalSampleAdvance.Backpressured, p.TryAdvance());
        Assert.Equal(before, p.State); Assert.Equal(1, body.Advances); Assert.Equal(2, body.Captures);
        var packet = p.Publish(); Assert.Equal(PhysicalSampleAdvance.Backpressured, p.TryAdvance());
        var r = new AvatarPhysicalObservationReceiver(Layout()); var receipt = r.Accept(packet);
        Assert.Throws<ArgumentException>(() => p.Acknowledge(receipt with { BatchSha256 = "changed" }));
        Assert.Equal(PhysicalSampleAdvance.Backpressured, p.TryAdvance()); p.Acknowledge(receipt);
        Assert.Equal(PhysicalSampleAdvance.Advanced, p.TryAdvance()); Assert.Equal(2, body.Advances);
    }

    [Fact]
    public void CombinedByteCapacityIsReservedBeforeAnyMovement()
    {
        var body = new Body(); var p = new AvatarPhysicalObservationProducer(Layout(), body,
            maximumObservationBytes: PhysicalObservationProtocol.MaximumObservationBytes);
        p.CaptureInitial(); p.Resume(); Assert.Equal(PhysicalSampleAdvance.Backpressured, p.TryAdvance());
        Assert.Equal(0, body.Advances); var r = new AvatarPhysicalObservationReceiver(Layout());
        p.Acknowledge(r.Accept(p.Publish())); Assert.Equal(PhysicalSampleAdvance.Advanced, p.TryAdvance());
    }

    [Fact]
    public void MissingContactAfterMovementFaultsWithoutPublishingPartialStep()
    {
        var body = new Body(); var p = new AvatarPhysicalObservationProducer(Layout(), body); p.CaptureInitial(); p.Resume();
        body.Change = f => f with { Contacts = f.Contacts.Take(1).ToArray() };
        Assert.Throws<ArgumentException>(() => p.TryAdvance()); Assert.True(p.State.Faulted);
        Assert.Equal(1, body.Advances); Assert.Equal(0, p.State.CapturedThroughStep); Assert.Equal(1, p.State.ObservedPhysicsStep);
        body.Change = null; Assert.Throws<InvalidOperationException>(() => p.TryAdvance()); Assert.Equal(1, body.Advances);
        var r = new AvatarPhysicalObservationReceiver(Layout()); p.Acknowledge(r.Accept(p.Publish()));
        Assert.Single(r.ConsumeThroughStep(0)); Assert.Throws<ArgumentOutOfRangeException>(() => r.ConsumeThroughStep(1));
    }

    [Fact]
    public void CounterChangesDuringCombinedCaptureCannotHideAnUnobservedStep()
    {
        var body = new Body(); var p = new AvatarPhysicalObservationProducer(Layout(), body); p.CaptureInitial(); p.Resume();
        body.CaptureHook = () => ++body.CompletedPhysicsStep;
        Assert.Throws<InvalidOperationException>(() => p.TryAdvance()); Assert.True(p.State.Faulted);
        Assert.Equal(0, p.State.CapturedThroughStep); Assert.Equal(2, p.State.ObservedPhysicsStep);
    }

    [Fact]
    public void ReentryAndOtherThreadCannotAdvanceOrPublishTheOwnedBody()
    {
        var body = new Body(); var p = new AvatarPhysicalObservationProducer(Layout(), body); p.CaptureInitial(); p.Resume();
        Exception? error = null; var thread = new Thread(() => error = Record.Exception(() => p.Publish())); thread.Start(); thread.Join();
        Assert.IsType<InvalidOperationException>(error); Assert.False(p.State.Faulted);
        body.AdvanceHook = () => p.TryAdvance(); Assert.Throws<InvalidOperationException>(() => p.TryAdvance());
        Assert.True(p.State.Faulted); Assert.Equal(1, body.Advances); Assert.Equal(1, body.Captures);
    }

    [Fact]
    public void UnownedStepAndChangedDurationAreRejectedBeforeAnotherAdvance()
    {
        foreach (bool duration in new[] { false, true })
        {
            var body = new Body(); var p = new AvatarPhysicalObservationProducer(Layout(), body); p.CaptureInitial(); p.Resume();
            if (duration) body.PhysicsStepMs *= 2; else ++body.CompletedPhysicsStep;
            Assert.Throws<InvalidOperationException>(() => p.TryAdvance()); Assert.True(p.State.Faulted); Assert.Equal(0, body.Advances);
        }
    }

    [Fact]
    public void NonzeroEpochAndExactStepExhaustionPreserveCausalBoundary()
    {
        var clock = Clock with { InitialPhysicsStep = PhysicalSampleClock.MaximumExactStep - 1 };
        var body = new Body { CompletedPhysicsStep = clock.InitialPhysicsStep };
        var p = new AvatarPhysicalObservationProducer(Layout(clock), body);
        var first = p.CaptureInitial(); Assert.Equal(clock.NeuralEpochMs, first.NeuralTimeMs); p.Resume();
        Assert.Equal(PhysicalSampleAdvance.Advanced, p.TryAdvance());
        Assert.Throws<ArgumentOutOfRangeException>(() => p.TryAdvance()); Assert.Equal(1, body.Advances);
        Assert.True(p.State.Faulted); Assert.Equal(PhysicalSampleClock.MaximumExactStep, p.State.CapturedThroughStep);
    }

    [Fact]
    public void UnityAdapterRequiresAllDeclaredLinksIncludingZeroLoadAndOriginalSequences()
    {
        var layout = UnityPhysicalSampleProbe.UnityObservationEncoding.CreateLayout(Clock);
        Assert.Equal(15, layout.ContactSites.Count);
        var contact = Frame().Contacts[1].Observation;
        var json = layout.ContactSites.Select(s => JsonSerializer.Serialize(contact with { InputSource = s.SourceId },
            new JsonSerializerOptions(JsonSerializerDefaults.Web))).Reverse().ToArray();
        var frame = UnityPhysicalSampleProbe.UnityObservationEncoding.Capture(Frame().Body, json);
        var sample = PhysicalObservationProtocol.Capture(layout, 0, frame);
        Assert.Equal(15, PhysicalObservationProtocol.Decode(layout, sample).Contacts.Count);
        Assert.Throws<ArgumentException>(() => UnityPhysicalSampleProbe.UnityObservationEncoding.Capture(Frame().Body, json[..^1]));
        json[0] = json[1]; // An existing source cannot stand in for a missing link.
        var duplicated = UnityPhysicalSampleProbe.UnityObservationEncoding.Capture(Frame().Body, json);
        Assert.Throws<ArgumentException>(() => PhysicalObservationProtocol.Capture(layout, 0, duplicated));
    }

    [Fact]
    public void UnityAdapterRejectsMissingContactNumbersBeforeDefaultValuesCanHideThem()
    {
        var layout = UnityPhysicalSampleProbe.UnityObservationEncoding.CreateLayout(Clock);
        var json = layout.ContactSites.Select(s => JsonSerializer.Serialize(Frame().Contacts[0].Observation with { InputSource = s.SourceId },
            new JsonSerializerOptions(JsonSerializerDefaults.Web))).ToArray();
        var node = JsonNode.Parse(json[0])!; node.AsObject().Remove("forceNewtons"); json[0] = node.ToJsonString();
        Assert.Throws<JsonException>(() => UnityPhysicalSampleProbe.UnityObservationEncoding.Capture(Frame().Body, json));
        node["forceNewtons"] = 25; json[0] = node.ToJsonString().Replace("\"forceNewtons\":25", "\"forceNewtons\":25,\"forceNewtons\":0");
        Assert.Throws<ArgumentException>(() => UnityPhysicalSampleProbe.UnityObservationEncoding.Capture(Frame().Body, json));
    }
}
