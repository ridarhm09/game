using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class BerburuHurufQuestion
{
    [Tooltip("Nama untuk memudahkan pengecekan di Inspector.")]
    public string questionName;

    [Tooltip("Field lama; tidak digunakan pada mode server-only.")]
    public string answerWord;

    [Tooltip("GameObject template soal.")]
    public GameObject questionObject;

    [Tooltip("Daftar PopupLetterSlot khusus template ini.")]
    public List<PopupLetterSlot> letterSlots =
        new List<PopupLetterSlot>();
}

public class BerburuQuestionController : MonoBehaviour
{
    [Header("Panel Berburu Huruf")]
    public GameObject berburuHurufObject;
    // Provider for local letter sprites (offline mode)
    private LetterSpriteProvider _letterProvider;

    [Header("Controller")]
    public MiniGameAnswerController miniGameAnswerController;
    public AudioManagerDonimal audioManager;

    [Header("Daftar Template Soal")]
    [Tooltip(
        "Template dipilih berdasarkan jumlah slot. " +
        "Contoh kata 4 huruf membutuhkan template 4 slot."
    )]
    public List<BerburuHurufQuestion> questions =
        new List<BerburuHurufQuestion>();

    [Header("Feedback")]
    public GameObject correctFeedback;
    public GameObject wrongFeedback;
    public GameObject completedFeedback;

    [Header("Gambar Jawaban (nama file sesuai kata)")]
    public Texture2D[] answerImages;

    public float feedbackDuration = 1.5f;
    public float completionDelay = 3f;

    [Header("Testing")]
    public bool enableKeyboardTest = false;

    private string currentWord = "";
    private string[] currentSequence = null;
    private string currentBuiltAnswer = "";
    private int currentStep = 0;

    private List<PopupLetterSlot> currentLetterSlots =
        new List<PopupLetterSlot>();

    private bool gameCompleted = false;
    private bool questionActive = false;

    private Coroutine feedbackCoroutine;
    private Coroutine completionCoroutine;

    private readonly List<GameQuestionData> serverQuestions =
        new List<GameQuestionData>();

    private bool isFetchingQuestions = false;
    private GameQuestionData currentServerQuestion;
    private GameQuestionData lastPresentedServerQuestion;
    private int requestVersion;
    private bool completedImageReady;

    private void Start()
    {
        // Existing init code ...
        // Find LetterSpriteProvider in the scene (or create one if missing)
        _letterProvider = FindFirstObjectByType<LetterSpriteProvider>();
        if (_letterProvider == null)
        {
            Debug.LogWarning("[Berburu] LetterSpriteProvider not found – falling back to server images.");
        }

        // Original initialization logic
        if (miniGameAnswerController == null)
        {
            miniGameAnswerController = FindFirstObjectByType<MiniGameAnswerController>(FindObjectsInactive.Include);
        }

        if (audioManager == null)
        {
            audioManager = FindFirstObjectByType<AudioManagerDonimal>(FindObjectsInactive.Include);
        }

        HideAllQuestionObjects();
        HideAllFeedback();

        questionActive = false;
        gameCompleted = false;
    }

    private void OnDisable()
    {
        StopQuestion();
    }

    private void Update()
    {
        if (!enableKeyboardTest)
        {
            return;
        }

        // Hindari satu tombol dibaca dua kali. Jika controller global aktif,
        // seluruh input keyboard ditangani oleh MiniGameAnswerController.
        if (miniGameAnswerController != null &&
            miniGameAnswerController.enableKeyboardInput)
        {
            return;
        }

        if (!questionActive || gameCompleted)
        {
            return;
        }

        if (
            berburuHurufObject != null &&
            !berburuHurufObject.activeInHierarchy
        )
        {
            return;
        }

        for (
            KeyCode key = KeyCode.A;
            key <= KeyCode.Z;
            key++
        )
        {
            if (KeyboardInputUtility.WasPressed(key))
            {
                char pressedLetter =
                    (char)(
                        'a' +
                        (key - KeyCode.A)
                    );

                CheckLetter(
                    "huruf:" + pressedLetter
                );

                return;
            }
        }
    }

    public void StartQuestion()
    {
        
        ScoreManager.Instance?.UseDefaultQuestionPoint();

        StopRunningCoroutines();
        requestVersion++;
        StopQuestionAudio();

        HideAllFeedback();
        HideAllQuestionObjects();

        questionActive = false;
        gameCompleted = false;

        currentStep = 0;
        currentBuiltAnswer = "";
        currentSequence = null;
        currentWord = "";
        completedImageReady = false;

        if (
            serverQuestions.Count == 0 &&
            !isFetchingQuestions
        )
        {
            FetchQuestionsFromServer();
            return;
        }

        if (serverQuestions.Count > 0)
        {
            SetupDynamicQuestion();
        }
    }

    private void FetchQuestionsFromServer()
    {
        if (isFetchingQuestions)
        {
            return;
        }

        isFetchingQuestions = true;

        Debug.Log(
            "[Berburu] Fetching questions from server..."
        );

        if (DonimalAPI.Instance == null)
        {
            Debug.LogError(
                "[Berburu] DonimalAPI tidak ditemukan. Mode server-only aktif."
            );

            isFetchingQuestions = false;
            HandleServerFailure();
            return;
        }

        DonimalAPI.Instance.GetRandomQuestions(
            "berburu_huruf",
            50,

            onSuccess: (data) =>
            {
                isFetchingQuestions = false;

                serverQuestions.Clear();

                if (data != null)
                {
                    serverQuestions.AddRange(data);
                }

                if (serverQuestions.Count == 0)
                {
                    Debug.LogError(
                        "[Berburu] Server merespons tetapi soal kosong."
                    );

                    HandleServerFailure();
                    return;
                }

                SetupDynamicQuestion();
            },

            onError: (error) =>
            {
                Debug.LogError(
                    "[Berburu] Gagal fetch soal: " +
                    error
                );

                isFetchingQuestions = false;
                HandleServerFailure();
            }
        );
    }

    private void SetupDynamicQuestion()
    {
        if (serverQuestions.Count == 0)
        {
            HandleServerFailure();
            return;
        }

        // Koreksi hasil backend yang merusak jawaban menjadi 1 huruf saja, 
        // dengan cara parsing filename langsung dari medianya.
        foreach (GameQuestionData question in serverQuestions)
        {
            if (question == null || string.IsNullOrEmpty(question.media_url)) continue;

            string url = question.media_url;
            int lastSlash = url.LastIndexOf('/');
            int lastDot = url.LastIndexOf('.');
            if (lastSlash >= 0 && lastDot > lastSlash) 
            {
                string filename = url.Substring(lastSlash + 1, lastDot - lastSlash - 1);
                question.correct_answer = filename.ToUpper();
            }

            // Ganti IBU menjadi MAMA agar sesuai jumlah slot (dan mem-bypass ID yang berubah)
            if (question.correct_answer != null && question.correct_answer.ToUpper() == "IBU")
            {
                question.correct_answer = "MAMA";
            }
        }

        // Hanya pilih soal yang benar-benar dapat ditampilkan oleh template
        // yang tersedia di Inspector. Sebelumnya soal 5+ huruf dapat terpilih
        // walaupun Scene hanya mempunyai template 3 dan 4 slot, sehingga
        // semua questionObject tetap tersembunyi dan layar terlihat kosong.
        List<GameQuestionData> compatibleQuestions =
            serverQuestions.FindAll(IsServerQuestionCompatible);

        if (compatibleQuestions.Count == 0)
        {
            foreach (GameQuestionData serverQuestion in serverQuestions)
            {
                string answer = serverQuestion == null
                    ? "<null>"
                    : NormalizeWord(serverQuestion.correct_answer);
                Debug.LogError(
                    "[Berburu] Soal ditolak | correct_answer=" + answer +
                    " | jumlah huruf=" + answer.Length +
                    " | media_url=" +
                    (serverQuestion == null ? "<null>" : serverQuestion.media_url)
                );
            }

            Debug.LogError(
                "[Berburu] Tidak ada soal server yang cocok dengan " +
                "template slot di Inspector. Mode server-only aktif."
            );

            HandleServerFailure();
            return;
        }

        currentServerQuestion =
            compatibleQuestions[
                Random.Range(0, compatibleQuestions.Count)
            ];
        lastPresentedServerQuestion = currentServerQuestion;

        // Poin soal mengikuti game_questions.score_point dari server.
        ScoreManager.Instance?.SetCurrentQuestionPoint(
            currentServerQuestion.score_point
        );

        currentWord =
            NormalizeWord(
                currentServerQuestion.correct_answer
            );

        if (string.IsNullOrEmpty(currentWord))
        {
            Debug.LogError(
                "[Berburu] correct_answer kosong. Mode server-only aktif."
            );

            HandleServerFailure();
            return;
        }

        // ==================================================
        // OPTION_DATA.SEQUENCE
        // ==================================================

        // Sumber jawaban tunggal adalah correct_answer dari server.
        // option_data.sequence tidak dipakai karena pada sebagian record
        // isinya bukan urutan huruf dari jawaban.
        currentSequence = BuildSequenceFromWord(currentWord);

        if (
            currentSequence == null ||
            currentSequence.Length == 0 ||
            System.Array.Exists(
                currentSequence,
                letter => string.IsNullOrEmpty(letter) || letter.Length != 1
            )
        )
        {
            Debug.LogError(
                "[Berburu] Sequence tidak valid. Mode server-only aktif."
            );

            HandleServerFailure();
            return;
        }

        string sequenceAsWord =
            string.Join(
                "",
                currentSequence
            );

        if (
            NormalizeWord(sequenceAsWord) !=
            currentWord
        )
        {
            Debug.LogError(
                "[Berburu] Urutan huruf tidak sama dengan correct_answer. " +
                "Sequence=" +
                sequenceAsWord +
                ", correct_answer=" +
                currentWord
            );

            HandleServerFailure();
            return;
        }

        BerburuHurufQuestion template = FindTemplateForLength(currentSequence.Length);

        if (template == null)
        {
            Debug.LogError(
                "[Berburu] Tidak ada template dengan " +
                currentSequence.Length +
                " slot. Mode server-only aktif."
            );

            HandleServerFailure();
            return;
        }

        // Daftar Inspector bisa hanya memuat tiga slot meski objek template punya empat.
        // Bersihkan juga slot yang tidak tercantum agar huruf soal lama tidak tertinggal.
        if (template.questionObject != null)
        {
            foreach (PopupLetterSlot sceneSlot in template.questionObject.GetComponentsInChildren<PopupLetterSlot>(true))
            {
                sceneSlot.ResetSlot();
                sceneSlot.gameObject.SetActive(false);
            }
        }

        // Aktifkan hanya slot yang diperlukan oleh jawaban saat ini.
        for (int i = 0; i < template.letterSlots.Count; i++)
        {
            PopupLetterSlot templateSlot = template.letterSlots[i];
            if (templateSlot != null)
                templateSlot.gameObject.SetActive(i < currentSequence.Length);
        }

        currentLetterSlots = template.letterSlots.GetRange(0, currentSequence.Length);

        currentStep = 0;
        currentBuiltAnswer = "";
        gameCompleted = false;
        questionActive = false;

        // Set isi setiap slot dari sequence server.
        for (
            int i = 0;
            i < currentLetterSlots.Count;
            i++
        )
        {
            PopupLetterSlot slot =
                currentLetterSlots[i];

            if (slot == null)
            {
                continue;
            }

            slot.Setup(
                currentSequence[i]
            );

            LoadLetterSlotImageFromLearningTable(
                slot,
                currentSequence[i],
                requestVersion
            );
        }

        if (template.questionObject != null)
        {
            template.questionObject.SetActive(true);
        }

        LoadServerImage(
            template.questionObject
        );

        PlayQuestionAudio();

        questionActive = true;

        Debug.Log(
            "[Berburu] Target: " +
            currentWord.ToUpper()
        );

        Debug.Log(
            "[Berburu] Sequence: " +
            string.Join(
                " -> ",
                currentSequence
            ).ToUpper()
        );
    }

    private void LoadCompletedImageFromLearningTable(string word, int version)
    {
        EnsureCompletedFeedbackGraphic();
        if (answerImages != null)
        {
            foreach (Texture2D texture in answerImages)
            {
                if (texture == null || !string.Equals(texture.name, word,
                    System.StringComparison.OrdinalIgnoreCase)) continue;
                completedImageReady = ServerQuestionImageUtility.ApplyTexture(
                    completedFeedback.transform.Find("AnswerImage").gameObject,
                    texture, "[Berburu Jawaban]");
                if (completedImageReady) return;
            }
        }

        if (DonimalAPI.Instance == null) return;
        EnsureCompletedFeedbackGraphic();

        DonimalAPI.Instance.GetLearningMaterial(word,
            material =>
            {
                if (version != requestVersion) return;
                if (material == null || string.IsNullOrWhiteSpace(material.media_url))
                {
                    Debug.LogError("[Berburu] Server tabel belajar tidak mengirim media_url untuk " + word + ".");
                    return;
                }

                if (!UrlMatchesWord(material.media_url, word))
                {
                    Debug.LogError("[Berburu] media_url tabel belajar tidak sesuai kata " +
                                     word + ": " + material.media_url +
                                     ". Perbaiki data pada server.");
                    return;
                }

                DownloadCompletedImage(material.media_url, word, version);
            },
            error =>
            {
                if (version == requestVersion)
                {
                    Debug.LogError("[Berburu] Gagal membaca tabel belajar: " + error);
                }
            });
    }

    private void LoadLetterSlotImageFromLearningTable(
        PopupLetterSlot slot,
        string letter,
        int version)
    {
        if (slot == null || string.IsNullOrWhiteSpace(letter))
            return;

        // 1️⃣ Try local sprite provider first (offline mode)
        if (_letterProvider != null)
        {
            Sprite localSprite = _letterProvider.GetSprite(letter);
            if (localSprite != null)
            {
                // Apply sprite to the slot's Image component if present
                Image img = slot.GetComponentInChildren<Image>();
                if (img != null)
                {
                    img.sprite = localSprite;
                    Debug.Log("[Berburu] Loaded local sprite for letter " + letter);
                }
                else
                {
                    // Fallback: try RawImage
                    RawImage raw = slot.GetComponentInChildren<RawImage>();
                    if (raw != null)
                    {
                        raw.texture = localSprite.texture;
                        Debug.Log("[Berburu] Loaded local sprite texture for letter " + letter);
                    }
                }
                return; // success – skip server fetch
            }
        }

        // 2️⃣ If no local sprite, fall back to original server‑learning‑material logic
        if (DonimalAPI.Instance == null) return;
        DonimalAPI.Instance.GetLearningMaterial(letter,
            material =>
            {
                if (version != requestVersion || slot == null) return;
                if (material == null || string.IsNullOrWhiteSpace(material.media_url))
                {
                    Debug.LogError("[Berburu] Tabel belajar tidak mengirim media_url huruf " +
                                   letter.ToUpperInvariant() + ".");
                    return;
                }

                if (!UrlMatchesLearningValue(material.media_url, letter))
                {
                    Debug.LogError("[Berburu] media_url tabel belajar tidak sesuai huruf " +
                                   letter.ToUpperInvariant() + ": " + material.media_url);
                    return;
                }

                DonimalAPI.Instance.GetTexture(material.media_url,
                    texture =>
                    {
                        if (version != requestVersion || slot == null) return;
                        if (ServerQuestionImageUtility.ApplyTexture(
                                slot.gameObject, texture,
                                "[Berburu Huruf " + letter.ToUpperInvariant() + "]"))
                        {
                            Debug.Log("[Berburu] Gambar huruf siap: " +
                                      letter.ToUpperInvariant());
                        }
                    },
                    error =>
                    {
                        if (version == requestVersion)
                            Debug.LogError("[Berburu] Gagal download gambar huruf " +
                                           letter.ToUpperInvariant() + ": " + error +
                                           " | media_url: " + material.media_url);
                    });
            },
            error =>
            {
                if (version == requestVersion)
                    Debug.LogError("[Berburu] Gagal membaca tabel belajar untuk huruf " +
                                   letter.ToUpperInvariant() + ": " + error);
            });
    }

    private void EnsureCompletedFeedbackGraphic()
    {
        if (completedFeedback == null)
        {
            completedFeedback = new GameObject("CompletedAnswer", typeof(RectTransform));
            completedFeedback.transform.SetParent(
                berburuHurufObject != null ? berburuHurufObject.transform : transform, false);
            RectTransform panel = completedFeedback.GetComponent<RectTransform>();
            panel.anchorMin = Vector2.zero;
            panel.anchorMax = Vector2.one;
            panel.offsetMin = panel.offsetMax = Vector2.zero;
            completedFeedback.SetActive(false);
        }
        Transform existing = completedFeedback.transform.Find("AnswerImage");
        if (existing != null &&
            (existing.GetComponent<RawImage>() != null || existing.GetComponent<Image>() != null)) return;

        GameObject imageObject = new GameObject(
            "AnswerImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        imageObject.transform.SetParent(completedFeedback.transform, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(300f, 300f);
        imageObject.GetComponent<RawImage>().raycastTarget = false;
    }

    private void DownloadCompletedImage(string mediaUrl, string word, int version)
    {
        if (DonimalAPI.Instance == null) return;

        DonimalAPI.Instance.GetTexture(mediaUrl,
            texture =>
            {
                if (version != requestVersion) return;
                completedImageReady = ServerQuestionImageUtility.ApplyTexture(
                    completedFeedback.transform.Find("AnswerImage").gameObject,
                    texture, "[Berburu Jawaban]");
                if (completedImageReady)
                    Debug.Log("[Berburu] Gambar jawaban tabel belajar siap: " + word);
            },
            error =>
            {
                if (version != requestVersion) return;
                Debug.LogError("[Berburu] Gagal download gambar jawaban: " + error +
                               " | media_url server: " + mediaUrl);
            });
    }

    private static bool UrlMatchesWord(string mediaUrl, string word)
    {
        if (string.IsNullOrWhiteSpace(mediaUrl) || string.IsNullOrWhiteSpace(word)) return false;

        string normalizedUrl = mediaUrl.Replace("%20", " ").ToLowerInvariant();
        string normalizedWord = word.Trim().ToLowerInvariant();
        return normalizedUrl.Contains(normalizedWord);
    }

    private static bool UrlMatchesLearningValue(string mediaUrl, string value)
    {
        if (string.IsNullOrWhiteSpace(mediaUrl) || string.IsNullOrWhiteSpace(value))
            return false;

        string normalizedUrl = mediaUrl.Replace("%20", " ").Replace("\\", "/");
        int queryIndex = normalizedUrl.IndexOf('?');
        if (queryIndex >= 0) normalizedUrl = normalizedUrl.Substring(0, queryIndex);
        int slashIndex = normalizedUrl.LastIndexOf('/');
        string fileName = slashIndex >= 0
            ? normalizedUrl.Substring(slashIndex + 1)
            : normalizedUrl;
        int extensionIndex = fileName.LastIndexOf('.');
        if (extensionIndex > 0) fileName = fileName.Substring(0, extensionIndex);

        return string.Equals(
            fileName.Trim(), value.Trim(), System.StringComparison.OrdinalIgnoreCase);
    }

    private bool IsServerQuestionCompatible(GameQuestionData question)
    {
        if (question == null)
        {
            return false;
        }

        string word = NormalizeWord(question.correct_answer);

        if (string.IsNullOrEmpty(word) || string.IsNullOrWhiteSpace(question.media_url))
        {
            return false;
        }

        string[] sequence = BuildSequenceFromWord(word);

        if (
            sequence == null ||
            sequence.Length == 0 ||
            System.Array.Exists(
                sequence,
                letter => string.IsNullOrEmpty(letter) || letter.Length != 1
            ) ||
            NormalizeWord(string.Join("", sequence)) != word
        )
        {
            return false;
        }

        return FindTemplateForLength(sequence.Length) != null;
    }

    private BerburuHurufQuestion FindTemplateForLength(int letterCount)
    {
        if (questions != null)
        {
            BerburuHurufQuestion configured = null;
            foreach (BerburuHurufQuestion candidate in questions)
            {
                if (candidate == null || candidate.letterSlots == null ||
                    candidate.letterSlots.Count < letterCount ||
                    !candidate.letterSlots.TrueForAll(slot => slot != null)) continue;

                if (configured == null ||
                    candidate.letterSlots.Count < configured.letterSlots.Count)
                    configured = candidate;
            }

            if (configured != null)
            {
                if (configured.questionObject == null && configured.letterSlots.Count > 0)
                    configured.questionObject = ResolveTemplateRoot(configured.letterSlots[0].transform);
                return configured.questionObject != null ? configured : null;
            }
        }

        if (berburuHurufObject == null) return null;

        PopupLetterSlot[] sceneSlots =
            berburuHurufObject.GetComponentsInChildren<PopupLetterSlot>(true);
        Dictionary<GameObject, List<PopupLetterSlot>> slotsByTemplate =
            new Dictionary<GameObject, List<PopupLetterSlot>>();

        foreach (PopupLetterSlot slot in sceneSlots)
        {
            if (slot == null) continue;
            GameObject root = ResolveTemplateRoot(slot.transform);
            if (root == null) continue;
            if (!slotsByTemplate.ContainsKey(root))
                slotsByTemplate[root] = new List<PopupLetterSlot>();
            slotsByTemplate[root].Add(slot);
        }

        KeyValuePair<GameObject, List<PopupLetterSlot>>? bestEntry = null;
        foreach (KeyValuePair<GameObject, List<PopupLetterSlot>> entry in slotsByTemplate)
        {
            if (entry.Value.Count < letterCount) continue;
            if (!bestEntry.HasValue || entry.Value.Count < bestEntry.Value.Value.Count)
                bestEntry = entry;
        }

        if (bestEntry.HasValue)
        {
            KeyValuePair<GameObject, List<PopupLetterSlot>> entry = bestEntry.Value;
            entry.Value.Sort((a, b) =>
                a.transform.GetSiblingIndex().CompareTo(b.transform.GetSiblingIndex()));
            Debug.LogWarning("[Berburu] Template " + entry.Value.Count +
                             " slot dipakai untuk kata " + letterCount +
                             " huruf pada " + entry.Key.name + ".");
            return new BerburuHurufQuestion
            {
                questionName = entry.Key.name,
                questionObject = entry.Key,
                letterSlots = entry.Value
            };
        }

        return null;
    }

    private GameObject ResolveTemplateRoot(Transform slotTransform)
    {
        if (slotTransform == null || berburuHurufObject == null) return null;
        Transform root = berburuHurufObject.transform;
        Transform current = slotTransform;
        while (current.parent != null && current.parent != root)
            current = current.parent;
        return current != root && current.parent == root ? current.gameObject : null;
    }

    private void HandleServerFailure()
    {
        questionActive = false;
        gameCompleted = false;
        currentStep = 0;
        currentBuiltAnswer = "";
        currentSequence = null;
        currentWord = "";
        currentLetterSlots = null;
        currentServerQuestion = null;
        StopQuestionAudio();
        HideAllFeedback();
        HideAllQuestionObjects();
    }

    public void CheckLetter(string inputData)
    {
        if (!questionActive || gameCompleted)
        {
            return;
        }

        if (
            berburuHurufObject != null &&
            !berburuHurufObject.activeInHierarchy
        )
        {
            return;
        }

        string inputLetter =
            NormalizeLetterInput(
                inputData
            );

        if (
            string.IsNullOrEmpty(inputLetter) ||
            inputLetter.Length != 1
        )
        {
            return;
        }

        if (
            currentSequence == null ||
            currentStep >=
            currentSequence.Length
        )
        {
            return;
        }

        string expected =
            NormalizeLetterInput(
                currentSequence[currentStep]
            );

        Debug.Log(
            "[Berburu] Input: " +
            inputLetter.ToUpper() +
            " | Expected: " +
            expected.ToUpper() +
            " | currentStep before++: " + currentStep +
            " | sequence length: " + currentSequence.Length +
            " | target word: " + currentWord
        );

        // Remove the immediate inputLetter != expected check
        if (
            currentStep <
            currentLetterSlots.Count &&
            currentLetterSlots[currentStep] != null
        )
        {
            // If they tapped a wrong letter, display it as text instead of the correct preload
            if (inputLetter != expected)
            {
                currentLetterSlots[currentStep].Setup(inputLetter);
            }

            currentLetterSlots[
                currentStep
            ].Reveal();
        }

        currentBuiltAnswer +=
            inputLetter;

        currentStep++;

        if (
            currentStep <
            currentSequence.Length
        )
        {
            Debug.Log("[Berburu] BUKAN HURUF TERAKHIR, menunggu input lanjutan. (currentStep=" + currentStep + " \u003c length=" + currentSequence.Length + ")");
            return;
        }

        Debug.Log("[Berburu] HURUF TERAKHIR DICAPAI! Mengevaluasi kecocokan seluruh kata...");

        // Evaluate at the end
        string finalAnswer =
            NormalizeWord(
                currentBuiltAnswer
            );

        if (finalAnswer != currentWord)
        {
            Debug.LogError(
                "[Berburu] Sequence selesai tetapi hasil akhir salah: " +
                finalAnswer +
                " harusnya " +
                currentWord
            );

            DockingSerialReader.Instance?.SendAnswerLed(false);

            ShowFeedback(wrongFeedback);

            // Reset state so they can try again
            currentStep = 0;
            currentBuiltAnswer = "";
            for (int i = 0; i < currentLetterSlots.Count; i++)
            {
                if (currentLetterSlots[i] != null)
                {
                    // Re-setup with correct sequence character so the text resets
                    currentLetterSlots[i].Setup(currentSequence[i].ToString());
                }
            }

            return;
        }

        gameCompleted = true;
        questionActive = false;

        StopRunningCoroutines();
        StopQuestionAudio();
        HideAllFeedback();
        completionCoroutine = StartCoroutine(WaitBeforeCompletionFeedback());
    }

    private void LoadServerImage(
        GameObject targetObject
    )
    {
        if (
            currentServerQuestion == null ||
            string.IsNullOrWhiteSpace(
                currentServerQuestion.media_url
            ) ||
            DonimalAPI.Instance == null ||
            targetObject == null
        )
        {
            return;
        }

        int version = requestVersion;
        DonimalAPI.Instance.GetTexture(
            currentServerQuestion.media_url,

            onSuccess: (texture) =>
            {
                if (version != requestVersion) return;
                ApplyTextureToUI(
                    targetObject,
                    texture
                );
            },

            onError: (error) =>
            {
                Debug.LogError(
                    "[Berburu] Gagal download gambar: " +
                    error
                );
            }
        );
    }

    private void ApplyTextureToUI(
        GameObject targetObject,
        Texture2D texture
    )
    {
        if (
            targetObject == null ||
            texture == null
        )
        {
            return;
        }

        RawImage rawImage =
            targetObject
                .GetComponentInChildren<
                    RawImage
                >(true);

        if (rawImage != null)
        {
            rawImage.texture = texture;
            return;
        }

        Image image =
            targetObject
                .GetComponentInChildren<
                    Image
                >(true);

        if (image != null)
        {
            image.sprite =
                Sprite.Create(
                    texture,
                    new Rect(
                        0f,
                        0f,
                        texture.width,
                        texture.height
                    ),
                    new Vector2(
                        0.5f,
                        0.5f
                    )
                );

            image.preserveAspect = true;
            return;
        }

        Debug.LogWarning(
            "[Berburu] RawImage/Image tidak ditemukan."
        );
    }

    private void PlayQuestionAudio()
    {
        ServerQuestionAudioUtility.Play(currentServerQuestion, "[Berburu]", "Berburu " + currentWord);
    }

    private void StopQuestionAudio()
    {
        ServerQuestionAudioUtility.Stop();
        if (audioManager != null)
        {
            audioManager.StopAudio();
        }
    }

    private void ShowFeedback(
        GameObject feedbackObject
    )
    {
        if (feedbackObject == null)
        {
            return;
        }

        if (feedbackCoroutine != null)
        {
            StopCoroutine(
                feedbackCoroutine
            );
        }

        feedbackCoroutine =
            StartCoroutine(
                FeedbackRoutine(
                    feedbackObject
                )
            );
    }

    private IEnumerator FeedbackRoutine(
        GameObject feedbackObject
    )
    {
        if (
            correctFeedback != null &&
            correctFeedback != feedbackObject
        )
        {
            correctFeedback.SetActive(false);
        }

        if (
            wrongFeedback != null &&
            wrongFeedback != feedbackObject
        )
        {
            wrongFeedback.SetActive(false);
        }

        feedbackObject.SetActive(true);

        yield return new WaitForSeconds(
            feedbackDuration
        );

        feedbackObject.SetActive(false);
        feedbackCoroutine = null;
    }

    private IEnumerator WaitBeforeCompletionFeedback()
    {
        yield return new WaitForSeconds(completionDelay);
        completionCoroutine = null;
        HandleGameCompleted();
    }

    private void HandleGameCompleted()
    {
        StopRunningCoroutines();
        StopQuestionAudio();
        HideAllFeedback();
        HideAllQuestionObjects();

        if (miniGameAnswerController == null)
        {
            miniGameAnswerController =
                FindFirstObjectByType<
                    MiniGameAnswerController
                >(FindObjectsInactive.Include);
        }

        if (miniGameAnswerController != null)
        {
            miniGameAnswerController
                .CompleteBerburuHuruf();
        }

    }

    private void HideAllQuestionObjects()
    {
        if (questions == null)
        {
            return;
        }

        foreach (
            BerburuHurufQuestion q
            in questions
        )
        {
            if (q == null)
            {
                continue;
            }

            if (q.questionObject != null)
            {
                q.questionObject.SetActive(false);
            }

            if (q.letterSlots == null)
            {
                continue;
            }

            foreach (
                PopupLetterSlot slot
                in q.letterSlots
            )
            {
                if (slot != null)
                {
                    slot.ResetSlot();
                }
            }
        }
    }

    private void HideAllFeedback()
    {
        if (correctFeedback != null)
        {
            correctFeedback.SetActive(false);
        }

        if (wrongFeedback != null)
        {
            wrongFeedback.SetActive(false);
        }

        if (completedFeedback != null)
        {
            completedFeedback.SetActive(false);
        }
    }

    private void StopRunningCoroutines()
    {
        if (feedbackCoroutine != null)
        {
            StopCoroutine(
                feedbackCoroutine
            );

            feedbackCoroutine = null;
        }

        if (completionCoroutine != null)
        {
            StopCoroutine(
                completionCoroutine
            );

            completionCoroutine = null;
        }
    }

    private string[] NormalizeSequence(
        string[] sequence
    )
    {
        if (sequence == null)
        {
            return null;
        }

        string[] result =
            new string[sequence.Length];

        for (
            int i = 0;
            i < sequence.Length;
            i++
        )
        {
            result[i] =
                NormalizeLetterInput(
                    sequence[i]
                );
        }

        return result;
    }

    private string[] BuildSequenceFromWord(
        string word
    )
    {
        string normalizedWord =
            NormalizeWord(word);

        string[] result =
            new string[
                normalizedWord.Length
            ];

        for (
            int i = 0;
            i < normalizedWord.Length;
            i++
        )
        {
            result[i] =
                normalizedWord[i]
                    .ToString();
        }

        return result;
    }

    private string NormalizeLetterInput(string value)
    {
        return RfidAnswerUtility.Normalize("huruf", value);
    }

    private string NormalizeWord(
        string value
    )
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return value
            .ToLower()
            .Trim()
            .Replace(" ", "");
    }

    public bool IsQuestionActive()
    {
        return questionActive;
    }

    public bool IsGameCompleted()
    {
        return gameCompleted;
    }

    public string GetCurrentWord()
    {
        return currentWord;
    }

    public bool TryShowNextServerQuestion()
    {
        if (lastPresentedServerQuestion != null)
        {
            serverQuestions.Remove(lastPresentedServerQuestion);
            lastPresentedServerQuestion = null;
        }

        if (serverQuestions.Count == 0)
        {
            return false;
        }

        StartQuestion();
        return true;
    }

    public void StopQuestion()
    {
        requestVersion++;
        questionActive = false;
        gameCompleted = false;

        currentStep = 0;
        currentBuiltAnswer = "";
        currentSequence = null;
        currentWord = "";

        currentServerQuestion = null;

        StopRunningCoroutines();
        StopQuestionAudio();

        HideAllQuestionObjects();
        HideAllFeedback();
    }
}
