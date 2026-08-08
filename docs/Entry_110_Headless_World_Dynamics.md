# Folded Archive Entry 110: Headless World Dynamics

## Purpose

WorldSim must remain inspectable as a visible physical world, but its numerical
consequences must not depend on WPF rendering or frame rate. The physiology and
carried-device state transitions therefore live in `NRE.SimAvatar` and are
consumed by the visible simulator.

## Extracted Dynamics

- metabolic energy and hydration depletion;
- tissue damage from energy depletion and dehydration;
- physical recovery only when neuronal sleep and shelter coincide;
- food and water consequences with bounded state;
- predator-contact tissue damage;
- one shared carried-device capacity with deterministic range priority and
  charge discharge.

The extraction also fixes an inventory inconsistency: the former GUI fields
could retain three short and three long charges while displaying a capped total
of three. The shared inventory now enforces one physical capacity.

## Qualification

`tools/run-headless-worldsim-qualification.ps1` runs the deterministic scenario
tests without starting WPF, the Control Program, or any neural structures. A
10,000-step scenario must produce bit-for-bit equal state on repeated runs.
These tests establish deterministic environmental consequences; they do not
claim intelligent behaviour or replace visible embodied qualification.
