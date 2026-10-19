using System.Collections;
using System.Collections.Generic;
using System.Text;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SusunQuestionController : MonoBehaviour
{
    [Header("Controller")]
    public CategoryToMiniGameImageController miniGameController;
    public MiniGameAnswerController miniGameAnswerController;
    public AudioManagerDonimal audioManager;

    [Header("Root Menyusun Angka")]
    [Tooltip("Parent 'susun'. Template lama soal10, soal28, dst. boleh tetap ada sementara.")]
    public Transform susunRoot;

    [Header("Template Dinamis")]
    [Tooltip("Satu template soal yang dipakai untuk SEMUA urutan angka. Jika kosong, script memakai template soalXX pertama yang ditemukan.")]
    public GameObject questionTemplate;

    [Tooltip("Parent tempat slot angka hasil generate diletakkan. Jika kosong, script memakai parent dari Number Slot Prefab.")]
    public Transform numberContainer;

    [Tooltip("Satu contoh object angka. Bisa memakai salah satu child angka dari template lama, misalnya object '2'.")]
    public GameObject numberSlotPrefab;

    [Header("Teks Soal")]
    [Tooltip("TMP Text untuk menampilkan question_text dari server. Jika kosong, dicari otomatis di template.")]
    public TMP_Text questionText;

    [Tooltip("Jarak horizontal antar angka jika Number Container tidak memakai layout khusus.")]
    public float numberSpacing = 140f;

    [Header("Question Settings")]
    public bool randomQuestion = true;
    public bool preventImmediateRepeat = true;

    [Header("Animasi Pop Up")]
    public float startScale = 0f;
    public float overshootScale = 1.2f;
    public float popUpDuration = 0.25f;
    public float settleDuration = 0.15f;
    public float completedDisplayDuration = 0.8f;

    private readonly List<GameObject> legacyQuestionTemplates = new List<GameObject>();
    private readonly List<GameObject> sourceNumberObjects = new List<GameObject>();
    private readonly List<Transform> generatedNumberObjects = new List<Transform>();
    private readonly List<Vector3> generatedOriginalScales = new List<Vector3>();

    private Vector3 numberPrefabLocalPosition;
    private Vector2 numberPrefabAnchoredPosition;
    private bool numberPrefabHasRectTransform;

    private int currentStep = 0;
    private string[] currentSequence = null;
    private string currentFinalAnswer = "";

    private bool questionActive = false;
    private bool sequenceCompleted = false;
    private bool isAnimating = false;

    private Coroutine popUpCoroutine;

    private readonly List<GameQuestionData> serverQuestions = new List<GameQuestionData>();
    private bool isFetchingQuestions = false;
    private GameQuestionData currentServerQuestion;
    private GameQuestionData lastPresentedServerQuestion;
    private int requestVersion;

    private void Awake()
    {
        if (susunRoot == null)
        {
            susunRoot = transform;
        }

        PrepareDynamicTemplate();
        HideAllQuestions();
        HideSourceNumberObjects();
    }

    private void Start()
    {
        if (miniGameController == null)
        {
            miniGameController = FindFirstObjectByType<CategoryToMiniGameImageController>(FindObjectsInactive.Include);
        }

        if (miniGameAnswerController == null)
        {
            miniGameAnswerController = FindFirstObjectByType<MiniGameAnswerController>(FindObjectsInactive.Include);
        }

        if (audioManager == null)
        {
            audioManager = FindFirstObjectByType<AudioManagerDonimal>(FindObjectsInactive.Include);
        }

        // StartQuestion dapat dipanggil sebelum Start saat panel baru diaktifkan.
        // Jangan menyembunyikan soal yang sudah disiapkan oleh StartQuestion.
    }

    private void OnDisable()
    {
        StopQuestion();
    }

    // =========================================================
    // TEMPLATE DINAMIS
    // =========================================================

    private bool PrepareDynamicTemplate()
    {
        CacheLegacyQuestionTemplates();

        if (questionTemplate == null && legacyQuestionTemplates.Count > 0)
        {
            questionTemplate = legacyQuestionTemplates[0];
            Debug.Log(
                "[SusunQuestion] Dynamic Question Template otomatis memakai: " +
                questionTemplate.name
            );
        }

        if (questionTemplate == null)
        {
            Transform susunTemplate = FindChildByName(susunRoot, "susun");
            if (susunTemplate != null && susunTemplate != susunRoot)
            {
                questionTemplate = susunTemplate.gameObject;
                Debug.Log("[SusunQuestion] Template memakai child susun.");
            }
        }

        if (questionTemplate == null)
        {
            Debug.LogError(
                "[SusunQuestion] Question Template belum tersedia. " +
                "Isi Question Template dengan SATU template soal yang akan dipakai untuk semua urutan."
            );
            return false;
        }

        if (numberSlotPrefab == null)
        {
            // Pada scene saat ini container angka adalah sibling gambar soal.
            if (numberContainer == null)
                numberContainer = FindNamedNumberContainer(susunRoot);

            Transform fallbackSlot = FindFirstNumberSlot(numberContainer);
            if (fallbackSlot == null)
                fallbackSlot = FindFirstNumberSlot(questionTemplate.transform);
            if (fallbackSlot == null)
                fallbackSlot = FindFirstNumberSlot(susunRoot);
            if (fallbackSlot != null)
            {
                numberSlotPrefab = fallbackSlot.gameObject;
                Debug.Log(
                    "[SusunQuestion] Number Slot Prefab otomatis memakai child: " +
                    numberSlotPrefab.name
                );
            }
        }

        if (numberSlotPrefab == null)
        {
            Debug.LogError(
                "[SusunQuestion] Number Slot Prefab belum tersedia. " +
                "Isi dengan satu object angka yang memiliki Image/RawImage/SpriteRenderer."
            );
            return false;
        }

        if (numberContainer == null)
        {
            if (numberSlotPrefab.transform.parent != null)
            {
                numberContainer = numberSlotPrefab.transform.parent;
            }
            else
            {
                numberContainer = FindNamedNumberContainer(questionTemplate.transform);
            }
        }

        if (numberContainer == null)
        {
            Debug.LogError(
                "[SusunQuestion] Number Container belum tersedia. " +
                "Buat parent untuk angka lalu isi field Number Container."
            );
            return false;
        }

        CacheSourceNumberObjects();

        ResolveQuestionText();

        RectTransform prefabRect = numberSlotPrefab.GetComponent<RectTransform>();
        numberPrefabHasRectTransform = prefabRect != null;
        numberPrefabLocalPosition = numberSlotPrefab.transform.localPosition;
        numberPrefabAnchoredPosition = prefabRect != null
            ? prefabRect.anchoredPosition
            : Vector2.zero;

        HideSourceNumberObjects();

        Debug.Log(
            "[SusunQuestion] Template dinamis siap. Template=" + questionTemplate.name +
            " | Container=" + numberContainer.name +
            " | Slot=" + numberSlotPrefab.name
        );

        return true;
    }

    private void ResolveQuestionText()
    {
        if (questionText != null || questionTemplate == null)
        {
            return;
        }

        foreach (TMP_Text candidate in susunRoot.GetComponentsInChildren<TMP_Text>(true))
        {
            string cleanName = candidate.gameObject.name.Trim().ToLowerInvariant();
            if (cleanName == "question_text" || cleanName == "questiontext" ||
                cleanName == "teks_soal" || cleanName == "tekssoal")
            {
                questionText = candidate;
                return;
            }
        }

        // Template lama dapat berupa SpriteRenderer, jadi teks UI berada di root susun.
        GameObject textObject = new GameObject("question_text", typeof(RectTransform));
        textObject.transform.SetParent(susunRoot, false);
        questionText = textObject.AddComponent<TextMeshProUGUI>();
        questionText.alignment = TextAlignmentOptions.Center;
        questionText.fontSize = 36f;
        questionText.enableAutoSizing = true;
        questionText.fontSizeMin = 20f;
        questionText.fontSizeMax = 36f;
        questionText.color = Color.black;
        questionText.raycastTarget = false;
        RectTransform textRect = questionText.rectTransform;
        textRect.anchorMin = new Vector2(0.1f, 0.72f);
        textRect.anchorMax = new Vector2(0.9f, 0.92f);
        textRect.offsetMin = Vector2.zero;
        textRect.offsetMax = Vector2.zero;
    }

    private void ApplyQuestionText()
    {
        if (questionText == null)
        {
            return;
        }

        questionText.gameObject.SetActive(false);
    }

    private void CacheLegacyQuestionTemplates()
    {
        legacyQuestionTemplates.Clear();

        if (susunRoot == null)
        {
            return;
        }

        Transform[] allTransforms = susunRoot.GetComponentsInChildren<Transform>(true);

        foreach (Transform candidate in allTransforms)
        {
            if (candidate == null || candidate == susunRoot)
            {
                continue;
            }

            if (!LooksLikeLegacyQuestionTemplate(candidate.name))
            {
                continue;
            }

            if (!legacyQuestionTemplates.Contains(candidate.gameObject))
            {
                legacyQuestionTemplates.Add(candidate.gameObject);
            }
        }
    }

    private bool LooksLikeLegacyQuestionTemplate(string objectName)
    {
        if (string.IsNullOrWhiteSpace(objectName))
        {
            return false;
        }

        string clean = objectName.Trim().ToLowerInvariant();
        if (!clean.StartsWith("soal"))
        {
            return false;
        }

        string suffix = clean.Substring(4);
        if (suffix.Length < 2)
        {
            return false;
        }

        foreach (char c in suffix)
        {
            if (!char.IsDigit(c))
            {
                return false;
            }
        }

        return true;
    }

    private Transform FindFirstNumberSlot(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child == null || child == root)
            {
                continue;
            }

            string clean = child.name.Trim().ToLowerInvariant();
            bool isDigitSlot = clean.Length == 1 && char.IsDigit(clean[0]);
            bool isNamedNumberSlot = clean.StartsWith("angka") && clean.Length > 5;
            if (!isDigitSlot && !isNamedNumberSlot)
            {
                continue;
            }

            if (
                child.GetComponent<RawImage>() != null ||
                child.GetComponent<Image>() != null ||
                child.GetComponentInChildren<SpriteRenderer>(true) != null ||
                child.GetComponentInChildren<RawImage>(true) != null ||
                child.GetComponentInChildren<Image>(true) != null
            )
            {
                return child;
            }
        }

        return null;
    }

    private Transform FindNamedNumberContainer(Transform root)
    {
        if (root == null)
        {
            return null;
        }

        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child == null)
            {
                continue;
            }

            string clean = child.name.Trim().ToLowerInvariant();
            if (
                clean == "numbercontainer" ||
                clean == "number_container" ||
                clean == "numbers" ||
                clean == "answer" ||
                clean == "angkacontainer" ||
                clean == "angka_container"
            )
            {
                return child;
            }
        }

        return null;
    }

    private void CacheSourceNumberObjects()
    {
        sourceNumberObjects.Clear();

        if (numberContainer == null)
        {
            return;
        }

        // Hanya child langsung dari container yang dianggap slot angka lama.
        // Ini mencegah child UI di dalam prefab ikut dimatikan saat prefab dikloning.
        foreach (Transform child in numberContainer)
        {
            if (child == null)
            {
                continue;
            }

            string clean = child.name.Trim().ToLowerInvariant();
            bool isDigitSlot = clean.Length == 1 && char.IsDigit(clean[0]);
            bool isNamedNumberSlot = clean.StartsWith("angka") && clean.Length > 5;
            if (isDigitSlot || isNamedNumberSlot)
            {
                if (!sourceNumberObjects.Contains(child.gameObject))
                {
                    sourceNumberObjects.Add(child.gameObject);
                }
            }
        }

        if (numberSlotPrefab != null && !sourceNumberObjects.Contains(numberSlotPrefab))
        {
            sourceNumberObjects.Add(numberSlotPrefab);
        }
    }

    private Transform FindChildByName(Transform root, string childName)
    {
        if (root == null || string.IsNullOrWhiteSpace(childName)) return null;

        foreach (Transform child in root.GetComponentsInChildren<Transform>(true))
        {
            if (child != null && string.Equals(child.name.Trim(), childName, System.StringComparison.OrdinalIgnoreCase))
            {
                return child;
            }
        }

        return null;
    }

    private void HideSourceNumberObjects()
    {
        foreach (GameObject obj in sourceNumberObjects)
        {
            if (obj != null)
            {
                obj.SetActive(false);
            }
        }
    }

    private bool GenerateNumberSlots()
    {
        ClearGeneratedNumbers();
        HideSourceNumberObjects();

        if (
            currentSequence == null ||
            currentSequence.Length == 0 ||
            numberContainer == null
        )
        {
            return false;
        }

        if (sourceNumberObjects.Count < currentSequence.Length)
        {
            Debug.LogError(
                "[SusunQuestion] Slot angka tidak cukup. Dibutuhkan " +
                currentSequence.Length + ", tersedia " + sourceNumberObjects.Count +
                ". Isi parent answer dengan angka1 dan angka2."
            );
            return false;
        }

        for (int i = 0; i < currentSequence.Length; i++)
        {
            GameObject slot = sourceNumberObjects[i];
            if (slot == null)
            {
                return false;
            }

            Vector3 safeScale = GetSafeScale(slot.transform.localScale);
            slot.transform.localScale = safeScale;
            slot.SetActive(false);

            generatedNumberObjects.Add(slot.transform);
            generatedOriginalScales.Add(safeScale);

            LoadNumberImageFromLearningTable(
                slot.transform,
                currentSequence[i],
                requestVersion
            );
        }

        Debug.Log(
            "[SusunQuestion] Berhasil generate " +
            generatedNumberObjects.Count +
            " slot angka."
        );

        return generatedNumberObjects.Count == currentSequence.Length;
    }

    private void PositionGeneratedSlot(Transform target, int index, int total)
    {
        if (target == null)
        {
            return;
        }

        float centerOffset = index - ((total - 1) / 2f);
        float xOffset = centerOffset * numberSpacing;

        RectTransform rect = target.GetComponent<RectTransform>();
        if (rect != null && numberPrefabHasRectTransform)
        {
            rect.anchoredPosition = new Vector2(
                numberPrefabAnchoredPosition.x + xOffset,
                numberPrefabAnchoredPosition.y
            );
        }
        else
        {
            Vector3 pos = numberPrefabLocalPosition;
            pos.x += xOffset;
            target.localPosition = pos;
        }
    }

    private void ClearGeneratedNumbers()
    {
        foreach (Transform generated in generatedNumberObjects)
        {
            if (generated == null)
            {
                continue;
            }

            generated.gameObject.SetActive(false);
            if (!sourceNumberObjects.Contains(generated.gameObject))
            {
                Destroy(generated.gameObject);
            }
        }

        generatedNumberObjects.Clear();
        generatedOriginalScales.Clear();
    }

    private Vector3 GetSafeScale(Vector3 scale)
    {
        if (Mathf.Approximately(scale.x, 0f)) scale.x = 1f;
        if (Mathf.Approximately(scale.y, 0f)) scale.y = 1f;
        if (Mathf.Approximately(scale.z, 0f)) scale.z = 1f;
        return scale;
    }

    // =========================================================
    // START / FETCH
    // =========================================================

    public void StartQuestion()
    {
        ScoreManager.Instance?.UseDefaultQuestionPoint();

        StopRunningCoroutine();
        requestVersion++;
        StopQuestionAudio();

        questionActive = false;
        sequenceCompleted = false;
        isAnimating = false;
        currentStep = 0;
        currentFinalAnswer = "";
        currentSequence = null;

        ClearGeneratedNumbers();
        HideAllQuestions();

        if (!PrepareDynamicTemplate())
        {
            return;
        }

        if (serverQuestions.Count == 0 && !isFetchingQuestions)
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
        int fetchVersion = requestVersion;

        Debug.Log("[SusunQuestion] Fetching questions from server...");

        if (DonimalAPI.Instance == null)
        {
            Debug.LogError(
                "[SusunQuestion] DonimalAPI tidak ditemukan. Mode server-only aktif."
            );

            isFetchingQuestions = false;
            SetupLocalFallbackQuestion();
            return;
        }

        // Offline mode – langsung ke fallback lokal
        if (DonimalAPI.Instance.offlineMode)
        {
            isFetchingQuestions = false;
            Debug.LogWarning("[SusunQuestion] Offline mode aktif – menggunakan soal lokal.");
            SetupLocalFallbackQuestion();
            return;
        }

        FetchQuestionsForMode("menyusun_angka", fetchVersion);
    }

    private void SetupLocalFallbackQuestion()
    {
        // Jika sudah ada soal server dari sebelumnya, pakai itu
        if (serverQuestions.Count > 0)
        {
            SetupDynamicQuestion();
            return;
        }

        // Buat soal lokal: angka dua digit yang bisa disusun
        string[] localSequences = { "10", "12", "21", "13", "31", "20", "23", "32", "14", "41" };
        foreach (string seq in localSequences)
        {
            GameQuestionData fakeQ = new GameQuestionData
            {
                id = "local_susun_" + seq,
                correct_answer = seq,
                option_data = new GameQuestionOptionData
                {
                    sequence = new string[seq.Length]
                }
            };
            for (int i = 0; i < seq.Length; i++)
                fakeQ.option_data.sequence[i] = seq[i].ToString();

            serverQuestions.Add(fakeQ);
        }

        Debug.Log("[SusunQuestion] Soal lokal dibuat: " + serverQuestions.Count + " soal.");
        SetupDynamicQuestion();
    }

    private void FetchQuestionsForMode(string mode, int fetchVersion)
    {
        if (fetchVersion != requestVersion) return;

        DonimalAPI.Instance.GetRandomQuestions(
            mode,
            50,

            onSuccess: (data) =>
            {
                if (fetchVersion != requestVersion)
                {
                    return;
                }

                if ((data == null || data.Count == 0) && mode == "menyusun_angka")
                {
                    Debug.LogWarning("[SusunQuestion] Mode menyusun_angka kosong; mencoba alias susun_angka.");
                    FetchQuestionsForMode("susun_angka", fetchVersion);
                    return;
                }

                isFetchingQuestions = false;

                serverQuestions.Clear();

                if (data != null)
                {
                    serverQuestions.AddRange(data);

#if UNITY_EDITOR
                    LocalQuestionAudioCatalog catalog = Resources.Load<LocalQuestionAudioCatalog>("LocalQuestionAudioCatalog");
                    if (catalog != null) catalog.ImportSusunQuestionIds(data);
                    else Debug.LogWarning("[SusunQuestion] Katalog lokal tidak ditemukan; ID belum dapat disimpan.");
#endif

                    StringBuilder apiCheck =
                        new StringBuilder("[SusunAngka API CHECK]");

                    foreach (GameQuestionData question in data)
                    {
                        if (question == null)
                        {
                            continue;
                        }

                        string sequenceText = "NULL";
                        string numbersText = "NULL";

                        if (question.option_data != null)
                        {
                            if (question.option_data.sequence != null)
                            {
                                sequenceText = string.Join(
                                    ", ",
                                    question.option_data.sequence
                                );
                            }

                            if (question.option_data.numbers != null)
                            {
                                numbersText = string.Join(
                                    ", ",
                                    question.option_data.numbers
                                );
                            }
                        }

                        apiCheck
                            .Append("\nID: ")
                            .Append(question.id)
                            .Append(" | correct_answer: '")
                            .Append(question.correct_answer)
                            .Append("' | answer(alias): '")
                            .Append(question.answer)
                            .Append("' | sequence: [")
                            .Append(sequenceText)
                            .Append("] | numbers: [")
                            .Append(numbersText)
                            .Append("]");
                    }

                    Debug.Log(apiCheck.ToString());
                }

                if (serverQuestions.Count == 0)
                {
                    Debug.LogError("[SusunQuestion] Server mengembalikan soal kosong untuk menyusun_angka dan susun_angka. " +
                        "Periksa hubungan subgame dan status aktif sembilan soal di backend. " +
                        "Pendaftaran ID di katalog audio tidak menambahkan soal ke database server.");
                    SetupLocalFallbackQuestion();
                    return;
                }

                Debug.Log("[SusunQuestion] Soal ditemukan menggunakan mode: " + mode);
                SetupDynamicQuestion();
            },

            onError: (error) =>
            {
                if (fetchVersion != requestVersion)
                {
                    return;
                }

                isFetchingQuestions = false;

                Debug.LogError("[SusunQuestion] Gagal fetch soal untuk mode " + mode + ": " + error);
                SetupLocalFallbackQuestion();
            }
        );
    }

    // =========================================================
    // DATA SOAL DINAMIS
    // =========================================================

    private void SetupDynamicQuestion()
    {
        if (serverQuestions.Count == 0)
        {
            SetupLocalFallbackQuestion();
            return;
        }

        List<GameQuestionData> compatibleQuestions = serverQuestions.FindAll(
            question => IsQuestionCompatible(question)
        );

        if (compatibleQuestions.Count == 0)
        {
            Debug.LogError("[SusunQuestion] Tidak ada soal dengan jawaban minimal dua digit. Periksa data susun_angka di server.");
            SetupLocalFallbackQuestion();
            return;
        }
        if (preventImmediateRepeat && lastPresentedServerQuestion != null && compatibleQuestions.Count > 1)
        {
            compatibleQuestions.RemoveAll(q => q.id == lastPresentedServerQuestion.id);
        }

        currentServerQuestion = randomQuestion
            ? compatibleQuestions[Random.Range(0, compatibleQuestions.Count)]
            : compatibleQuestions[0];
        lastPresentedServerQuestion = currentServerQuestion;

        if (currentServerQuestion == null)
        {
            Debug.LogError("[SusunQuestion] Data soal server null.");
            HandleServerFailure();
            return;
        }

        ScoreManager.Instance?.SetCurrentQuestionPoint(
            currentServerQuestion.score_point
        );

        if (!TryBuildSequence(currentServerQuestion, out List<string> sequence))
        {
            Debug.LogError(
                "[SusunQuestion] Gagal membentuk sequence dari soal ID: " +
                currentServerQuestion.id
            );
            HandleServerFailure();
            return;
        }

        currentSequence = sequence.ToArray();
        currentStep = 0;
        currentFinalAnswer = "";
        sequenceCompleted = false;
        isAnimating = false;

        string normalizedTarget = NormalizeNumber(GetQuestionAnswer(currentServerQuestion));
        string sequenceAnswer = string.Join("", currentSequence);

        if (
            !string.IsNullOrEmpty(normalizedTarget) &&
            normalizedTarget != sequenceAnswer
        )
        {
            Debug.LogError(
                "[SusunQuestion] option_data.sequence tidak sama dengan correct_answer. " +
                "Sequence=" + sequenceAnswer +
                " | correct_answer=" + normalizedTarget
            );
            HandleServerFailure();
            return;
        }

        HideAllQuestions();
        questionTemplate.SetActive(true);
        ApplyQuestionText();

        if (!GenerateNumberSlots())
        {
            Debug.LogError("[SusunQuestion] Gagal membuat slot angka dinamis.");
            HandleServerFailure();
            return;
        }

        if (miniGameController != null)
        {
            miniGameController.SetCurrentCorrectAnswer(
                "angka:" + currentSequence[0]
            );
        }

        LoadServerImage();
        PlayQuestionAudio();

        questionActive = true;

        Debug.Log(
            "[SusunQuestion] Target akhir: " +
            sequenceAnswer
        );

        Debug.Log(
            "[SusunQuestion] Sequence: " +
            string.Join(" -> ", currentSequence)
        );
    }

    private bool HasPlayableQuestion()
    {
        return serverQuestions.Exists(IsQuestionCompatible);
    }
    private bool IsQuestionCompatible(GameQuestionData question)
    {
        if (question == null)
        {
            return false;
        }

        if (!TryBuildSequence(question, out List<string> sequence))
        {
            return false;
        }

        // Menyusun angka minimal 2 kartu/digit.
        return sequence.Count >= 2;
    }

    private bool TryBuildSequence(
        GameQuestionData question,
        out List<string> sequence
    )
    {
        sequence = new List<string>();

        if (question == null)
        {
            return false;
        }

        // Target jawaban adalah sumber urutan utama. Dengan begitu soal
        // "1 dan 0 untuk angka 10" selalu diproses sebagai 1 -> 0,
        // walaupun option_data.sequence dari data lama berbeda.
        string rawAnswer = GetQuestionAnswer(question);
        string normalizedAnswer = NormalizeNumber(rawAnswer);

        foreach (char digit in normalizedAnswer)
        {
            if (char.IsDigit(digit))
            {
                sequence.Add(digit.ToString());
            }
        }

        // Fallback untuk data lama yang belum memiliki correct_answer.
        if (sequence.Count < 2 && question.option_data != null)
        {
            sequence.Clear();

            if (question.option_data.sequence != null &&
                question.option_data.sequence.Length > 0)
            {
                AddDigitsFromValues(question.option_data.sequence, sequence);
            }

            if (sequence.Count < 2 && question.option_data.numbers != null &&
                question.option_data.numbers.Length > 0)
            {
                sequence.Clear();
                AddDigitsFromValues(question.option_data.numbers, sequence);
            }
        }

        // Menyusun angka minimal membutuhkan dua kartu angka.
        if (sequence.Count < 2)
        {
            return false;
        }

        return true;
    }


    private void AddDigitsFromValues(
        string[] values,
        List<string> target
    )
    {
        if (values == null || target == null)
        {
            return;
        }

        foreach (string value in values)
        {
            string normalized = NormalizeNumber(value);

            // Jika satu item berisi lebih dari satu digit (mis. "24"),
            // pecah menjadi dua kartu: "2", "4".
            foreach (char digit in normalized)
            {
                if (char.IsDigit(digit))
                {
                    target.Add(digit.ToString());
                }
            }
        }
    }

    private string GetQuestionAnswer(GameQuestionData question)
    {
        if (question == null)
        {
            return "";
        }

        if (!string.IsNullOrWhiteSpace(question.correct_answer))
        {
            return question.correct_answer;
        }

        // DonimalAPI masih menyediakan alias `answer` untuk endpoint lama.
        if (!string.IsNullOrWhiteSpace(question.answer))
        {
            return question.answer;
        }

        return "";
    }

    // =========================================================
    // INPUT / VALIDASI JAWABAN
    // =========================================================

    public void CheckNumber(string inputData)
    {
        if (!questionActive || sequenceCompleted || isAnimating)
        {
            return;
        }

        string inputNumber = NormalizeNumber(inputData);

        if (string.IsNullOrEmpty(inputNumber))
        {
            return;
        }

        // Satu kali scan harus mewakili satu kartu angka 0-9.
        if (inputNumber.Length != 1 || !char.IsDigit(inputNumber[0]))
        {
            Debug.LogWarning(
                "[SusunQuestion] Input harus satu digit. Input diterima: " + inputData
            );
            return;
        }

        if (currentSequence == null || currentStep >= currentSequence.Length)
        {
            return;
        }

        string expected = currentSequence[currentStep];

        Debug.Log(
            "[SusunQuestion] Input: " + inputNumber +
            " | Expected: " + expected +
            " | Step: " + (currentStep + 1) + "/" + currentSequence.Length
        );

        if (inputNumber != expected)
        {
            Debug.Log("[SusunQuestion] SALAH URUTAN.");

            if (miniGameAnswerController != null)
            {
                miniGameAnswerController.ShowSusunAngkaWrongFeedback();
            }

            return;
        }

        currentFinalAnswer += inputNumber;
        HandleCorrectStep();
    }

    private void HandleCorrectStep()
    {
        if (
            currentSequence == null ||
            currentStep < 0 ||
            currentStep >= currentSequence.Length ||
            currentStep >= generatedNumberObjects.Count
        )
        {
            return;
        }

        int completedIndex = currentStep;
        Transform completedObject = generatedNumberObjects[completedIndex];
        Vector3 completedScale = generatedOriginalScales[completedIndex];

        currentStep++;
        bool isFinalStep = currentStep >= currentSequence.Length;

        if (!isFinalStep)
        {
            Debug.Log(
                "[SusunQuestion] Step " + completedIndex +
                " benar. Berikutnya: " + currentSequence[currentStep]
            );

            if (miniGameController != null)
            {
                miniGameController.SetCurrentCorrectAnswer(
                    "angka:" + currentSequence[currentStep]
                );
            }
        }
        else
        {
            string finalAnswer = NormalizeNumber(currentFinalAnswer);
            string correctAnswer = NormalizeNumber(currentServerQuestion != null
                    ? GetQuestionAnswer(currentServerQuestion)
                    : string.Join("", currentSequence));

            // Jika correct_answer kosong tetapi sequence valid, sequence menjadi acuan.
            if (string.IsNullOrEmpty(correctAnswer))
            {
                correctAnswer = string.Join("", currentSequence);
            }

            if (finalAnswer != correctAnswer)
            {
                Debug.LogError(
                    "[SusunQuestion] Hasil akhir " + finalAnswer +
                    " tidak sama dengan correct_answer " + correctAnswer
                );

                if (miniGameAnswerController != null)
                {
                    miniGameAnswerController.ShowSusunAngkaWrongFeedback();
                }

                // Jangan kunci soal agar bisa dicoba lagi.
                currentStep = Mathf.Max(0, currentStep - 1);
                currentFinalAnswer = currentFinalAnswer.Substring(
                    0,
                    Mathf.Max(0, currentFinalAnswer.Length - 1)
                );
                return;
            }

            Debug.Log(
                "[SusunQuestion] Sequence selesai. Hasil akhir: " + finalAnswer
            );

            sequenceCompleted = true;
            questionActive = false;

            if (miniGameController != null)
            {
                miniGameController.SetCurrentCorrectAnswer("");
            }
        }

        if (popUpCoroutine != null)
        {
            StopCoroutine(popUpCoroutine);
        }

        popUpCoroutine = StartCoroutine(
            PopUpRoutine(
                completedObject,
                completedScale,
                isFinalStep
            )
        );
    }

    private IEnumerator PopUpRoutine(
        Transform targetTransform,
        Vector3 originalScale,
        bool isFinalStep
    )
    {
        if (targetTransform == null)
        {
            yield break;
        }

        isAnimating = true;

        Vector3 startVector = originalScale * startScale;
        Vector3 overshootVector = originalScale * overshootScale;

        targetTransform.localScale = startVector;
        targetTransform.gameObject.SetActive(true);

        float elapsed = 0f;

        while (elapsed < popUpDuration)
        {
            elapsed += Time.deltaTime;

            float t = popUpDuration <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / popUpDuration);

            targetTransform.localScale = Vector3.Lerp(
                startVector,
                overshootVector,
                t
            );

            yield return null;
        }

        targetTransform.localScale = overshootVector;
        elapsed = 0f;

        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;

            float t = settleDuration <= 0f
                ? 1f
                : Mathf.Clamp01(elapsed / settleDuration);

            targetTransform.localScale = Vector3.Lerp(
                overshootVector,
                originalScale,
                t
            );

            yield return null;
        }

        targetTransform.localScale = originalScale;
        isAnimating = false;

        if (isFinalStep)
        {
            if (completedDisplayDuration > 0f)
            {
                yield return new WaitForSeconds(completedDisplayDuration);
            }

            if (miniGameAnswerController != null)
            {
                miniGameAnswerController.CompleteSusunAngka();
            }
        }

        popUpCoroutine = null;
    }

    // =========================================================
    // MEDIA SOAL / GAMBAR ANGKA
    // =========================================================

    private void LoadServerImage()
    {
        if (
            currentServerQuestion == null ||
            string.IsNullOrWhiteSpace(currentServerQuestion.media_url) ||
            questionTemplate == null ||
            DonimalAPI.Instance == null
        )
        {
            return;
        }

        GameObject imageTarget = FindQuestionImageTarget(questionTemplate);
        if (imageTarget == null)
        {
            Debug.Log("[SusunQuestion] Template tidak memiliki area gambar soal; dilewati.");
            return;
        }

        int version = requestVersion;

        DonimalAPI.Instance.GetTexture(
            currentServerQuestion.media_url,

            onSuccess: (texture) =>
            {
                if (version != requestVersion) return;
                ApplyTextureToUI(imageTarget, texture);
            },

            onError: (error) =>
            {
                if (version != requestVersion) return;
                Debug.LogError(
                    "[SusunQuestion] Gagal download gambar: " + error
                );
            }
        );
    }

    private GameObject FindQuestionImageTarget(GameObject template)
    {
        if (template == null) return null;

        Transform[] children = template.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child == null || child == template.transform) continue;

            string childName = child.name.Trim().ToLowerInvariant();
            if (childName != "soal" && childName != "question" && childName != "front")
            {
                continue;
            }

            if (
                child.GetComponent<RawImage>() != null ||
                child.GetComponent<Image>() != null ||
                child.GetComponentInChildren<RawImage>(true) != null ||
                child.GetComponentInChildren<Image>(true) != null
            )
            {
                return child.gameObject;
            }
        }

        return null;
    }

    private void LoadNumberImageFromLearningTable(
        Transform target,
        string number,
        int version
    )
    {
        if (target == null || DonimalAPI.Instance == null) return;

        DonimalAPI.Instance.GetLearningMaterial(
            number,
            material =>
            {
                if (version != requestVersion || target == null) return;

                if (material == null || string.IsNullOrWhiteSpace(material.media_url))
                {
                    Debug.LogError(
                        "[SusunQuestion] Tabel belajar tidak mengirim media_url angka " +
                        number + "."
                    );
                    return;
                }

                if (!LearningUrlMatchesNumber(material.media_url, number))
                {
                    Debug.LogError(
                        "[SusunQuestion] media_url tabel belajar tidak sesuai angka " +
                        number + ": " + material.media_url
                    );
                    return;
                }

                if (DonimalAPI.Instance == null) return;

                DonimalAPI.Instance.GetTexture(
                    material.media_url,
                    texture =>
                    {
                        if (version != requestVersion || target == null) return;

                        SpriteRenderer spriteRenderer = target.GetComponentInChildren<SpriteRenderer>(true);
                        if (spriteRenderer != null)
                        {
                            float pixelsPerUnit = spriteRenderer.sprite != null
                                ? spriteRenderer.sprite.pixelsPerUnit : 100f;
                            spriteRenderer.sprite = Sprite.Create(texture,
                                new Rect(0f, 0f, texture.width, texture.height),
                                new Vector2(0.5f, 0.5f), pixelsPerUnit);
                        }
                        else
                        {
                            ServerQuestionImageUtility.ApplyTexture(target.gameObject, texture,
                                "[SusunQuestion Angka " + number + "]");
                        }
                    },
                    error =>
                    {
                        if (version == requestVersion)
                        {
                            Debug.LogError(
                                "[SusunQuestion] Gagal download gambar angka " +
                                number + ": " + error +
                                " | media_url: " + material.media_url
                            );
                        }
                    }
                );
            },
            error =>
            {
                if (version == requestVersion)
                {
                    Debug.LogError(
                        "[SusunQuestion] Gagal membaca tabel belajar angka " +
                        number + ": " + error
                    );
                }
            }
        );
    }

    private static bool LearningUrlMatchesNumber(string mediaUrl, string number)
    {
        if (string.IsNullOrWhiteSpace(mediaUrl)) return false;

        string cleanUrl = mediaUrl.Replace("%20", " ").Replace("\\", "/");
        int query = cleanUrl.IndexOf('?');
        if (query >= 0) cleanUrl = cleanUrl.Substring(0, query);

        int slash = cleanUrl.LastIndexOf('/');
        string fileName = slash >= 0 ? cleanUrl.Substring(slash + 1) : cleanUrl;

        int extension = fileName.LastIndexOf('.');
        if (extension > 0) fileName = fileName.Substring(0, extension);

        return string.Equals(
            fileName.Trim(),
            number.Trim(),
            System.StringComparison.OrdinalIgnoreCase
        );
    }

    private void ApplyTextureToUI(GameObject targetObject, Texture2D texture)
    {
        if (targetObject == null || texture == null)
        {
            return;
        }

        RawImage rawImage = targetObject.GetComponentInChildren<RawImage>(true);
        if (rawImage != null)
        {
            rawImage.texture = texture;
            return;
        }

        Image image = targetObject.GetComponentInChildren<Image>(true);
        if (image != null)
        {
            image.sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f)
            );

            image.preserveAspect = true;
            return;
        }

        Debug.LogWarning("[SusunQuestion] RawImage/Image tidak ditemukan.");
    }

    // =========================================================
    // AUDIO / RESET / NEXT
    // =========================================================

    private void PlayQuestionAudio()
    {
        ServerQuestionAudioUtility.Play(currentServerQuestion, "[SusunQuestion]");
    }

    private void StopQuestionAudio()
    {
        ServerQuestionAudioUtility.Stop();

        if (audioManager != null)
        {
            audioManager.StopAudio();
        }
    }

    private void HideAllQuestions()
    {
        if (questionText != null)
        {
            questionText.gameObject.SetActive(false);
        }

        foreach (GameObject template in legacyQuestionTemplates)
        {
            if (template != null)
            {
                template.SetActive(false);
            }
        }

        if (questionTemplate != null)
        {
            questionTemplate.SetActive(false);
        }
    }

    private void HandleServerFailure()
    {
        questionActive = false;
        sequenceCompleted = false;
        isAnimating = false;
        currentStep = 0;
        currentFinalAnswer = "";
        currentSequence = null;
        currentServerQuestion = null;

        StopRunningCoroutine();
        StopQuestionAudio();
        ClearGeneratedNumbers();
        HideAllQuestions();
        HideSourceNumberObjects();
    }

    public bool TryShowNextServerQuestion()
    {
        if (lastPresentedServerQuestion != null)
        {
            serverQuestions.Remove(lastPresentedServerQuestion);
            lastPresentedServerQuestion = null;
        }

        if (serverQuestions.Count == 0 || !HasPlayableQuestion())
        {
            HandleServerFailure();
            return false;
        }

        StartQuestion();
        return true;
    }

    public void StopQuestion()
    {
        requestVersion++;
        StopRunningCoroutine();
        StopQuestionAudio();

        questionActive = false;
        sequenceCompleted = false;
        isAnimating = false;
        currentStep = 0;
        currentFinalAnswer = "";
        currentSequence = null;

        if (miniGameController != null)
        {
            miniGameController.SetCurrentCorrectAnswer("");
        }

        ClearGeneratedNumbers();
        HideAllQuestions();
        HideSourceNumberObjects();

        currentServerQuestion = null;
    }

    private void StopRunningCoroutine()
    {
        if (popUpCoroutine != null)
        {
            StopCoroutine(popUpCoroutine);
            popUpCoroutine = null;
        }

        isAnimating = false;
    }

    // =========================================================
    // NORMALISASI
    // =========================================================

    private string NormalizeNumber(string value)
    {
        value = RfidAnswerUtility.Normalize("angka", value);
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        string result = value.ToLowerInvariant().Trim();

        if (result.StartsWith("angka:"))
        {
            result = result.Substring("angka:".Length);
        }

        // Hanya simpan digit. Jadi:
        // "28"          -> "28"
        // "2, 4, 1, 3" -> "2413"
        // "angka:2"     -> "2"
        StringBuilder digits = new StringBuilder();
        foreach (char c in result)
        {
            if (char.IsDigit(c))
            {
                digits.Append(c);
            }
        }

        return digits.ToString();
    }

    public bool IsQuestionActive()
    {
        return questionActive;
    }

    public string GetCurrentFinalAnswer()
    {
        return currentFinalAnswer;
    }
}
