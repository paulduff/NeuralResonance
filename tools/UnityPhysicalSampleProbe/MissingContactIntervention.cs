using NeuralResonanceEngine.Shared.Contracts;
using NRE.SimAvatar;

namespace UnityPhysicalSampleProbe;

public sealed record MissingContactEvidence(long PhysicsStep, string DroppedSiteId,
    string OriginalReplyFile, PhysicalObservationFrame DeliveredToProducer);

// Explicit isolated adapter intervention. Unity's original complete reply stays
// untouched; the producer sees one incomplete endpoint after actual movement.
internal sealed class MissingContactIntervention(UnityPhysicalSampleBody body, string output) : IAvatarPhysicalObservationBody
{
    public long? DropContactAtStep { get; set; }
    public string? DroppedSiteId { get; private set; }
    public long CompletedPhysicsStep => body.CompletedPhysicsStep;
    public double PhysicsStepMs => body.PhysicsStepMs;
    public void AdvanceOneStep() => body.AdvanceOneStep();
    public PhysicalObservationFrame CaptureObservation()
    {
        var frame = ((IAvatarPhysicalObservationBody)body).CaptureObservation();
        if (CompletedPhysicsStep != DropContactAtStep) return frame;
        if (DroppedSiteId is not null) throw new InvalidOperationException("Do not repeat the intervention capture.");
        DroppedSiteId = "head";
        var changed = frame with { Contacts = frame.Contacts.Where(c => c.SiteId != DroppedSiteId).ToArray() };
        if (frame.Contacts.Count != 15 || changed.Contacts.Count != 14)
            throw new InvalidOperationException("Intervention did not remove exactly the declared site.");
        UnityPhysicalSampleBody.WriteNew(Path.Combine(output, "missing-contact-intervention.json"),
            new MissingContactEvidence(CompletedPhysicsStep, DroppedSiteId, body.StepSources[^1].ReplyFile, changed));
        return changed;
    }
}
