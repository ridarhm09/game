using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

[System.Serializable]
public class BunyiQuestionVisual
{
    [Tooltip("Huruf soal, contoh: a")]
    public string letter;

    [Tooltip("GameObject popup soal untuk huruf tersebut")]
    public GameObject questionObject;

    [Tooltip("Tekstur lokal yang digunakan saat server tidak tersedia.")]
    public Sprite localTexture;
}

public class BunyiQuestionController : MonoBehaviour
{
    [Header("Controller")]
    public CategoryToMiniGameImageController miniGameController;
    public AudioManagerDonimal audioManager;

    [Header("Satu Template untuk Semua Soal")]
    [Tooltip("Satu GameObject yang dipakai ulang untuk seluruh soal.")]
    public GameObject questionObject;

    [Tooltip("RawImage pada template yang gambarnya akan diganti.")]
    public RawImage questionImage;

    [Header("Fallback Lokal")]
    [Tooltip("Daftar huruf dan tekstur lokal. questionObject lama hanya dipakai untuk kompatibilitas scene.")]
    public List<BunyiQuestionVisual> questionVisuals = new List<BunyiQuestionVisual>();

    [Tooltip("Aktifkan hanya jika soal lokal boleh dipakai saat server gagal.")]
    public bool useLocalFallbackWhenServerFails = false;

    [Header("Question Settings")]
    public bool preventImmediateRepeat = true;
    public bool playOpeningAudio = true;
    public float questionStartDelay = 0.1f;

    private Coroutine questionCoroutine;
    private string currentLetter = "";
    private bool questionActive = false;

    // --- DYNAMIC API VARIABLES ---
    private List<GameQuestionData> serverQuestions = new List<GameQuestionData>();
    private bool isFetchingQuestions = false;
    private int requestVersion;
    private GameQuestionData currentServerQuestion;
    private GameQuestionData lastPresentedServerQuestion;

    private void Start()
    {
        ResolveQuestionTemplate();
        HideAllQuestionVisuals();
    }

    private void OnDisable()
    {
        StopQuestion();
    }

    public void StartQuestion()
    {
        requestVersion++;
        isFetchingQuestions = false;
        
        // Default digunakan jika nanti harus fallback ke soal lokal.
        ScoreManager.Instance?.UseDefaultQuestionPoint();

if (questionCoroutine != null)
        {
            StopCoroutine(questionCoroutine);
            questionCoroutine = null;
        }

        ResolveQuestionTemplate();
        questionActive = false;
        HideAllQuestionVisuals();

        if (serverQuestions.Count == 0 && !isFetchingQuestions)
        {
            FetchQuestionsFromServer();
        }
        else if (serverQuestions.Count > 0)
        {
            SetupDynamicQuestion();
        }

    }

    private void FetchQuestionsFromServer()
    {
        isFetchingQuestions = true;
        Debug.Log("[BunyiQuestion] Fetching questions from server...");

        if (DonimalAPI.Instance == null)
        {
            isFetchingQuestions = false;
            HandleServerFailure("DonimalAPI tidak ditemukan.");
            return;
        }

        // Jika offline mode aktif, langsung pakai soal lokal tanpa sentuh server
        if (DonimalAPI.Instance.offlineMode)
        {
            isFetchingQuestions = false;
            useLocalFallbackWhenServerFails = true;
            HandleServerFailure("Offline mode aktif. Gunakan soal lokal.");
            return;
        }

        FetchQuestionsForMode(
            new[] { "tebak_bunyi", "bunyi_huruf", "bunyi" },
            0
        );
    }

    private void FetchQuestionsForMode(string[] modes, int modeIndex)
    {
        if (modeIndex >= modes.Length)
        {
            isFetchingQuestions = false;
            HandleServerFailure(
                "Server tidak mengirim soal valid untuk mode " +
                "tebak_bunyi, bunyi_huruf, maupun bunyi."
            );
            return;
        }

        int version = requestVersion;
        string mode = modes[modeIndex];
        DonimalAPI.Instance.GetRandomQuestions(
            mode,
            50,
            onSuccess: data =>
            {
                if (version != requestVersion) return;
                serverQuestions.Clear();

                if (data != null)
                {
                    foreach (GameQuestionData question in data)
                    {
                        if (IsValidServerQuestion(question))
                        {
                            serverQuestions.Add(question);
                        }
                    }
                }

                if (serverQuestions.Count > 0)
                {
                    isFetchingQuestions = false;
                    Debug.Log(
                        "[BunyiQuestion] Soal ditemukan menggunakan mode server: " +
                        mode
                    );
                    SetupDynamicQuestion();
                    return;
                }

                Debug.LogWarning(
                    "[BunyiQuestion] Mode " + mode +
                    " kosong atau tidak memiliki correct_answer/media_url; " +
                    "mencoba alias berikutnya."
                );
                FetchQuestionsForMode(modes, modeIndex + 1);
            },
            onError: error =>
            {
                if (version != requestVersion) return;
                Debug.LogWarning(
                    "[BunyiQuestion] Mode " + mode + " gagal: " + error +
                    ". Mencoba alias berikutnya."
                );
                FetchQuestionsForMode(modes, modeIndex + 1);
            }
        );
    }

    private bool IsValidServerQuestion(GameQuestionData question)
    {
        return question != null &&
               !string.IsNullOrWhiteSpace(question.correct_answer) &&
               !string.IsNullOrWhiteSpace(question.media_url);
    }

    private void HandleServerFailure(string error)
    {
        if (useLocalFallbackWhenServerFails)
        {
            Debug.LogWarning(
                "[BunyiQuestion] Gagal mengambil soal server: " + error +
                " Menggunakan fallback lokal."
            );
            questionCoroutine = StartCoroutine(StartQuestionRoutine(true));
            return;
        }

        questionActive = false;
        questionCoroutine = null;
        HideAllQuestionVisuals();

        if (miniGameController != null)
        {
            miniGameController.SetCurrentCorrectAnswer("");
        }

        Debug.LogError(
            "[BunyiQuestion] Soal server gagal dimuat: " + error +
            " Mode server-only aktif; perbaiki endpoint/API agar mengirim media_url."
        );
    }

    private void SetupDynamicQuestion()
    {
        if (serverQuestions.Count == 0) return;

        int randIdx = Random.Range(0, serverQuestions.Count);
        currentServerQuestion = serverQuestions[randIdx];
        lastPresentedServerQuestion = currentServerQuestion;

        
        // Poin soal mengikuti game_questions.score_point dari server.
        ScoreManager.Instance?.SetCurrentQuestionPoint(
            currentServerQuestion.score_point
        );

string targetLetter = NormalizeLetter(currentServerQuestion.correct_answer);

        currentLetter = targetLetter;
        questionCoroutine = StartCoroutine(StartQuestionRoutine(false));
    }

    private IEnumerator StartQuestionRoutine(bool useLocalFallback)
    {
        if (questionStartDelay > 0f) yield return new WaitForSeconds(questionStartDelay);

        if (miniGameController == null) yield break;

        if (useLocalFallback)
        {
            List<string> availableLetters = GetAvailableVisualLetters();

            // Jika tidak ada visual di Inspector, generate A-Z dari LetterSpriteProvider
            if (availableLetters.Count == 0)
            {
                LetterSpriteProvider provider = FindFirstObjectByType<LetterSpriteProvider>();
                if (provider != null)
                {
                    for (char c = 'A'; c <= 'Z'; c++)
                    {
                        Sprite spr = provider.GetSprite(c.ToString());
                        if (spr != null) availableLetters.Add(c.ToString());
                    }
                }
            }

            if (availableLetters.Count == 0)
            {
                // Hardcoded A-Z sebagai last resort jika LetterSpriteProvider juga kosong
                for (char c = 'A'; c <= 'Z'; c++) availableLetters.Add(c.ToString());
            }

            string selectedLetter = SelectRandomLetter(availableLetters);
            currentLetter = NormalizeLetter(selectedLetter);
        }
        
        string correctAnswer = "huruf:" + currentLetter;
        miniGameController.SetCurrentCorrectAnswer(correctAnswer);

        bool visualFound = ShowOnlySelectedQuestion(currentLetter);

        if (!visualFound)
        {
            miniGameController.SetCurrentCorrectAnswer("");
            questionCoroutine = null;
            yield break;
        }

        // Optional: Download Texture if server provided one and inject to the visual object
        if (!useLocalFallback && currentServerQuestion != null && !string.IsNullOrEmpty(currentServerQuestion.media_url))
        {
            if (DonimalAPI.Instance != null)
            {
                int version = requestVersion;
                questionObject.SetActive(false);
                questionImage.texture = null;
                DonimalAPI.Instance.GetTexture(currentServerQuestion.media_url,
                    onSuccess: (tex) =>
                    {
                        if (version != requestVersion) return;
                        ResolveQuestionTemplate();
                        if (questionImage != null)
                        {
                            questionImage.texture = tex;
                            questionObject.SetActive(true);
                            questionActive = true;
                        }
                    },
                    onError: (err) => { Debug.LogError("[BunyiQuestion] Gagal unduh gambar: " + err); });
            }
        }

        if (!useLocalFallback && currentServerQuestion != null)
        {
            ServerQuestionAudioUtility.Play(currentServerQuestion, "[BunyiQuestion]");
        }
        else
        {
            if (playOpeningAudio && audioManager != null)
            {
                audioManager.PlaySubGameStartAudio("huruf", "tebak_bunyi");
            }

            // Visual lokal tidak memiliki ID soal server untuk pencocokan katalog.
            Debug.LogWarning($"[BunyiQuestion] Huruf {currentLetter.ToUpper()} memakai visual lokal tanpa ID soal; audio soal memerlukan ID dari server dan pasangan di LocalQuestionAudioCatalog.");
        }

        if (useLocalFallback) questionActive = true;
        questionCoroutine = null;
    }

    private string SelectRandomLetter(List<string> availableLetters)
    {
        if (availableLetters.Count == 1) return NormalizeLetter(availableLetters[0]);

        string selected = NormalizeLetter(availableLetters[Random.Range(0, availableLetters.Count)]);
        if (preventImmediateRepeat && !string.IsNullOrEmpty(currentLetter))
        {
            int safety = 0;
            while (selected == currentLetter && safety < 30)
            {
                selected = NormalizeLetter(availableLetters[Random.Range(0, availableLetters.Count)]);
                safety++;
            }
        }
        return selected;
    }

    private List<string> GetAvailableVisualLetters()
    {
        List<string> result = new List<string>();

        if (questionVisuals == null)
        {
            return result;
        }

        foreach (BunyiQuestionVisual visual in questionVisuals)
        {
            if (visual == null || visual.localTexture == null)
            {
                continue;
            }

            string letter = NormalizeLetter(visual.letter);
            if (!string.IsNullOrEmpty(letter) && !result.Contains(letter))
            {
                result.Add(letter);
            }
        }

        return result;
    }

    private bool ShowOnlySelectedQuestion(string selectedLetter)
    {
        HideAllQuestionVisuals();
        ResolveQuestionTemplate();

        if (questionObject == null || questionImage == null)
        {
            Debug.LogError(
                "[BunyiQuestion] Template tunggal atau RawImage tidak ditemukan."
            );
            return false;
        }

        string normalizedSelectedLetter = NormalizeLetter(selectedLetter);

        BunyiQuestionVisual selectedVisual = null;
        if (questionVisuals != null)
        {
            selectedVisual = questionVisuals.Find(
                visual =>
                    visual != null &&
                    NormalizeLetter(visual.letter) == normalizedSelectedLetter
            );
        }

        if (selectedVisual != null && selectedVisual.localTexture != null)
        {
            questionImage.texture = selectedVisual.localTexture.texture;
        }
        else
        {
            // Coba pakai sprite dari LetterSpriteProvider sebagai texture
            LetterSpriteProvider provider = FindFirstObjectByType<LetterSpriteProvider>();
            Sprite spr = provider != null ? provider.GetSprite(normalizedSelectedLetter) : null;
            if (spr != null)
            {
                questionImage.texture = spr.texture;
            }
            else if (currentServerQuestion == null || string.IsNullOrEmpty(currentServerQuestion.media_url))
            {
                Debug.LogWarning(
                    "[BunyiQuestion] Sprite huruf " + normalizedSelectedLetter.ToUpper() +
                    " tidak ditemukan di LetterSpriteProvider. Pastikan file ada di Resources/LearningLetters/huruf/ " +
                    "dan Texture Type = Sprite (2D and UI)."
                );
                // Lanjut tampil soal meskipun tanpa gambar huruf
            }
        }

        questionObject.SetActive(true);
        return true;
    }

    private void ResolveQuestionTemplate()
    {
        // Cari dari Inspector list dulu
        if (questionObject == null && questionVisuals != null)
        {
            foreach (BunyiQuestionVisual visual in questionVisuals)
            {
                if (visual != null && visual.questionObject != null)
                {
                    questionObject = visual.questionObject;
                    break;
                }
            }
        }

        // Fallback: cari dari hierarchy scene (misal bunyi/soal1/soal atau bunyi/soal)
        if (questionObject == null)
        {
            // Cari child bernama "soal1", "soal", dst. di dalam parent controller
            Transform[] allChildren = GetComponentsInChildren<Transform>(true);
            foreach (Transform child in allChildren)
            {
                if (child == null || child == transform) continue;
                string n = child.name.Trim().ToLowerInvariant();
                if (n == "soal" || n.StartsWith("soal"))
                {
                    RawImage ri = child.GetComponentInChildren<RawImage>(true);
                    if (ri != null)
                    {
                        questionObject = child.gameObject;
                        questionImage = ri;
                        break;
                    }
                }
            }
        }

        if (questionImage == null && questionObject != null)
        {
            questionImage = questionObject.GetComponentInChildren<RawImage>(true);
        }

        // Jika tidak ada RawImage, tambahkan otomatis
        if (questionImage == null && questionObject != null)
        {
            questionImage = questionObject.AddComponent<RawImage>();
            questionImage.raycastTarget = false;
            Debug.LogWarning("[BunyiQuestion] RawImage ditambahkan otomatis ke " + questionObject.name);
        }
    }

    private void HideAllQuestionVisuals()
    {
        if (questionVisuals != null)
        {
            foreach (BunyiQuestionVisual visual in questionVisuals)
            {
                if (visual != null && visual.questionObject != null)
                    visual.questionObject.SetActive(false);
            }
        }

        if (questionImage != null) questionImage.texture = null;
        if (questionObject != null)
        {
            questionObject.SetActive(false);
        }
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
        isFetchingQuestions = false;
        ServerQuestionAudioUtility.Stop();
        if (questionCoroutine != null)
        {
            StopCoroutine(questionCoroutine);
            questionCoroutine = null;
        }

        questionActive = false;
        HideAllQuestionVisuals();
        if (miniGameController != null) miniGameController.SetCurrentCorrectAnswer("");
        currentLetter = "";
    }

    private string NormalizeLetter(string value)
    {
        return RfidAnswerUtility.Normalize("huruf", value);
    }

    public bool IsQuestionActive() { return questionActive; }
}
