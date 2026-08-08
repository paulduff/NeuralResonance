# Folded Archive Entry 111: Dyad Wire Contract Compatibility

## Purpose

Entity and DNNE are independently versioned components of Dyad. This rung makes
their language and population-training boundary mechanically inspectable before
either component is deployed to Tartarus.

## Canonical Manifest

Both repositories carry the byte-identical file
`contracts/dyad-wire-contract.v1.json`. It records:

- the reviewed-language and adapter-training protocol identifiers;
- the maximum candidate-text length;
- every property in numeric grounding and source records;
- the accepted-review and Entity-generation response shapes;
- the adapter-training record shape; and
- Entity's final accepted-emission envelope.

Reflection tests compare the manifest with each repository's compiled DTOs and
constants. A wire-visible code change therefore requires an intentional manifest
change in both repositories.

## Deployment Check

Run the DNNE-side peer check before launch:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "C:\Users\User\source\repos\Folded Archive\NeuralResonanceEngine.DNN\tools\test-dyad-wire-contract.ps1" -PeerContractPath "C:\Users\User\source\repos\EntityLLM\contracts\dyad-wire-contract.v1.json"
```

The command compares SHA-256 hashes of the complete files. A mismatch is a hard
failure; textual similarity is not accepted.

## Boundary

This is compatibility evidence, not semantic authority. Entity still proposes
language, DNNE still reviews it from current neuronal state, and only an exact
accepted candidate may be emitted or voiced.
