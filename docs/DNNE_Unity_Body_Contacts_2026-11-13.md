# Actual Unity body and contact delivery

The completed native body-only study retained all contact replies but delivered
only body observations. The new `--combined` mode of `UnityPhysicalSampleProbe`
uses the complete observation producer/outbox/receiver against its own isolated
Unity child. It uses the existing 15-link, 38-muscle plant and step-ownership
bridge. No new physical controller, afferent generator or live host is selected.

Before this change, Avatar's producer, transport, shared body/contact contracts,
Unity encoder, physical bridge and migration audit were inspected. Body-only
mode remains available. The shared saved-source audit is extracted without
changing its original transaction and body-only acceptance conditions. All old
frozen packages remain unchanged.

## Native source completeness

The combined adapter now validates raw body JSON before shared-record defaults
can hide omissions. Every native member must be present, including nested muscle,
pressure, balance and substrate fields. Null required plants, duplicate members,
unknown fields and nonfinite measurements reject. The native schema omits exactly
the eight finger/grip members identified in the body-profile review. Their shared
defaults remain explicitly unavailable; a source supplying those members requires
a revised declared encoding. The computed substrate validity flag is not source
data. Finite out-of-range measurements remain visible rather than being clipped.

This does not make temperature, blood oxygen or support-plane offset dynamic
plants. Their reference declarations remain in the immutable v2 stream identity,
along with all eleven unavailable member paths. A future neuronal consumer must
honor this profile. The legacy live receiver and editor are not changed here.

Four new source-schema tests exercise signed measurements and declared defaults,
deletion of every native member, duplicate/unknown/new-hand fields, null plants
and preservation of finite out-of-range data. They are synthetic boundary checks,
not a substitute for the real Unity capture.

## Prospective physical checks

The independent visible PowerShell window first requires **74 focused tests**:
31 combined protocol, four native schema, 16 body producer, 22 body transport and
one solution manifest. It then builds a frozen Unity project in a separate writable
workspace, retaining the built assets/settings and executed binaries.

The new physical study requires:

* A fresh paused, isolated muscular Avatar and rejected competing step owners.
* Initial step 0 plus actual steps 1–200 delivered exactly: 201 bodies and **3,015
  contact observations**, including unloaded sites, through the combined v2 lane.
* One original native reply per endpoint, strict body/contact parsing, immutable
  availability/profile identity, original signed values and capture stamps.
* Bounded capacity of four samples, backpressure before physics, packet sizes
  1/3/4, duplicate acceptance/acknowledgement without duplicate consumption.
* Independently inspected holds at steps 0, 3, 80 and 201. Body and every contact
  must remain identical, excluding only their wall-clock capture stamps.
* Actual finite motion greater than 5 cm and nonzero contact load. These unchanged
  body-study thresholds establish an exercised plant, not physiological behavior.
* A deliberate adapter intervention at real step 201: retain the complete original
  native reply, remove only its head contact in a separate saved copy, and pass
  that incomplete endpoint to the producer. It must fault, refuse another advance
  and leave captured/delivered completeness at step 200. This is an injected loss,
  not evidence that Unity naturally lost a contact.

The source grid, all bridge requests/replies, layout, packets, receipts, consumed
samples, intervention and producer fault state are preserved. `--audit-combined`
reconstructs every endpoint from the native replies and replays saved packets
without starting Unity or a neuronal model. It checks the exact omitted site,
held body/contact state, complete delivered bytes and unchanged original source.

## Execution and evidence status

Both .NET projects build with zero warnings/errors. Tests and actual Unity physics
must run in the independent PowerShell window; no pass is inferred from compilation.
Use `prepare-unity-physical-sample.py --combined` from clean pushed source, verify
the initial D: backup, then its frozen `start-unity-physical-sample-window.ps1`.
The launcher requires the declared combined protocol/counts and uses a distinct
body/contact task title/footer, printing the results folder again at completion.

This study does not measure throughput, connect a live DNNE clock, introduce a
sensory neuron, calibrate afferent physiology or demonstrate Avatar recovery.
The existing contact saturation, aggregation, coordinate convention and stale
unloaded metadata remain explicit limitations. Original legacy behavior and saves
stay preserved; no claim of complete biological sensing follows from exact delivery.
