namespace NRE.Core.Engine;

/// <summary>
/// Biologically inspired language system with auditory, semantic, dorsal motor,
/// and lexical pathways. The diagram-heavy legacy header was replaced with this
/// compact summary to keep the production source readable.
/// </summary>
public sealed class LanguageSystem
{
    private readonly object _gate = new();
    
    // === ANATOMICAL REGIONS ===
    private readonly PrimaryAuditoryCortex _a1;
    private readonly WernickesArea _wernicke;
    private readonly BrocasArea _broca;
    private readonly AngularGyrus _angularGyrus;
    private readonly ArcuateFasciculus _arcuate;
    private readonly PhonologicalLoop _phonoLoop;
    private readonly SemanticNetwork _semantics;
    private readonly ArticulatoryMotorCortex _motorSpeech;
    
    // === LEXICON ===
    private readonly Lexicon _lexicon;
    
    // === STATE ===
    private readonly List<string> _outputBuffer = new();
    private readonly Queue<PhonemeEvent> _inputQueue = new();
    private float _comprehensionConfidence;
    private float _productionReadiness;
    
    public LanguageSystem(int vocabularyCapacity = 10000)
    {
        _a1 = new PrimaryAuditoryCortex();
        _wernicke = new WernickesArea();
        _broca = new BrocasArea();
        _angularGyrus = new AngularGyrus();
        _arcuate = new ArcuateFasciculus();
        _phonoLoop = new PhonologicalLoop(capacity: 7);
        _semantics = new SemanticNetwork(dimensions: 128);
        _motorSpeech = new ArticulatoryMotorCortex();
        _lexicon = new Lexicon(vocabularyCapacity);
        
        // Bootstrap with basic vocabulary
        InitializeCoreLexicon();
    }
    
    public LanguageState Snapshot()
    {
        lock (_gate)
        {
            return new LanguageState(
                LexiconSize: _lexicon.Size,
                PhonologicalBufferCount: _phonoLoop.Count,
                ActiveSemanticNodes: _semantics.ActiveNodeCount,
                ComprehensionConfidence: _comprehensionConfidence,
                ProductionReadiness: _productionReadiness,
                WernickeActivity: _wernicke.GetActivity(),
                BrocaActivity: _broca.GetActivity(),
                PendingOutputWords: _outputBuffer.Count);
        }
    }
    
    /// <summary>
    /// Process language input (text or phoneme stream).
    /// Implements the ventral stream: sound to meaning.
    /// </summary>
    public LanguageComprehensionResult ProcessInput(string text, float dt)
    {
        lock (_gate)
        {
            if (string.IsNullOrEmpty(text))
                return new LanguageComprehensionResult(false, Array.Empty<SemanticToken>(), 0f, null);
            
            // === 1) TOKENIZE: text to words ===
            var words = TokenizeText(text);
            
            // === 2) A1: Phonological encoding ===
            var phonemeSequences = new List<PhonemeSequence>();
            foreach (var word in words)
            {
                var phonemes = _a1.EncodeWord(word);
                phonemeSequences.Add(phonemes);
                _phonoLoop.Push(phonemes);
            }
            
            // === 3) WERNICKE'S: Lexical access ===
            var lexicalItems = new List<LexicalItem>();
            foreach (var word in words)
            {
                var item = _wernicke.ProcessWord(word, _lexicon, dt);
                if (item.HasValue)
                    lexicalItems.Add(item.Value);
            }
            
            // === 4) ANGULAR GYRUS: Semantic integration ===
            var semanticTokens = new List<SemanticToken>();
            foreach (var item in lexicalItems)
            {
                var semantic = _angularGyrus.IntegrateSemantics(item, _semantics, dt);
                semanticTokens.Add(semantic);
            }
            
            // === 5) BROCA'S: Syntactic parsing ===
            var parseTree = _broca.ParseSyntax(lexicalItems, dt);
            
            // === 6) ARCUATE: Integrate streams ===
            _arcuate.Transmit(_wernicke, _broca, dt);
            
            // Compute comprehension confidence
            float lexicalCoverage = lexicalItems.Count / (float)Math.Max(1, words.Length);
            float syntacticCoherence = parseTree?.Coherence ?? 0f;
            _comprehensionConfidence = lexicalCoverage * 0.6f + syntacticCoherence * 0.4f;
            
            return new LanguageComprehensionResult(
                Success: _comprehensionConfidence > 0.5f,
                SemanticTokens: semanticTokens.ToArray(),
                Confidence: _comprehensionConfidence,
                ParseTree: parseTree);
        }
    }

    /// <summary>
    /// Process heard phonemes directly, allowing auditory learning/comprehension without a written surface form.
    /// </summary>
    public LanguageComprehensionResult ProcessPhonemeInput(IReadOnlyList<string> phonemes, float dt, string? surfaceHint = null)
    {
        lock (_gate)
        {
            if (phonemes == null || phonemes.Count == 0)
                return new LanguageComprehensionResult(false, Array.Empty<SemanticToken>(), 0f, null);

            var cleaned = phonemes
                .Where(p => !string.IsNullOrWhiteSpace(p) && p != "_")
                .ToArray();
            if (cleaned.Length == 0)
                return new LanguageComprehensionResult(false, Array.Empty<SemanticToken>(), 0f, null);

            var probeWord = string.IsNullOrWhiteSpace(surfaceHint)
                ? LexemePhonology.ToSurface(cleaned)
                : surfaceHint.Trim().ToLowerInvariant();

            var sequence = new PhonemeSequence(probeWord, cleaned);
            _phonoLoop.Push(sequence);

            var entry = _lexicon.LookupByPhonemes(cleaned);
            if (entry == null)
            {
                _wernicke.ReceiveNeuralInput(0.02f, dt);
                _comprehensionConfidence *= 0.75f;
                return new LanguageComprehensionResult(false, Array.Empty<SemanticToken>(), _comprehensionConfidence, null);
            }

            entry.Access();
            var lexicalItem = new LexicalItem(
                Word: entry.Word,
                SemanticVector: entry.SemanticVector,
                POS: entry.PartOfSpeech,
                Confidence: 0.75f);

            var semantic = _angularGyrus.IntegrateSemantics(lexicalItem, _semantics, dt);
            var parseTree = _broca.ParseSyntax(new List<LexicalItem> { lexicalItem }, dt);
            _arcuate.Transmit(_wernicke, _broca, dt);

            _comprehensionConfidence = 0.82f;
            return new LanguageComprehensionResult(
                Success: true,
                SemanticTokens: new[] { semantic },
                Confidence: _comprehensionConfidence,
                ParseTree: parseTree);
        }
    }
    
    /// <summary>
    /// Generate language output from semantic intention.
    /// Implements the dorsal stream: meaning to articulation.
    /// </summary>
    public LanguageProductionResult GenerateOutput(SemanticIntention intention, float dt)
    {
        lock (_gate)
        {
            // === 1) SEMANTIC NETWORK: Activate relevant concepts ===
            var activatedConcepts = _semantics.ActivateFromIntention(intention, dt);
            
            // === 2) ANGULAR GYRUS: concept to lexical mapping ===
            var lexicalCandidates = new List<LexicalItem>();
            foreach (var concept in activatedConcepts)
            {
                var items = _angularGyrus.MapToLexicon(concept, _lexicon);
                lexicalCandidates.AddRange(items);
            }
            
            // === 3) BROCA'S: Syntactic planning ===
            var syntacticPlan = _broca.PlanSyntax(lexicalCandidates, intention.Structure, dt);
            
            // === 4) PHONOLOGICAL LOOP: Buffer for articulation ===
            var orderedWords = syntacticPlan.GetOrderedWords();
            foreach (var word in orderedWords)
            {
                var phonemes = _a1.EncodeWord(word);
                _phonoLoop.Push(phonemes);
            }
            
            // === 5) MOTOR CORTEX: Articulation planning ===
            var articulatoryPlan = _motorSpeech.PlanArticulation(_phonoLoop, dt);
            
            // === 6) OUTPUT ===
            string outputText = string.Join(" ", orderedWords);
            _outputBuffer.Add(outputText);
            
            _productionReadiness = syntacticPlan.Completeness;
            
            return new LanguageProductionResult(
                Text: outputText,
                Words: orderedWords,
                ArticulatoryPlan: articulatoryPlan,
                Fluency: _productionReadiness);
        }
    }
    
    /// <summary>
    /// Simple text generation from a prompt (high-level interface).
    /// </summary>
    public string Respond(string input, float dt)
    {
        lock (_gate)
        {
            // Comprehend input
            var comprehension = ProcessInput(input, dt);
            
            if (!comprehension.Success || comprehension.SemanticTokens.Length == 0)
                return "[incomprehensible]";
            
            // Build semantic intention from comprehension
            var intention = BuildResponseIntention(comprehension);
            
            // Generate response
            var production = GenerateOutput(intention, dt);
            
            return production.Text;
        }
    }
    
    /// <summary>
    /// Learn a new word with its semantic representation.
    /// </summary>
    public void LearnWord(string word, float[] semanticVector, string partOfSpeech = "noun")
    {
        lock (_gate)
        {
            var phonemes = _a1.EncodeWord(word);
            _lexicon.Add(new LexicalEntry(
                Word: word,
                Phonemes: phonemes,
                SemanticVector: semanticVector,
                PartOfSpeech: ParsePOS(partOfSpeech),
                Frequency: 1,
                LastAccess: DateTime.UtcNow));
        }
    }

    /// <summary>
    /// Bind a heard phoneme stream to a surface form so the lexicon can grow from auditory experience.
    /// </summary>
    public void LearnHeardWord(string surface, IReadOnlyList<string> phonemes, string partOfSpeech = "noun")
    {
        lock (_gate)
        {
            if (string.IsNullOrWhiteSpace(surface) || phonemes == null || phonemes.Count == 0)
                return;

            var normalized = surface.Trim().ToLowerInvariant();
            if (_lexicon.Lookup(normalized) is not null)
                return;

            var sequence = new PhonemeSequence(normalized, phonemes.Where(p => !string.IsNullOrWhiteSpace(p) && p != "_").ToArray());
            if (sequence.Phonemes.Length == 0)
                return;

            _lexicon.Add(new LexicalEntry(
                Word: normalized,
                Phonemes: sequence,
                SemanticVector: MakePhonemeVector(sequence.Phonemes),
                PartOfSpeech: ParsePOS(partOfSpeech),
                Frequency: 1,
                LastAccess: DateTime.UtcNow));
        }
    }
    
    /// <summary>
    /// Get the semantic vector for a word (if known).
    /// </summary>
    public float[]? GetWordVector(string word)
    {
        lock (_gate)
        {
            var entry = _lexicon.Lookup(word);
            return entry?.SemanticVector;
        }
    }
    
    /// <summary>
    /// Bind language system to neural activity patterns.
    /// Called from NreEngine to connect language to the lattice.
    /// </summary>
    public void BindToNeuralActivity(
        IReadOnlyList<(byte hemi, int idx, byte region)> spikes,
        float dt)
    {
        lock (_gate)
        {
            // Map neural activity to language regions
            float wernickeInput = 0f;
            float brocaInput = 0f;
            float angularInput = 0f;
            
            foreach (var spike in spikes)
            {
                // Wernicke's area: posterior Superior Temporal Gyrus (STG, region 22)
                // Also receives from Middle Temporal Gyrus (MTG, region 23) for lexical-semantic
                if (spike.region == RegionIds.SuperiorTemporalGyrus)
                    wernickeInput += 0.01f;
                if (spike.region == RegionIds.MiddleTemporalGyrus)
                    wernickeInput += 0.005f;
                
                // Broca's area: Inferior Frontal Gyrus (IFG, region 17, BA44/45)
                // Precentral gyrus (M1, region 11) provides articulatory motor output
                if (spike.region == RegionIds.InferiorFrontalGyrus)
                    brocaInput += 0.01f;
                if (spike.region == RegionIds.PrecentralGyrus)
                    brocaInput += 0.003f;
                
                // Angular gyrus (region 21) is the actual semantic hub for cross-modal integration.
                // Also frontal association (Superior Frontal Gyrus, region 15, dorsolateral PFC)
                // provides top-down semantic and executive input.
                if (spike.region == RegionIds.AngularGyrus)
                    angularInput += 0.01f;
                if (spike.region == RegionIds.SuperiorFrontalGyrus)
                    angularInput += 0.005f;
            }
            
            _wernicke.ReceiveNeuralInput(wernickeInput, dt);
            _broca.ReceiveNeuralInput(brocaInput, dt);
            _angularGyrus.ReceiveNeuralInput(angularInput, dt);
        }
    }
    
    // === PRIVATE HELPERS ===
    
    private string[] TokenizeText(string text)
    {
        // Simple whitespace + punctuation tokenization
        return text.ToLowerInvariant()
            .Split(new[] { ' ', '\t', '\n', '\r', ',', '.', '!', '?', ';', ':' },
                   StringSplitOptions.RemoveEmptyEntries);
    }
    
    private SemanticIntention BuildResponseIntention(LanguageComprehensionResult comprehension)
    {
        // Build a response intention based on what was understood
        var concepts = comprehension.SemanticTokens
            .Select(t => t.Concept)
            .Where(c => c != null)
            .ToArray();
        
        // Simple: echo/acknowledge the concepts
        return new SemanticIntention(
            Type: IntentionType.Declarative,
            Concepts: concepts!,
            Structure: SyntacticStructure.SubjectVerbObject,
            Mood: Mood.Indicative);
    }
    
    private PartOfSpeech ParsePOS(string pos)
    {
        return pos.ToLowerInvariant() switch
        {
            "noun" or "n" => PartOfSpeech.Noun,
            "verb" or "v" => PartOfSpeech.Verb,
            "adj" or "adjective" => PartOfSpeech.Adjective,
            "adv" or "adverb" => PartOfSpeech.Adverb,
            "det" or "determiner" => PartOfSpeech.Determiner,
            "prep" or "preposition" => PartOfSpeech.Preposition,
            "pron" or "pronoun" => PartOfSpeech.Pronoun,
            "conj" or "conjunction" => PartOfSpeech.Conjunction,
            _ => PartOfSpeech.Noun
        };
    }
    
    private void InitializeCoreLexicon()
    {
        // Bootstrap with function words and basic vocabulary
        var coreWords = new (string word, string pos, float[] vector)[]
        {
            // Determiners
            ("the", "det", MakeVector(0.1f, 0, 0)),
            ("a", "det", MakeVector(0.1f, 0.05f, 0)),
            ("an", "det", MakeVector(0.1f, 0.05f, 0.01f)),
            ("this", "det", MakeVector(0.15f, 0.1f, 0)),
            ("that", "det", MakeVector(0.15f, 0.1f, 0.1f)),
            
            // Pronouns
            ("i", "pron", MakeVector(0.5f, 0.8f, 0)),
            ("you", "pron", MakeVector(0.5f, 0.7f, 0.1f)),
            ("he", "pron", MakeVector(0.5f, 0.6f, 0.2f)),
            ("she", "pron", MakeVector(0.5f, 0.6f, 0.3f)),
            ("it", "pron", MakeVector(0.5f, 0.5f, 0.4f)),
            ("we", "pron", MakeVector(0.5f, 0.9f, 0)),
            ("they", "pron", MakeVector(0.5f, 0.4f, 0.5f)),
            
            // Common verbs
            ("is", "verb", MakeVector(0.2f, 0.3f, 0.8f)),
            ("are", "verb", MakeVector(0.2f, 0.3f, 0.75f)),
            ("was", "verb", MakeVector(0.2f, 0.3f, 0.7f)),
            ("be", "verb", MakeVector(0.2f, 0.3f, 0.85f)),
            ("have", "verb", MakeVector(0.3f, 0.4f, 0.6f)),
            ("do", "verb", MakeVector(0.4f, 0.5f, 0.5f)),
            ("say", "verb", MakeVector(0.6f, 0.7f, 0.3f)),
            ("think", "verb", MakeVector(0.8f, 0.6f, 0.2f)),
            ("know", "verb", MakeVector(0.75f, 0.65f, 0.25f)),
            ("see", "verb", MakeVector(0.7f, 0.3f, 0.4f)),
            ("want", "verb", MakeVector(0.6f, 0.8f, 0.1f)),
            ("come", "verb", MakeVector(0.4f, 0.2f, 0.6f)),
            ("go", "verb", MakeVector(0.4f, 0.2f, 0.7f)),
            ("make", "verb", MakeVector(0.5f, 0.6f, 0.4f)),
            ("get", "verb", MakeVector(0.45f, 0.55f, 0.45f)),
            
            // Common nouns
            ("thing", "noun", MakeVector(0.3f, 0.3f, 0.3f)),
            ("person", "noun", MakeVector(0.5f, 0.7f, 0.2f)),
            ("time", "noun", MakeVector(0.2f, 0.1f, 0.9f)),
            ("way", "noun", MakeVector(0.35f, 0.25f, 0.5f)),
            ("world", "noun", MakeVector(0.4f, 0.2f, 0.8f)),
            ("life", "noun", MakeVector(0.6f, 0.8f, 0.4f)),
            ("day", "noun", MakeVector(0.25f, 0.15f, 0.85f)),
            ("man", "noun", MakeVector(0.5f, 0.65f, 0.25f)),
            ("woman", "noun", MakeVector(0.5f, 0.65f, 0.35f)),
            ("child", "noun", MakeVector(0.5f, 0.7f, 0.15f)),
            
            // Prepositions
            ("in", "prep", MakeVector(0.1f, 0.1f, 0.5f)),
            ("on", "prep", MakeVector(0.1f, 0.15f, 0.55f)),
            ("at", "prep", MakeVector(0.1f, 0.12f, 0.52f)),
            ("to", "prep", MakeVector(0.1f, 0.2f, 0.6f)),
            ("for", "prep", MakeVector(0.15f, 0.25f, 0.5f)),
            ("with", "prep", MakeVector(0.2f, 0.3f, 0.45f)),
            ("from", "prep", MakeVector(0.1f, 0.18f, 0.58f)),
            
            // Conjunctions
            ("and", "conj", MakeVector(0.05f, 0.05f, 0.1f)),
            ("but", "conj", MakeVector(0.05f, 0.1f, 0.15f)),
            ("or", "conj", MakeVector(0.05f, 0.08f, 0.12f)),
            ("if", "conj", MakeVector(0.1f, 0.15f, 0.2f)),
            ("because", "conj", MakeVector(0.1f, 0.2f, 0.25f)),
            
            // Adjectives
            ("good", "adj", MakeVector(0.7f, 0.8f, 0.2f)),
            ("bad", "adj", MakeVector(0.3f, 0.2f, 0.2f)),
            ("new", "adj", MakeVector(0.6f, 0.5f, 0.7f)),
            ("old", "adj", MakeVector(0.4f, 0.5f, 0.3f)),
            ("big", "adj", MakeVector(0.5f, 0.4f, 0.6f)),
            ("small", "adj", MakeVector(0.5f, 0.6f, 0.4f)),
            
            // Adverbs
            ("not", "adv", MakeVector(0.1f, 0.1f, 0.2f)),
            ("very", "adv", MakeVector(0.2f, 0.3f, 0.4f)),
            ("now", "adv", MakeVector(0.15f, 0.1f, 0.9f)),
            ("here", "adv", MakeVector(0.2f, 0.15f, 0.5f)),
            ("there", "adv", MakeVector(0.2f, 0.15f, 0.6f)),
        };
        
        foreach (var (word, pos, vector) in coreWords)
        {
            LearnWord(word, vector, pos);
        }
    }
    
    private float[] MakeVector(float v1, float v2, float v3)
    {
        // Create a 128-dim vector with the first 3 values set, rest random-ish
        var vec = new float[128];
        vec[0] = v1;
        vec[1] = v2;
        vec[2] = v3;
        
        // Fill rest with derived values for structure
        for (int i = 3; i < 128; i++)
        {
            vec[i] = ((v1 + v2 + v3) / 3f) * MathF.Sin(i * 0.1f) * 0.1f;
        }
        return vec;
    }
    
    private float[] MakePhonemeVector(IReadOnlyList<string> phonemes)
    {
        var vec = new float[128];
        if (phonemes.Count == 0) return vec;

        unchecked
        {
            for (int i = 0; i < phonemes.Count; i++)
            {
                var ph = phonemes[i];
                int hash = 17;
                for (int j = 0; j < ph.Length; j++)
                    hash = hash * 31 + ph[j];

                int idx = Math.Abs(hash) % vec.Length;
                vec[idx] += 0.35f;
                vec[(idx + i * 7) % vec.Length] += 0.15f;
            }
        }

        return vec;
    }
        // === INNER CLASSES: Language Processing Regions ===
    
    /// <summary>
    /// Primary Auditory Cortex (A1): Phoneme processing
    /// Converts words to phoneme sequences
    /// </summary>
    private sealed class PrimaryAuditoryCortex
    {
        // Simple grapheme-to-phoneme mapping (English approximation)
        private static readonly Dictionary<string, string> GraphemeToPhoneme = new()
        {
            ["a"] = "ae", ["e"] = "eh", ["i"] = "ih", ["o"] = "aw", ["u"] = "uh",
            ["b"] = "b", ["c"] = "k", ["d"] = "d", ["f"] = "f", ["g"] = "g",
            ["h"] = "h", ["j"] = "jh", ["k"] = "k", ["l"] = "l", ["m"] = "m",
            ["n"] = "n", ["p"] = "p", ["q"] = "kw", ["r"] = "r", ["s"] = "s",
            ["t"] = "t", ["v"] = "v", ["w"] = "w", ["x"] = "ks", ["y"] = "j", ["z"] = "z",
            ["th"] = "th", ["sh"] = "sh", ["ch"] = "ch", ["ng"] = "ng",
            ["ee"] = "i:", ["oo"] = "u:", ["ai"] = "ay", ["ou"] = "ow",
        };
        
        public PhonemeSequence EncodeWord(string word)
        {
            var phonemes = new List<string>();
            int i = 0;
            while (i < word.Length)
            {
                // Try digraphs first
                if (i + 1 < word.Length)
                {
                    string digraph = word.Substring(i, 2);
                    if (GraphemeToPhoneme.TryGetValue(digraph, out var diPhoneme))
                    {
                        phonemes.Add(diPhoneme);
                        i += 2;
                        continue;
                    }
                }
                
                // Single character
                string ch = word[i].ToString();
                if (GraphemeToPhoneme.TryGetValue(ch, out var phoneme))
                    phonemes.Add(phoneme);
                else
                    phonemes.Add(ch); // Unknown, pass through
                i++;
            }
            
            return new PhonemeSequence(word, phonemes.ToArray());
        }
    }
    
    /// <summary>
    /// Wernicke's Area: Lexical access and comprehension
    /// </summary>
    private sealed class WernickesArea
    {
        private float _activity;
        private float _neuralInput;
        
        public LexicalItem? ProcessWord(string word, Lexicon lexicon, float dt)
        {
            var entry = lexicon.Lookup(word);
            if (entry == null)
            {
                // Unknown word - could trigger learning
                _activity *= 0.9f; // Reduced confidence
                return null;
            }
            
            // Successful lexical access
            _activity = _activity * 0.8f + 0.2f;
            entry.Access(); // Update frequency
            
            return new LexicalItem(
                Word: entry.Word,
                SemanticVector: entry.SemanticVector,
                POS: entry.PartOfSpeech,
                Confidence: _activity);
        }
        
        public void ReceiveNeuralInput(float input, float dt)
        {
            _neuralInput = _neuralInput * 0.9f + input * 0.1f;
            _activity += _neuralInput * 0.05f;
            _activity = Math.Clamp(_activity, 0f, 1f);
        }
        
        public float GetActivity() => _activity;
    }
    
    /// <summary>
    /// Broca's Area: Syntax and production planning
    /// </summary>
    private sealed class BrocasArea
    {
        private float _activity;
        private float _neuralInput;
        
        public ParseTree? ParseSyntax(List<LexicalItem> items, float dt)
        {
            if (items.Count == 0) return null;
            
            // Simple bottom-up parsing
            var nodes = items.Select(i => new ParseNode(i.POS.ToString(), i.Word)).ToList();
            
            // Try to build phrase structure
            var phrases = BuildPhrases(nodes);
            
            float coherence = phrases.Count > 0 ? 
                Math.Min(1f, phrases.Count / (float)items.Count) : 0f;
            
            _activity = _activity * 0.8f + coherence * 0.2f;
            
            return new ParseTree(phrases, coherence);
        }
        
        public SyntacticPlan PlanSyntax(List<LexicalItem> candidates, SyntacticStructure structure, float dt)
        {
            // Order words according to syntactic structure
            var ordered = new List<string>();
            
            // Simple SVO ordering (LexicalItem is a struct; use explicit found flags)
            bool hasSubject = false;
            bool hasVerb = false;
            bool hasObj = false;
            LexicalItem subject = default;
            LexicalItem verb = default;
            LexicalItem obj = default;

            int nounSeen = 0;
            for (int ci = 0; ci < candidates.Count; ci++)
            {
                var it = candidates[ci];
                if (!hasSubject && (it.POS == PartOfSpeech.Pronoun || it.POS == PartOfSpeech.Noun))
                {
                    subject = it;
                    hasSubject = true;
                }
                if (!hasVerb && it.POS == PartOfSpeech.Verb)
                {
                    verb = it;
                    hasVerb = true;
                }
                if (it.POS == PartOfSpeech.Noun)
                {
                    nounSeen++;
                    if (!hasObj && nounSeen >= 2)
                    {
                        obj = it;
                        hasObj = true;
                    }
                }

                if (hasSubject && hasVerb && hasObj) break;
            }

            if (hasSubject) ordered.Add(subject.Word);
            if (hasVerb) ordered.Add(verb.Word);
            if (hasObj) ordered.Add(obj.Word);

            // Add any remaining words
            foreach (var item in candidates)
            {
                if (!ordered.Contains(item.Word))
                    ordered.Add(item.Word);
            }
            
            float completeness = ordered.Count > 0 ? Math.Min(1f, ordered.Count / 3f) : 0f;
            
            return new SyntacticPlan(ordered.ToArray(), completeness);
        }
        
        private List<ParsePhrase> BuildPhrases(List<ParseNode> nodes)
        {
            var phrases = new List<ParsePhrase>();
            
            // Simple NP detection: Det + Adj* + Noun
            for (int i = 0; i < nodes.Count; i++)
            {
                if (nodes[i].Category == "Determiner" || nodes[i].Category == "Noun")
                {
                    var np = new List<ParseNode> { nodes[i] };
                    int j = i + 1;
                    while (j < nodes.Count && 
                           (nodes[j].Category == "Adjective" || nodes[j].Category == "Noun"))
                    {
                        np.Add(nodes[j]);
                        j++;
                    }
                    if (np.Count > 0)
                        phrases.Add(new ParsePhrase("NP", np.ToArray()));
                    i = j - 1;
                }
                else if (nodes[i].Category == "Verb")
                {
                    phrases.Add(new ParsePhrase("VP", new[] { nodes[i] }));
                }
            }
            
            return phrases;
        }
        
        public void ReceiveNeuralInput(float input, float dt)
        {
            _neuralInput = _neuralInput * 0.9f + input * 0.1f;
            _activity += _neuralInput * 0.05f;
            _activity = Math.Clamp(_activity, 0f, 1f);
        }
        
        public float GetActivity() => _activity;
    }
    
    /// <summary>
    /// Angular Gyrus: Semantic integration hub
    /// </summary>
    private sealed class AngularGyrus
    {
        private float _activity;
        private float _neuralInput;
        
        public SemanticToken IntegrateSemantics(LexicalItem item, SemanticNetwork network, float dt)
        {
            // Activate semantic network with word vector
            var concept = network.Activate(item.SemanticVector, dt);
            
            _activity = _activity * 0.85f + 0.15f;
            
            return new SemanticToken(
                Word: item.Word,
                Vector: item.SemanticVector,
                Concept: concept,
                Activation: _activity);
        }
        
        public List<LexicalItem> MapToLexicon(SemanticConcept concept, Lexicon lexicon)
        {
            // Find words that match this concept
            return lexicon.FindByVector(concept.Vector, topK: 3)
                .Select(e => new LexicalItem(e.Word, e.SemanticVector, e.PartOfSpeech, 0.8f))
                .ToList();
        }
        
        public void ReceiveNeuralInput(float input, float dt)
        {
            _neuralInput = _neuralInput * 0.9f + input * 0.1f;
            _activity += _neuralInput * 0.05f;
            _activity = Math.Clamp(_activity, 0f, 1f);
        }
    }
    
    /// <summary>
    /// Arcuate Fasciculus: White matter tract connecting Wernicke's and Broca's
    /// </summary>
    private sealed class ArcuateFasciculus
    {
        private float _transmission;
        
        public void Transmit(WernickesArea wernicke, BrocasArea broca, float dt)
        {
            // Bidirectional transmission
            float wernickeActivity = wernicke.GetActivity();
            float brocaActivity = broca.GetActivity();
            
            _transmission = (wernickeActivity + brocaActivity) * 0.5f;
            
            // The arcuate allows rehearsal and sensorimotor integration
        }
    }
    
    /// <summary>
    /// Phonological Loop: Working memory for verbal material (Baddeley model)
    /// </summary>
    private sealed class PhonologicalLoop
    {
        private readonly int _capacity;
        private readonly Queue<PhonemeSequence> _buffer = new();
        
        public PhonologicalLoop(int capacity) => _capacity = capacity;
        
        public int Count => _buffer.Count;
        
        public void Push(PhonemeSequence seq)
        {
            _buffer.Enqueue(seq);
            while (_buffer.Count > _capacity)
                _buffer.Dequeue();
        }
        
        public PhonemeSequence? Pop()
        {
            return _buffer.Count > 0 ? _buffer.Dequeue() : null;
        }
        
        public PhonemeSequence[] GetAll() => _buffer.ToArray();
    }
    
    /// <summary>
    /// Semantic Network: Distributed concept representations
    /// </summary>
    private sealed class SemanticNetwork
    {
        private readonly int _dimensions;
        private readonly Dictionary<int, SemanticConcept> _concepts = new();
        private readonly List<int> _activeNodes = new();
        private int _nextId;
        
        public SemanticNetwork(int dimensions) => _dimensions = dimensions;
        
        public int ActiveNodeCount => _activeNodes.Count;
        
        public SemanticConcept Activate(float[] vector, float dt)
        {
            // Find or create concept for this vector
            var closest = FindClosest(vector);
            if (closest != null && Similarity(closest.Vector, vector) > 0.8f)
            {
                closest.Activation = Math.Min(1f, closest.Activation + 0.1f);
                if (!_activeNodes.Contains(closest.Id))
                    _activeNodes.Add(closest.Id);
                return closest;
            }
            
            // Create new concept
            var concept = new SemanticConcept(_nextId++, vector, 0.5f);
            _concepts[concept.Id] = concept;
            _activeNodes.Add(concept.Id);
            
            // Decay old activations
            for (int i = _activeNodes.Count - 1; i >= 0; i--)
            {
                if (_concepts.TryGetValue(_activeNodes[i], out var c))
                {
                    c.Activation *= 0.95f;
                    if (c.Activation < 0.1f)
                        _activeNodes.RemoveAt(i);
                }
            }
            
            return concept;
        }
        
        public SemanticConcept[] ActivateFromIntention(SemanticIntention intention, float dt)
        {
            var result = new List<SemanticConcept>();
            foreach (var concept in intention.Concepts)
            {
                var activated = Activate(concept.Vector, dt);
                result.Add(activated);
            }
            return result.ToArray();
        }
        
        private SemanticConcept? FindClosest(float[] vector)
        {
            SemanticConcept? best = null;
            float bestSim = 0f;
            
            foreach (var concept in _concepts.Values)
            {
                float sim = Similarity(concept.Vector, vector);
                if (sim > bestSim)
                {
                    bestSim = sim;
                    best = concept;
                }
            }
            return best;
        }
        
        private float Similarity(float[] a, float[] b)
        {
            // Cosine similarity
            float dot = 0f, normA = 0f, normB = 0f;
            int len = Math.Min(a.Length, b.Length);
            for (int i = 0; i < len; i++)
            {
                dot += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }
            if (normA < 1e-9f || normB < 1e-9f) return 0f;
            return dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
        }
    }
    
    /// <summary>
    /// Motor Cortex for speech: Articulation planning
    /// </summary>
    private sealed class ArticulatoryMotorCortex
    {
        public ArticulatoryPlan PlanArticulation(PhonologicalLoop phonoLoop, float dt)
        {
            var sequences = phonoLoop.GetAll();
            var gestures = new List<ArticulatoryGesture>();
            
            foreach (var seq in sequences)
            {
                foreach (var phoneme in seq.Phonemes)
                {
                    gestures.Add(new ArticulatoryGesture(phoneme, GetDuration(phoneme)));
                }
                // Word boundary pause
                gestures.Add(new ArticulatoryGesture("_", 50));
            }
            
            return new ArticulatoryPlan(gestures.ToArray());
        }
        
        private int GetDuration(string phoneme)
        {
            // Duration in milliseconds (simplified)
            return phoneme switch
            {
                "i:" or "u:" or "a:" => 150, // Long vowels
                "ae" or "eh" or "ih" or "aw" or "uh" => 100, // Short vowels
                "p" or "t" or "k" => 80, // Stops
                "s" or "z" or "f" or "v" => 120, // Fricatives
                _ => 80
            };
        }
    }
    
    /// <summary>
    /// Lexicon: Word storage with semantic vectors
    /// </summary>
    private sealed class Lexicon
    {
        private readonly Dictionary<string, LexicalEntry> _entries = new();
        private readonly int _capacity;
        
        public Lexicon(int capacity) => _capacity = capacity;
        
        public int Size => _entries.Count;
        
        public void Add(LexicalEntry entry)
        {
            _entries[entry.Word.ToLowerInvariant()] = entry;
            
            // Evict least-used if over capacity
            if (_entries.Count > _capacity)
            {
                var leastUsed = _entries.Values
                    .OrderBy(e => e.Frequency)
                    .ThenBy(e => e.LastAccess)
                    .First();
                _entries.Remove(leastUsed.Word);
            }
        }
        
        public LexicalEntry? Lookup(string word)
        {
            return _entries.TryGetValue(word.ToLowerInvariant(), out var entry) ? entry : null;
        }

        public LexicalEntry? LookupByPhonemes(IReadOnlyList<string> phonemes)
        {
            LexicalEntry? best = null;
            float bestScore = 0f;

            foreach (var entry in _entries.Values)
            {
                float score = PhonemeSimilarity(entry.Phonemes.Phonemes, phonemes);
                if (score > bestScore)
                {
                    bestScore = score;
                    best = entry;
                }
            }

            return bestScore >= 0.72f ? best : null;
        }
        
        public List<LexicalEntry> FindByVector(float[] vector, int topK)
        {
            return _entries.Values
                .Select(e => (entry: e, sim: CosineSimilarity(e.SemanticVector, vector)))
                .OrderByDescending(x => x.sim)
                .Take(topK)
                .Select(x => x.entry)
                .ToList();
        }
        
        private float CosineSimilarity(float[] a, float[] b)
        {
            float dot = 0f, normA = 0f, normB = 0f;
            int len = Math.Min(a.Length, b.Length);
            for (int i = 0; i < len; i++)
            {
                dot += a[i] * b[i];
                normA += a[i] * a[i];
                normB += b[i] * b[i];
            }
            if (normA < 1e-9f || normB < 1e-9f) return 0f;
            return dot / (MathF.Sqrt(normA) * MathF.Sqrt(normB));
        }

        private static float PhonemeSimilarity(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            if (a.Count == 0 || b.Count == 0)
                return 0f;

            int maxLen = Math.Max(a.Count, b.Count);
            int minLen = Math.Min(a.Count, b.Count);
            int matches = 0;

            for (int i = 0; i < minLen; i++)
            {
                if (string.Equals(a[i], b[i], StringComparison.Ordinal))
                    matches++;
            }

            return matches / (float)maxLen;
        }
    }
}

// === SUPPORTING TYPES ===

public sealed class LexicalEntry
{
    public string Word { get; }
    public PhonemeSequence Phonemes { get; }
    public float[] SemanticVector { get; }
    public PartOfSpeech PartOfSpeech { get; }
    public int Frequency { get; private set; }
    public DateTime LastAccess { get; private set; }
    
    public LexicalEntry(string Word, PhonemeSequence Phonemes, float[] SemanticVector,
        PartOfSpeech PartOfSpeech, int Frequency, DateTime LastAccess)
    {
        this.Word = Word;
        this.Phonemes = Phonemes;
        this.SemanticVector = SemanticVector;
        this.PartOfSpeech = PartOfSpeech;
        this.Frequency = Frequency;
        this.LastAccess = LastAccess;
    }
    
    public void Access()
    {
        Frequency++;
        LastAccess = DateTime.UtcNow;
    }
}

public readonly record struct PhonemeSequence(string Word, string[] Phonemes);
public readonly record struct PhonemeEvent(string Phoneme, float Time);
public readonly record struct LexicalItem(string Word, float[] SemanticVector, PartOfSpeech POS, float Confidence);
public readonly record struct SemanticToken(string Word, float[] Vector, SemanticConcept? Concept, float Activation);
public readonly record struct ArticulatoryGesture(string Phoneme, int DurationMs);
public readonly record struct ArticulatoryPlan(ArticulatoryGesture[] Gestures);

public sealed class SemanticConcept
{
    public int Id { get; }
    public float[] Vector { get; }
    public float Activation { get; set; }
    
    public SemanticConcept(int id, float[] vector, float activation)
    {
        Id = id;
        Vector = vector;
        Activation = activation;
    }
}

public sealed class ParseTree
{
    public List<ParsePhrase> Phrases { get; }
    public float Coherence { get; }
    
    public ParseTree(List<ParsePhrase> phrases, float coherence)
    {
        Phrases = phrases;
        Coherence = coherence;
    }
}

public readonly record struct ParseNode(string Category, string Word);
public readonly record struct ParsePhrase(string Type, ParseNode[] Nodes);

public sealed class SyntacticPlan
{
    private readonly string[] _words;
    public float Completeness { get; }
    
    public SyntacticPlan(string[] words, float completeness)
    {
        _words = words;
        Completeness = completeness;
    }
    
    public string[] GetOrderedWords() => _words;
}

public sealed class SemanticIntention
{
    public IntentionType Type { get; }
    public SemanticConcept[] Concepts { get; }
    public SyntacticStructure Structure { get; }
    public Mood Mood { get; }
    
    public SemanticIntention(IntentionType Type, SemanticConcept[] Concepts,
        SyntacticStructure Structure, Mood Mood)
    {
        this.Type = Type;
        this.Concepts = Concepts;
        this.Structure = Structure;
        this.Mood = Mood;
    }
}

public enum PartOfSpeech
{
    Noun, Verb, Adjective, Adverb, Determiner, Preposition, Pronoun, Conjunction, Interjection
}

public enum IntentionType
{
    Declarative, Interrogative, Imperative, Exclamatory
}

public enum SyntacticStructure
{
    SubjectVerbObject, SubjectVerb, VerbObject, Intransitive
}

public enum Mood
{
    Indicative, Subjunctive, Imperative, Conditional
}

// === RESULT TYPES ===

public readonly record struct LanguageState(
    int LexiconSize,
    int PhonologicalBufferCount,
    int ActiveSemanticNodes,
    float ComprehensionConfidence,
    float ProductionReadiness,
    float WernickeActivity,
    float BrocaActivity,
    int PendingOutputWords);

public readonly record struct LanguageComprehensionResult(
    bool Success,
    SemanticToken[] SemanticTokens,
    float Confidence,
    ParseTree? ParseTree);

public readonly record struct LanguageProductionResult(
    string Text,
    string[] Words,
    ArticulatoryPlan ArticulatoryPlan,
    float Fluency);
