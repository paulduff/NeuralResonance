namespace NRE.Core.Engine;

/// <summary>
/// Diphthong Vocal Tract: Biologically Accurate Articulatory Speech Synthesis
///
/// BIOLOGICAL BASIS:
/// Speech production involves continuous trajectories through articulatory space,
/// not discrete phoneme concatenation. The vocal tract is defined by 6 articulatory
/// parameters that the motor cortex controls via brainstem motor nuclei:
///
///   - Jaw opening    (Trigeminal nerve, CN V → masseter/temporalis)
///   - Tongue height  (Hypoglossal nerve, CN XII → intrinsic tongue muscles)
///   - Tongue advance  (CN XII → genioglossus)
///   - Lip rounding   (Facial nerve, CN VII → orbicularis oris)
///   - Velum height   (Vagus/pharyngeal plexus → levator veli palatini)
///   - Voicing        (Recurrent laryngeal nerve → vocalis muscle)
///
/// A diphthong is a smooth glide between two vowel targets (e.g., /aɪ/ in "eye").
/// Coarticulation means each phoneme is influenced by its neighbours — the tongue
/// starts moving toward the next target before the current one is complete.
///
/// OUTPUT: Formant frequencies (F1, F2, F3) that can drive a formant synthesizer
/// or be mapped to Web Speech API parameters. F1 ≈ jaw opening/tongue height,
/// F2 ≈ tongue front-back position, F3 ≈ lip rounding.
///
/// NEURAL PATHWAY:
///   Broca's Area (phonological plan) → M1 larynx strip (motor commands)
///   → Brainstem motor nuclei (CN V, VII, XII) → Articulatory parameters
///   → Vocal tract shape → Formant synthesis → Audio
/// </summary>
public sealed class DiphthongVocalTract
{
    // === ARTICULATORY PARAMETER SPACE ===
    // Each parameter is normalized [0..1] representing the full physiological range.

    /// <summary>
    /// Complete articulatory state of the vocal tract at one moment in time.
    /// </summary>
    public readonly record struct ArticulatoryState(
        float JawOpen,       // 0=closed, 1=maximally open (F1 correlate)
        float TongueHeight,  // 0=low, 1=high (inverse F1 correlate)
        float TongueAdvance, // 0=back, 1=front (F2 correlate)
        float LipRound,      // 0=spread, 1=fully rounded (lowers F2, F3)
        float VelumHeight,   // 0=lowered (nasal), 1=raised (oral)
        float Voicing)       // 0=voiceless, 1=fully voiced
    {
        /// <summary>Linearly interpolate between two states.</summary>
        public static ArticulatoryState Lerp(ArticulatoryState a, ArticulatoryState b, float t)
        {
            t = Math.Clamp(t, 0f, 1f);
            float u = 1f - t;
            return new ArticulatoryState(
                JawOpen:       u * a.JawOpen       + t * b.JawOpen,
                TongueHeight:  u * a.TongueHeight  + t * b.TongueHeight,
                TongueAdvance: u * a.TongueAdvance + t * b.TongueAdvance,
                LipRound:      u * a.LipRound      + t * b.LipRound,
                VelumHeight:   u * a.VelumHeight    + t * b.VelumHeight,
                Voicing:       u * a.Voicing        + t * b.Voicing);
        }

        /// <summary>Smooth interpolation with ease-in/ease-out (more biological).</summary>
        public static ArticulatoryState SmoothLerp(ArticulatoryState a, ArticulatoryState b, float t)
        {
            // Smoothstep for more natural articulator movement
            t = Math.Clamp(t, 0f, 1f);
            t = t * t * (3f - 2f * t);
            return Lerp(a, b, t);
        }
    }

    /// <summary>
    /// Formant frequencies derived from articulatory state.
    /// F1 and F2 define vowel quality; F3 and F0 add naturalness.
    /// </summary>
    public readonly record struct FormantState(
        float F0,   // Fundamental frequency (pitch), Hz
        float F1,   // First formant (jaw/tongue height), Hz
        float F2,   // Second formant (tongue advance), Hz
        float F3,   // Third formant (lip rounding), Hz
        float Amplitude, // 0..1
        bool Voiced);

    // === PHONEME TARGET TABLE ===
    // Each phoneme maps to an articulatory target — the "ideal" vocal tract shape.
    // During speech, the tract glides between consecutive targets.

    private static readonly Dictionary<string, ArticulatoryState> _targets = new()
    {
        // === VOWELS (IPA) ===
        // Monophthongs
        ["i"]  = new(0.15f, 0.90f, 0.90f, 0.10f, 1.0f, 1.0f), // close front unrounded (beat)
        ["ɪ"]  = new(0.25f, 0.80f, 0.80f, 0.10f, 1.0f, 1.0f), // near-close near-front (bit)
        ["e"]  = new(0.35f, 0.65f, 0.85f, 0.10f, 1.0f, 1.0f), // close-mid front (bait)
        ["ɛ"]  = new(0.45f, 0.50f, 0.80f, 0.10f, 1.0f, 1.0f), // open-mid front (bet)
        ["æ"]  = new(0.70f, 0.30f, 0.75f, 0.10f, 1.0f, 1.0f), // near-open front (bat)
        ["ɑ"]  = new(0.85f, 0.15f, 0.30f, 0.10f, 1.0f, 1.0f), // open back unrounded (father)
        ["ɒ"]  = new(0.80f, 0.15f, 0.25f, 0.40f, 1.0f, 1.0f), // open back rounded (lot, BrE)
        ["ɔ"]  = new(0.60f, 0.35f, 0.30f, 0.60f, 1.0f, 1.0f), // open-mid back rounded (thought)
        ["o"]  = new(0.40f, 0.55f, 0.25f, 0.70f, 1.0f, 1.0f), // close-mid back rounded (go)
        ["ʊ"]  = new(0.30f, 0.70f, 0.35f, 0.60f, 1.0f, 1.0f), // near-close near-back (foot)
        ["u"]  = new(0.15f, 0.90f, 0.15f, 0.85f, 1.0f, 1.0f), // close back rounded (boot)
        ["ʌ"]  = new(0.55f, 0.45f, 0.50f, 0.10f, 1.0f, 1.0f), // open-mid back unrounded (strut)
        ["ə"]  = new(0.40f, 0.50f, 0.50f, 0.15f, 1.0f, 1.0f), // schwa (about)
        ["ɜ"]  = new(0.45f, 0.45f, 0.55f, 0.15f, 1.0f, 1.0f), // open-mid central (bird)

        // === DIPHTHONGS (as target pairs — first element) ===
        // Diphthongs are generated by interpolating between two vowel targets.
        // These entries are the onset; the glide target is looked up separately.

        // === CONSONANTS ===
        // Stops (voiceless)
        ["p"]  = new(0.00f, 0.50f, 0.50f, 0.00f, 1.0f, 0.0f), // bilabial stop
        ["t"]  = new(0.10f, 0.80f, 0.85f, 0.00f, 1.0f, 0.0f), // alveolar stop
        ["k"]  = new(0.10f, 0.85f, 0.30f, 0.00f, 1.0f, 0.0f), // velar stop
        // Stops (voiced)
        ["b"]  = new(0.00f, 0.50f, 0.50f, 0.00f, 1.0f, 1.0f), // voiced bilabial
        ["d"]  = new(0.10f, 0.80f, 0.85f, 0.00f, 1.0f, 1.0f), // voiced alveolar
        ["g"]  = new(0.10f, 0.85f, 0.30f, 0.00f, 1.0f, 1.0f), // voiced velar

        // Fricatives
        ["f"]  = new(0.05f, 0.50f, 0.50f, 0.00f, 1.0f, 0.0f), // labiodental
        ["v"]  = new(0.05f, 0.50f, 0.50f, 0.00f, 1.0f, 1.0f),
        ["θ"]  = new(0.10f, 0.70f, 0.90f, 0.00f, 1.0f, 0.0f), // dental
        ["ð"]  = new(0.10f, 0.70f, 0.90f, 0.00f, 1.0f, 1.0f),
        ["s"]  = new(0.10f, 0.85f, 0.90f, 0.00f, 1.0f, 0.0f), // alveolar
        ["z"]  = new(0.10f, 0.85f, 0.90f, 0.00f, 1.0f, 1.0f),
        ["ʃ"]  = new(0.15f, 0.80f, 0.70f, 0.30f, 1.0f, 0.0f), // post-alveolar
        ["ʒ"]  = new(0.15f, 0.80f, 0.70f, 0.30f, 1.0f, 1.0f),
        ["h"]  = new(0.30f, 0.50f, 0.50f, 0.00f, 1.0f, 0.0f), // glottal

        // Nasals (velum lowered)
        ["m"]  = new(0.00f, 0.50f, 0.50f, 0.00f, 0.0f, 1.0f), // bilabial nasal
        ["n"]  = new(0.10f, 0.80f, 0.85f, 0.00f, 0.0f, 1.0f), // alveolar nasal
        ["ŋ"]  = new(0.10f, 0.85f, 0.30f, 0.00f, 0.0f, 1.0f), // velar nasal

        // Approximants
        ["l"]  = new(0.15f, 0.75f, 0.85f, 0.00f, 1.0f, 1.0f), // lateral
        ["r"]  = new(0.20f, 0.65f, 0.70f, 0.10f, 1.0f, 1.0f), // postalveolar approx
        ["w"]  = new(0.15f, 0.85f, 0.15f, 0.85f, 1.0f, 1.0f), // labial-velar
        ["j"]  = new(0.15f, 0.90f, 0.90f, 0.10f, 1.0f, 1.0f), // palatal

        // Silence / rest
        ["_"]  = new(0.10f, 0.50f, 0.50f, 0.20f, 1.0f, 0.0f),
    };

    /// <summary>
    /// Diphthong definitions: onset vowel → glide target vowel.
    /// During a diphthong, the tract smoothly transitions from onset to target.
    /// </summary>
    private static readonly Dictionary<string, (string onset, string target)> _diphthongs = new()
    {
        ["aɪ"] = ("ɑ", "ɪ"),   // price, eye
        ["eɪ"] = ("e", "ɪ"),   // face, day
        ["ɔɪ"] = ("ɔ", "ɪ"),   // choice, boy
        ["aʊ"] = ("ɑ", "ʊ"),   // mouth, how
        ["oʊ"] = ("o", "ʊ"),   // goat, go
        ["ɪə"] = ("ɪ", "ə"),   // near, here (BrE)
        ["eə"] = ("ɛ", "ə"),   // square, there (BrE)
        ["ʊə"] = ("ʊ", "ə"),   // cure, tour (BrE)
    };

    // === TRACT STATE ===
    private ArticulatoryState _current;
    private ArticulatoryState _targetState;
    private float _transitionProgress = 1.0f; // 0=just started, 1=at target
    private float _transitionRate = 1.0f / 0.080f; // default: 80ms transition

    // Coarticulation lookahead
    private readonly Queue<(ArticulatoryState target, float durationSec)> _gestureQueue = new();

    // Output formant history (for smoothing / analysis)
    private FormantState _lastFormant;

    // Base pitch (can be modulated by prosody)
    public float BasePitchHz { get; set; } = 120f; // male default
    public float PitchRange { get; set; } = 40f;   // ±40Hz variation

    public DiphthongVocalTract()
    {
        _current = _targets["_"];
        _targetState = _current;
        _lastFormant = ArticulatoryToFormant(_current, 1.0f);
    }

    // === PUBLIC API ===

    /// <summary>
    /// Enqueue a phoneme sequence for articulation. Each phoneme is looked up
    /// in the target table; diphthongs are decomposed into onset+glide.
    /// </summary>
    public void EnqueuePhonemes(IReadOnlyList<string> phonemes, float rate = 1.0f)
    {
        for (int i = 0; i < phonemes.Count; i++)
        {
            string ph = phonemes[i];
            float dur = GetPhoneDuration(ph) / Math.Max(0.5f, rate);

            if (_diphthongs.TryGetValue(ph, out var diph))
            {
                // Diphthong: two targets with continuous glide
                var onsetState = GetTarget(diph.onset);
                var glideState = GetTarget(diph.target);

                // Onset gets 40% of duration, glide gets 60% (asymmetric — 
                // diphthongs spend more time gliding than at onset)
                _gestureQueue.Enqueue((onsetState, dur * 0.40f));
                _gestureQueue.Enqueue((glideState, dur * 0.60f));
            }
            else
            {
                var target = GetTarget(ph);

                // COARTICULATION: if next phoneme is known, blend the current
                // target slightly toward it (anticipatory coarticulation).
                if (i + 1 < phonemes.Count)
                {
                    string next = phonemes[i + 1];
                    var nextTarget = GetTarget(_diphthongs.ContainsKey(next)
                        ? _diphthongs[next].onset : next);

                    // Blend 15% toward next target (anticipatory coarticulation)
                    target = ArticulatoryState.Lerp(target, nextTarget, 0.15f);
                }

                _gestureQueue.Enqueue((target, dur));
            }
        }
    }

    /// <summary>
    /// Enqueue a word by converting it to phonemes via grapheme-to-phoneme rules.
    /// </summary>
    public void EnqueueWord(string word, float rate = 1.0f)
    {
        var phonemes = GraphemeToPhoneme(word);
        EnqueuePhonemes(phonemes, rate);
        // Word boundary pause
        _gestureQueue.Enqueue((GetTarget("_"), 0.050f));
    }

    /// <summary>
    /// Advance the vocal tract by dt seconds. Returns the current formant state.
    /// This should be called at the simulation tick rate (e.g., 60Hz).
    /// </summary>
    public FormantState Step(float dt, float pitchModulation = 0f)
    {
        // Advance transition toward current target
        _transitionProgress += _transitionRate * dt;

        if (_transitionProgress >= 1.0f)
        {
            _current = _targetState;
            _transitionProgress = 1.0f;

            // Pop next gesture from queue
            if (_gestureQueue.TryDequeue(out var next))
            {
                _targetState = next.target;
                _transitionRate = 1.0f / Math.Max(0.010f, next.durationSec);
                _transitionProgress = 0f;
            }
        }

        // Compute current articulatory state (smooth interpolation)
        var state = ArticulatoryState.SmoothLerp(_current, _targetState, _transitionProgress);

        // Convert to formants
        float pitch = BasePitchHz + PitchRange * pitchModulation;
        var formant = ArticulatoryToFormant(state, pitch);

        _lastFormant = formant;
        return formant;
    }

    /// <summary>True if the tract has pending gestures or is mid-transition.</summary>
    public bool IsSpeaking => _gestureQueue.Count > 0 || _transitionProgress < 1.0f;

    /// <summary>Current articulatory state (for visualization / telemetry).</summary>
    public ArticulatoryState CurrentState => ArticulatoryState.SmoothLerp(
        _current, _targetState, Math.Clamp(_transitionProgress, 0f, 1f));

    /// <summary>Last computed formant state.</summary>
    public FormantState LastFormant => _lastFormant;

    /// <summary>Number of pending gestures in the queue.</summary>
    public int PendingGestures => _gestureQueue.Count;

    // === ARTICULATORY → FORMANT MAPPING ===

    /// <summary>
    /// Convert articulatory parameters to formant frequencies.
    /// Based on Fant (1960) acoustic theory of speech production.
    ///
    /// F1 ≈ inversely related to tongue height; directly related to jaw opening
    /// F2 ≈ tongue advancement (front vowels have high F2, back vowels low F2)
    /// F3 ≈ lip rounding lowers it; also influenced by tongue tip position
    /// </summary>
    private static FormantState ArticulatoryToFormant(ArticulatoryState s, float pitch)
    {
        // F1: 250-900 Hz. Opens with jaw, lowers with tongue height.
        float f1 = 250f + 650f * s.JawOpen * (1f - 0.6f * s.TongueHeight);

        // F2: 700-2500 Hz. Rises with tongue advance, lowers with lip rounding.
        float f2 = 700f + 1800f * s.TongueAdvance * (1f - 0.25f * s.LipRound);

        // F3: 1800-3500 Hz. Lowers with lip rounding and retroflex articulation.
        float f3 = 2500f + 1000f * (1f - 0.4f * s.LipRound) * (0.5f + 0.5f * s.TongueAdvance);

        // Amplitude: voiced segments are louder; jaw opening contributes
        float amp = s.Voicing * (0.4f + 0.6f * s.JawOpen);

        // Nasal: lowered velum couples nasal cavity, adds nasal formant ~250Hz
        // and reduces oral formant amplitudes
        if (s.VelumHeight < 0.5f)
        {
            float nasality = 1f - s.VelumHeight * 2f;
            f1 = f1 * (1f - 0.2f * nasality) + 250f * nasality;
            amp *= (1f - 0.15f * nasality);
        }

        return new FormantState(
            F0: Math.Clamp(pitch, 50f, 500f),
            F1: Math.Clamp(f1, 200f, 1000f),
            F2: Math.Clamp(f2, 600f, 2800f),
            F3: Math.Clamp(f3, 1600f, 3800f),
            Amplitude: Math.Clamp(amp, 0f, 1f),
            Voiced: s.Voicing > 0.5f);
    }

    // === PHONEME DURATION ===

    private static float GetPhoneDuration(string phoneme)
    {
        // Durations in seconds (half of typical — accelerated simulation)
        if (_diphthongs.ContainsKey(phoneme)) return 0.090f;

        return phoneme switch
        {
            "i" or "u" or "ɑ" or "ɔ" or "ɜ" => 0.060f,
            "ɪ" or "ɛ" or "æ" or "ʌ" or "ə" or "ʊ" or "ɒ" => 0.040f,
            "p" or "t" or "k" or "b" or "d" or "g" => 0.033f,
            "f" or "v" or "θ" or "ð" or "s" or "z" or "ʃ" or "ʒ" or "h" => 0.045f,
            "m" or "n" or "ŋ" => 0.035f,
            "l" or "r" or "w" or "j" => 0.030f,
            "_" => 0.025f,
            _ => 0.040f
        };
    }

    // === GRAPHEME TO PHONEME (simplified English rules) ===

    /// <summary>
    /// Convert English text to IPA phoneme sequence.
    /// This is a simplified rule-based system; a full implementation would use
    /// a neural G2P model or a pronunciation dictionary (CMUdict).
    /// </summary>
    public static List<string> GraphemeToPhoneme(string word)
    {
        word = word.ToLowerInvariant().Trim();
        var result = new List<string>();

        // Check dictionary first
        if (_pronunciationDict.TryGetValue(word, out var known))
        {
            result.AddRange(known);
            return result;
        }

        // Rule-based fallback
        int i = 0;
        while (i < word.Length)
        {
            // Try digraphs first
            if (i + 1 < word.Length)
            {
                string di = word.Substring(i, 2);
                if (_digraphMap.TryGetValue(di, out var diPh))
                {
                    result.Add(diPh);
                    i += 2;
                    continue;
                }
            }

            char c = word[i];
            if (_simpleMap.TryGetValue(c, out var ph))
            {
                result.Add(ph);
            }
            // Skip silent letters and unmapped characters

            i++;
        }

        return result;
    }

    private static ArticulatoryState GetTarget(string phoneme)
    {
        return _targets.TryGetValue(phoneme, out var t) ? t : _targets["_"];
    }

    // === PRONUNCIATION DICTIONARY (common words) ===
    private static readonly Dictionary<string, string[]> _pronunciationDict = new()
    {
        ["hello"] = new[] { "h", "ɛ", "l", "oʊ" },
        ["world"] = new[] { "w", "ɜ", "l", "d" },
        ["the"]   = new[] { "ð", "ə" },
        ["is"]    = new[] { "ɪ", "z" },
        ["are"]   = new[] { "ɑ", "r" },
        ["you"]   = new[] { "j", "u" },
        ["i"]     = new[] { "aɪ" },
        ["a"]     = new[] { "ə" },
        ["we"]    = new[] { "w", "i" },
        ["they"]  = new[] { "ð", "eɪ" },
        ["this"]  = new[] { "ð", "ɪ", "s" },
        ["that"]  = new[] { "ð", "æ", "t" },
        ["what"]  = new[] { "w", "ɒ", "t" },
        ["how"]   = new[] { "h", "aʊ" },
        ["why"]   = new[] { "w", "aɪ" },
        ["yes"]   = new[] { "j", "ɛ", "s" },
        ["no"]    = new[] { "n", "oʊ" },
        ["think"] = new[] { "θ", "ɪ", "ŋ", "k" },
        ["know"]  = new[] { "n", "oʊ" },
        ["see"]   = new[] { "s", "i" },
        ["say"]   = new[] { "s", "eɪ" },
        ["make"]  = new[] { "m", "eɪ", "k" },
        ["time"]  = new[] { "t", "aɪ", "m" },
        ["go"]    = new[] { "g", "oʊ" },
        ["come"]  = new[] { "k", "ʌ", "m" },
        ["good"]  = new[] { "g", "ʊ", "d" },
        ["new"]   = new[] { "n", "j", "u" },
        ["out"]   = new[] { "aʊ", "t" },
        ["day"]   = new[] { "d", "eɪ" },
        ["eye"]   = new[] { "aɪ" },
        ["boy"]   = new[] { "b", "ɔɪ" },
        ["now"]   = new[] { "n", "aʊ" },
        ["high"]  = new[] { "h", "aɪ" },
        ["night"] = new[] { "n", "aɪ", "t" },
        ["light"] = new[] { "l", "aɪ", "t" },
        ["about"] = new[] { "ə", "b", "aʊ", "t" },
        ["voice"] = new[] { "v", "ɔɪ", "s" },
        ["noise"] = new[] { "n", "ɔɪ", "z" },
        ["fire"]  = new[] { "f", "aɪ", "ə", "r" },
        ["here"]  = new[] { "h", "ɪə", "r" },
        ["there"] = new[] { "ð", "eə", "r" },
        ["brain"] = new[] { "b", "r", "eɪ", "n" },
        ["mind"]  = new[] { "m", "aɪ", "n", "d" },
        ["speak"] = new[] { "s", "p", "i", "k" },
        ["talk"]  = new[] { "t", "ɔ", "k" },
        ["hear"]  = new[] { "h", "ɪə", "r" },
        ["sound"] = new[] { "s", "aʊ", "n", "d" },
        ["word"]  = new[] { "w", "ɜ", "d" },
        ["name"]  = new[] { "n", "eɪ", "m" },
        ["feel"]  = new[] { "f", "i", "l" },
        ["life"]  = new[] { "l", "aɪ", "f" },
        ["find"]  = new[] { "f", "aɪ", "n", "d" },
        ["point"] = new[] { "p", "ɔɪ", "n", "t" },
        ["right"] = new[] { "r", "aɪ", "t" },
        ["own"]   = new[] { "oʊ", "n" },
        ["home"]  = new[] { "h", "oʊ", "m" },
        ["place"] = new[] { "p", "l", "eɪ", "s" },
    };

    // Simple grapheme → phoneme maps
    private static readonly Dictionary<string, string> _digraphMap = new()
    {
        ["th"] = "θ", ["sh"] = "ʃ", ["ch"] = "tʃ", ["ng"] = "ŋ",
        ["ph"] = "f", ["wh"] = "w", ["ck"] = "k", ["ee"] = "i",
        ["oo"] = "u", ["ou"] = "aʊ", ["oi"] = "ɔɪ", ["oy"] = "ɔɪ",
        ["ai"] = "eɪ", ["ay"] = "eɪ", ["ow"] = "oʊ",
        ["ea"] = "i", ["ie"] = "i", ["ey"] = "eɪ",
    };

    private static readonly Dictionary<char, string> _simpleMap = new()
    {
        ['a'] = "æ", ['b'] = "b", ['c'] = "k", ['d'] = "d", ['e'] = "ɛ",
        ['f'] = "f", ['g'] = "g", ['h'] = "h", ['i'] = "ɪ", ['j'] = "dʒ",
        ['k'] = "k", ['l'] = "l", ['m'] = "m", ['n'] = "n", ['o'] = "ɒ",
        ['p'] = "p", ['q'] = "k", ['r'] = "r", ['s'] = "s", ['t'] = "t",
        ['u'] = "ʌ", ['v'] = "v", ['w'] = "w", ['x'] = "k", ['y'] = "j",
        ['z'] = "z",
    };
}
