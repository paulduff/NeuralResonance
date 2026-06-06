# Anatomical Reference Pack

This folder is the canon visual reference pack for cortical outer-shape work, lobe placement, and sulcal/gyral landmark validation.

## Included local references

### 1. `reference_01_cerebral_cortex_overview.jpg`
Use for:
- basic cortex vs white matter grounding
- frontal, lateral, and medial orientation sanity checks
- confirming the distinction between outer cortex and deeper matter

### 2. `reference_02_superior_surface_gyri_sulci.jpeg`
Use for:
- superior surface fold pattern checks
- dorsal midline split validation
- rough placement of frontal/parietal/occipital bands from above

### 3. `reference_03_lobes_lateral_view.jpg`
Use for:
- quick lobe colour sanity checks in the renderer
- frontal / parietal / temporal / occipital macro-boundary validation
- simple visual communication in docs and UI planning

### 4. `reference_04_sobotta_superior_surface_labels.png`
Use for:
- superior frontal sulcus / gyrus checks
- central sulcus placement from the superior aspect
- interparietal and parieto-occipital landmark tracing
- top-view regression validation for hemispheric folding

## Curated external references to add during future passes

These are the most useful additional references identified during review. They were selected because they help validate different geometry classes rather than just providing more pictures.

### A. Medial hemisphere anatomy
- Gray727 calcarine sulcus (Wikimedia Commons)
- Why it matters: gives a clean medial view with cingulate gyrus, corpus callosum, fornix, hippocampal gyrus, and calcarine region.
- Best use: medial wall validation, corpus callosum neighbourhood, hippocampal placement, visual cortex neighbourhood.

### B. Lateral gyri atlas surface
- Lateral surface of cerebral cortex - gyri (Wikimedia Commons; derived from Hagmann et al.)
- Why it matters: gives a denser atlas-like partition of named lateral gyri.
- Best use: superior/middle/inferior frontal gyri, postcentral region, supramarginal/angular placement, temporal band checks.

### C. Lateral + superior educational anatomy
- Brain - Cerebrum / Brain Anatomy pages (Medicine LibreTexts)
- Why it matters: these pages include clear superior and lateral educational diagrams with the central sulcus and adjacent gyri.
- Best use: fast validation for the central sulcus, precentral/postcentral relation, and high-level lobe boundaries.

### D. Midsagittal internal anatomy
- Brain Anatomy / Diencephalon and Brain Stem pages (Medicine LibreTexts)
- Why it matters: gives clean midsagittal views of thalamus, hypothalamus, corpus callosum, brainstem, and surrounding landmarks.
- Best use: midline structure placement and region spacing for thalamus / hypothalamus / pons / cerebellum.

## Recommended validation workflow

1. Validate the whole-brain outer silhouette first.
2. Validate the superior midline split and dominant dorsal folds.
3. Validate lateral lobe boundaries.
4. Validate the central sulcus and pre/postcentral relationship.
5. Validate medial wall structures and callosal neighbourhood.
6. Only then refine finer sulcal detail.

## Practical rule for renderer work

When references disagree, prefer atlas-style or anatomy-teaching references for geometry and landmark placement over simplified health-education illustrations.
