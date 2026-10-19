using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

public class HitungQuestionController : MonoBehaviour
{
    [Header("Controller")]
    public CategoryToMiniGameImageController miniGameController;
    public MiniGameAnswerController miniGameAnswerController;

    [Header("Root Menghitung")]
    [Tooltip("Isi dengan parent 'hitung' yang berisi soal1, soal2, soal3, dst. Ini akan dijadikan template layout.")]
    public Transform hitungRoot;

    [Header("Question Settings")]
    public bool randomQuestion = true;
    public bool preventImmediateRepeat = true;

    [Header("Pop Up Jawaban")]
    public float startScale = 0f;
    public float overshootScale = 1.2f;
    public float popUpDuration = 0.25f;
    public float settleDuration = 0.15f;
    public float answerDisplayDuration = 0.8f;

    private class HitungQuestionData
    {
        public string answerNumber;
        public GameObject questionObject;
        public GameObject soalObject;
        public GameObject answerObject;
        public Transform answerTransform;
        public Vector3 originalAnswerScale;
    }

    private readonly List<HitungQuestionData> questions = new List<HitungQuestionData>();
    private HitungQuestionData currentQuestion;
    private int currentIndex = -1;
    private int previousIndex = -1;
    private string currentAnswer = "";

    private bool questionActive = false;
    private bool answerCompleted = false;
    private bool isAnimating = false;
    private bool questionTextureReady;
    private bool answerVisualReady;
    private Coroutine answerCoroutine;

    // --- DYNAMIC API VARIABLES ---
    private List<GameQuestionData> serverQuestions = new List<GameQuestionData>();
    private bool isFetchingQuestions = false;
    private GameQuestionData currentServerQuestion;
    private int requestVersion;
    private int currentServerQuestionIndex;

    private void Awake()
    {
        if (hitungRoot == null)
        {
            hitungRoot = transform;
        }

        BuildQuestionList();
    }

    private void Start()
    {
        HideAllQuestions();
    }

    private void BuildQuestionList()
    {
        questions.Clear();
        if (hitungRoot == null) return;

        Transform[] allTransforms = hitungRoot.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in allTransforms)
        {
            if (child == null || child == hitungRoot) continue;
            string objectName = child.name.Trim();
            if (!objectName.StartsWith("soal", System.StringComparison.OrdinalIgnoreCase)) continue;
            if (objectName.Equals("soal", System.StringComparison.OrdinalIgnoreCase)) continue;

            string numberText = objectName.Substring(4).Trim();
            if (!int.TryParse(numberText, out int parsedNumber)) continue;

            Transform questionVisual = child.Find("soal");
            Transform answerTransform = child.Find(parsedNumber.ToString());

            if (questionVisual == null || answerTransform == null) continue;

            HitungQuestionData data = new HitungQuestionData
            {
                answerNumber = parsedNumber.ToString(),
                questionObject = child.gameObject,
                soalObject = questionVisual.gameObject,
                answerObject = answerTransform.gameObject,
                answerTransform = answerTransform,
                originalAnswerScale = answerTransform.localScale
            };

            if (Mathf.Approximately(data.originalAnswerScale.x, 0f)) data.originalAnswerScale.x = 1f;
            if (Mathf.Approximately(data.originalAnswerScale.y, 0f)) data.originalAnswerScale.y = 1f;

            questions.Add(data);
        }

        questions.Sort((a, b) => int.Parse(a.answerNumber).CompareTo(int.Parse(b.answerNumber)));

        // Struktur baru: hitung/soal dengan child soal dan answer.
        if (questions.Count == 0)
        {
            Transform template = FindSingleTemplate(hitungRoot);
            if (template != null)
            {
                Transform questionVisual = template.Find("soal");
                Transform answerVisual = FindAnswerVisual(template);
                if (questionVisual != null && answerVisual != null)
                {
                    // Simpan semua referensi sebelum menambah komponen UI.
                    // Pada beberapa konfigurasi Editor, AddComponent dapat membuat
                    // referensi Transform yang sedang dipakai menjadi tidak valid.
                    GameObject templateObject = template.gameObject;
                    GameObject questionVisualObject = questionVisual.gameObject;
                    GameObject answerVisualObject = answerVisual.gameObject;
                    Vector3 answerScale = NormalizeScale(answerVisual.localScale);

                    questions.Add(new HitungQuestionData
                    {
                        answerNumber = "",
                        questionObject = templateObject,
                        soalObject = questionVisualObject,
                        answerObject = answerVisualObject,
                        answerTransform = answerVisual,
                        originalAnswerScale = answerScale
                    });

                    EnsureGraphic(questionVisualObject, "soal");
                    EnsureAnswerGraphic(answerVisualObject);
                }
            }
        }

        Debug.Log("[HitungQuestion] TOTAL TEMPLATE: " + questions.Count);
    }

    private Transform FindSingleTemplate(Transform root)
    {
        foreach (Transform child in root)
        {
            if (child.Find("soal") != null && FindAnswerVisual(child) != null) return child;

            Transform nested = FindSingleTemplate(child);
            if (nested != null) return nested;
        }
        return null;
    }

    private Transform FindAnswerVisual(Transform template)
    {
        Transform namedAnswer = template.Find("answer");
        if (namedAnswer != null) return namedAnswer;

        Transform indonesianAnswer = template.Find("jawaban");
        if (indonesianAnswer != null) return indonesianAnswer;

        foreach (Transform child in template)
        {
            if (!child.name.Equals("soal", System.StringComparison.OrdinalIgnoreCase)) return child;
        }
        return null;
    }

    private Vector3 NormalizeScale(Vector3 scale)
    {
        if (Mathf.Approximately(scale.x, 0f)) scale.x = 1f;
        if (Mathf.Approximately(scale.y, 0f)) scale.y = 1f;
        if (Mathf.Approximately(scale.z, 0f)) scale.z = 1f;
        return scale;
    }

    private void EnsureGraphic(GameObject target, string label)
    {
        if (target.GetComponent<RawImage>() != null || target.GetComponent<Image>() != null) return;
        target.AddComponent<RawImage>();
        Debug.LogWarning("[HitungQuestion] RawImage ditambahkan otomatis pada " + label + ".");
    }

    private void EnsureAnswerGraphic(GameObject answerObject)
    {
        if (answerObject == null ||
            answerObject.GetComponentInChildren<RawImage>(true) != null ||
            answerObject.GetComponentInChildren<Image>(true) != null) return;

        GameObject imageObject = new GameObject(
            "AnswerImage", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
        imageObject.transform.SetParent(answerObject.transform, false);
        RectTransform rect = imageObject.GetComponent<RectTransform>();
        rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(300f, 300f);
        imageObject.GetComponent<RawImage>().raycastTarget = false;

        SpriteRenderer oldRenderer = answerObject.GetComponent<SpriteRenderer>();
        if (oldRenderer != null) oldRenderer.enabled = false;
    }

    public void StartQuestion()
    {
        
        // Default digunakan jika nanti harus fallback ke soal lokal.
        ScoreManager.Instance?.UseDefaultQuestionPoint();

        StopRunningCoroutine();
        ServerQuestionAudioUtility.Stop();
        requestVersion++;
        questionActive = false;
        answerCompleted = false;
        isAnimating = false;
        currentServerQuestionIndex = 0;

        // Hierarchy dapat berubah saat template lama soal0-soal9 dihapus.
        // Buang semua referensi Unity yang objeknya sudah dihancurkan.
        questions.RemoveAll(q =>
            q == null ||
            q.questionObject == null ||
            q.soalObject == null ||
            q.answerObject == null ||
            q.answerTransform == null);

        if (questions.Count == 0) BuildQuestionList();
        HideAllQuestions();
        if (questions.Count == 0)
        {
            Debug.LogError("[HitungQuestion] Template tidak ditemukan. Gunakan hierarchy hitung/soal/soal dan hitung/soal/answer.");
            return;
        }

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
        int version = requestVersion;
        Debug.Log("[HitungQuestion] Fetching questions from server...");

        if (DonimalAPI.Instance == null)
        {
            Debug.LogError("[HitungQuestion] DonimalAPI tidak ditemukan. Mode server-only aktif.");
            isFetchingQuestions = false;
            SetupLocalFallbackQuestion();
            return;
        }

        // Offline mode – langsung ke local fallback
        if (DonimalAPI.Instance.offlineMode)
        {
            isFetchingQuestions = false;
            Debug.LogWarning("[HitungQuestion] Offline mode aktif. Menggunakan fallback lokal.");
            SetupLocalFallbackQuestion();
            return;
        }

        FetchQuestionsForMode(new[] { "menghitung", "hitung", "hitung_benda" }, 0, version);
    }

    private void FetchQuestionsForMode(string[] modes, int modeIndex, int version)
    {
        if (version != requestVersion) return;
        if (modeIndex >= modes.Length)
        {
            isFetchingQuestions = false;
            Debug.LogWarning("[HitungQuestion] Server tidak menemukan soal. Menggunakan fallback lokal.");
            SetupLocalFallbackQuestion();
            return;
        }

        string mode = modes[modeIndex];
        DonimalAPI.Instance.GetRandomQuestions(mode, 50,
            data =>
            {
                if (version != requestVersion) return;
                if (data != null && data.Count > 0)
                {
                    serverQuestions = data;
#if UNITY_EDITOR
                    LocalQuestionAudioCatalog catalog = Resources.Load<LocalQuestionAudioCatalog>("LocalQuestionAudioCatalog");
                    if (catalog != null) catalog.ImportHitungQuestionIds(data);
                    else Debug.LogWarning("[HitungQuestion] Katalog lokal tidak ditemukan; ID belum dapat disimpan.");
#endif
                    currentServerQuestionIndex = 0;
                    isFetchingQuestions = false;
                    Debug.Log("[HitungQuestion] Soal ditemukan menggunakan mode server: " + mode);
                    SetupDynamicQuestion();
                    return;
                }

                Debug.LogWarning("[HitungQuestion] Mode " + mode + " kosong, mencoba alias berikutnya.");
                FetchQuestionsForMode(modes, modeIndex + 1, version);
            },
            error =>
            {
                if (version != requestVersion) return;
                Debug.LogWarning("[HitungQuestion] Mode " + mode + " gagal: " + error +
                                 ". Mencoba alias berikutnya.");
                FetchQuestionsForMode(modes, modeIndex + 1, version);
            });
    }

    private void SetupDynamicQuestion()
    {
        if (serverQuestions.Count == 0) return;

        currentServerQuestionIndex = Mathf.Clamp(
            currentServerQuestionIndex,
            0,
            serverQuestions.Count - 1
        );
        currentServerQuestion = serverQuestions[currentServerQuestionIndex];

        Debug.Log(
            "[HitungQuestion] Menampilkan soal " +
            (currentServerQuestionIndex + 1) +
            " dari " + serverQuestions.Count + "."
        );

        
        // Poin soal mengikuti game_questions.score_point dari server.
        ScoreManager.Instance?.SetCurrentQuestionPoint(
            currentServerQuestion.score_point
        );

string targetAnswer = NormalizeNumber(currentServerQuestion.correct_answer);

        // Cari template UI berdasarkan angka jawaban (agar pop-up jawabannya benar)
        currentQuestion = questions.Find(q => q.answerNumber == targetAnswer);

        // Satu template digunakan untuk semua jawaban dari server.
        if (currentQuestion == null && questions.Count == 1 && string.IsNullOrEmpty(questions[0].answerNumber))
        {
            currentQuestion = questions[0];
            EnsureAnswerGraphic(currentQuestion.answerObject);
        }

        if (currentQuestion == null)
        {
            Debug.LogError($"[HitungQuestion] Tidak ada template soal di Inspector untuk angka {targetAnswer}! Buatlah GameObject soal{targetAnswer} di Hierarchy.");
            return;
        }

        EnsureAnswerGraphic(currentQuestion.answerObject);
        currentAnswer = targetAnswer;
        int version = requestVersion;
        questionTextureReady = false;
        answerVisualReady = false;

        // Download Gambar dan Timpa ke RawImage (jika ada) di dalam soalObject
        if (DonimalAPI.Instance != null && !string.IsNullOrEmpty(currentServerQuestion.media_url))
        {
            DonimalAPI.Instance.GetTexture(currentServerQuestion.media_url,
                onSuccess: (tex) =>
                {
                    if (version != requestVersion) return;
                    questionTextureReady = ServerQuestionImageUtility.ApplyTexture(
                        currentQuestion.soalObject, tex, "[HitungQuestion]");
                    TryActivateDynamicQuestion(version, targetAnswer);
                },
                onError: (err) =>
                {
                    if (version != requestVersion) return;
                    Debug.LogError("[HitungQuestion] Gambar soal gagal dimuat; audio tidak diputar " +
                                   "agar tidak berpasangan dengan gambar lama. " + err);
                }
            );
        }
        else
        {
            Debug.LogError("[HitungQuestion] media_url soal kosong; gambar dan audio soal tidak ditampilkan.");
        }

        if (miniGameController != null) miniGameController.SetCurrentCorrectAnswer("angka:" + currentAnswer);
        LoadAnswerFromLearningTable(targetAnswer, version);
    }

    private void LoadAnswerFromLearningTable(string answer, int version)
    {
        DonimalAPI.Instance.GetLearningMaterial(answer,
            material =>
            {
                if (version != requestVersion) return;
                if (material == null || string.IsNullOrWhiteSpace(material.media_url))
                {
                    Debug.LogWarning("[HitungQuestion] Tabel belajar tidak memiliki media_url untuk nilai " + answer + "; jawaban tetap ditampilkan tanpa gambar.");
                    answerVisualReady = true;
                    TryActivateDynamicQuestion(version, answer);
                    return;
                }

                DonimalAPI.Instance.GetTexture(material.media_url,
                    texture =>
                    {
                        if (version != requestVersion) return;
                        if (!ServerQuestionImageUtility.ApplyTexture(
                                currentQuestion.answerObject, texture, "[HitungQuestion Jawaban]")) return;
                        answerVisualReady = true;
                        TryActivateDynamicQuestion(version, answer);
                    },
                    error =>
                    {
                        if (version != requestVersion) return;
                        Debug.LogWarning("[HitungQuestion] Gagal mengunduh gambar tabel belajar (jawaban tetap ditampilkan tanpa gambar): " + error);
                        // Tetap lanjutkan agar soal tidak berhenti menunggu selamanya.
                        answerVisualReady = true;
                        TryActivateDynamicQuestion(version, answer);
                    });
            },
            error =>
            {
                if (version != requestVersion) return;
                Debug.LogWarning("[HitungQuestion] Gagal mengambil jawaban dari tabel belajar (soal tetap dilanjutkan): " + error);
                answerVisualReady = true;
                TryActivateDynamicQuestion(version, answer);
            });
    }

    private void TryActivateDynamicQuestion(int version, string answer)
    {
        if (version != requestVersion || !questionTextureReady || !answerVisualReady ||
            questionActive || answerCompleted) return;
        if (!ShowQuestion()) return;
        questionActive = true;
        ServerQuestionAudioUtility.Play(currentServerQuestion, "[HitungQuestion]");
        Debug.Log("[HITUNG DINAMIS] Soal dan jawaban tabel belajar siap: " + answer);
    }

    private void SetDynamicAnswerText(GameObject answerObject, string answer)
    {
        if (answerObject == null) return;

        TMP_Text tmpText = answerObject.GetComponentInChildren<TMP_Text>(true);
        if (tmpText != null)
        {
            tmpText.text = answer;
            return;
        }

        Text legacyText = answerObject.GetComponentInChildren<Text>(true);
        if (legacyText != null)
        {
            legacyText.text = answer;
            return;
        }

        // Template tunggal belum mempunyai tampilan angka. Buat child UI baru
        // agar Transform milik object jawaban tidak diganti/dihancurkan Unity.
        GameObject textObject = new GameObject(
            "AnswerText",
            typeof(RectTransform),
            typeof(CanvasRenderer),
            typeof(TextMeshProUGUI)
        );
        textObject.transform.SetParent(answerObject.transform, false);

        RectTransform rect = textObject.GetComponent<RectTransform>();
        rect.anchorMin = new Vector2(0.5f, 0.5f);
        rect.anchorMax = new Vector2(0.5f, 0.5f);
        rect.anchoredPosition = Vector2.zero;
        rect.sizeDelta = new Vector2(300f, 200f);

        TextMeshProUGUI generatedText = textObject.GetComponent<TextMeshProUGUI>();
        generatedText.text = answer;
        generatedText.fontSize = 120f;
        generatedText.alignment = TextAlignmentOptions.Center;
        generatedText.color = new Color(0.05f, 0.25f, 0.65f, 1f);
        generatedText.raycastTarget = false;

        SpriteRenderer oldRenderer = answerObject.GetComponent<SpriteRenderer>();
        if (oldRenderer != null) oldRenderer.enabled = false;

        Debug.Log("[HitungQuestion] AnswerText dibuat otomatis dengan jawaban: " + answer);
    }

    private void SetupLocalFallbackQuestion()
    {
        // Jika tidak ada template di scene, coba rebuild sekali lagi
        if (questions.Count == 0) BuildQuestionList();

        // Jika masih kosong setelah rebuild, buat entry dummy agar tidak crash
        if (questions.Count == 0)
        {
            Debug.LogError(
                "[HitungQuestion] Tidak ada template soal di scene. " +
                "Buat GameObject hitung/soal/ dengan child bernama 'soal' (gambar soal) " +
                "dan child 'answer'/'jawaban' (angka jawaban), lalu assign ke Hitung Root di Inspector."
            );
            return;
        }

        if (randomQuestion)
        {
            currentIndex = Random.Range(0, questions.Count);
            if (preventImmediateRepeat && questions.Count > 1)
            {
                int safety = 0;
                while (currentIndex == previousIndex && safety < 50)
                {
                    currentIndex = Random.Range(0, questions.Count);
                    safety++;
                }
            }
        }
        else
        {
            currentIndex++;
            if (currentIndex >= questions.Count) currentIndex = 0;
        }
        previousIndex = currentIndex;
        currentQuestion = questions[currentIndex];
        currentAnswer = currentQuestion.answerNumber;

        // Jika template hanya satu (dinamis), pilih angka 1-10 secara acak
        if (string.IsNullOrEmpty(currentAnswer))
        {
            currentAnswer = Random.Range(1, 11).ToString();
            currentQuestion.answerNumber = currentAnswer;
        }

        ShowQuestion();
        // Tampilkan angka jawaban via text pada answerObject agar ada sesuatu yang muncul
        SetDynamicAnswerText(currentQuestion.answerObject, currentAnswer);

        questionActive = true;
        if (miniGameController != null) miniGameController.SetCurrentCorrectAnswer("angka:" + currentAnswer);
        Debug.Log($"[HITUNG LOKAL] Soal Aktif: {currentAnswer}");

        // Load gambar lokal dari katalog saat offline.
        LocalQuestionImageCatalog imageCatalog = Resources.Load<LocalQuestionImageCatalog>("LocalQuestionImageCatalog");
        if (imageCatalog != null && !string.IsNullOrEmpty(currentAnswer))
        {
            Sprite questionSprite = imageCatalog.GetQuestionImage(currentAnswer, "Angka", "Hitung Benda");
            if (questionSprite != null)
                ServerQuestionImageUtility.ApplySprite(currentQuestion.soalObject, questionSprite, "[HitungLokal Soal]");

            Sprite answerSprite = imageCatalog.GetAnswerImage(currentAnswer, "Angka");
            if (answerSprite != null)
                ServerQuestionImageUtility.ApplySprite(currentQuestion.answerObject, answerSprite, "[HitungLokal Jawaban]");
        }
    }

    public void CheckNumber(string inputData)
    {
        if (!questionActive || answerCompleted || isAnimating) return;

        string inputNumber = NormalizeNumber(inputData);
        if (string.IsNullOrEmpty(inputNumber)) return;

        if (inputNumber == currentAnswer) HandleCorrectAnswer();
        else HandleWrongAnswer(inputNumber);
    }

    private void HandleCorrectAnswer()
    {
        answerCompleted = true;
        questionActive = false;
        if (answerCoroutine != null) StopCoroutine(answerCoroutine);
        answerCoroutine = StartCoroutine(ShowAnswerPopUpRoutine());
    }

    private IEnumerator ShowAnswerPopUpRoutine()
    {
        if (currentQuestion == null || currentQuestion.answerObject == null || currentQuestion.answerTransform == null) yield break;
        isAnimating = true;

        Transform answerTransform = currentQuestion.answerTransform;
        Vector3 normalScale = currentQuestion.originalAnswerScale;
        Vector3 startVector = normalScale * startScale;
        Vector3 overshootVector = normalScale * overshootScale;

        answerTransform.localScale = startVector;
        currentQuestion.answerObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < popUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = popUpDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / popUpDuration);
            answerTransform.localScale = Vector3.Lerp(startVector, overshootVector, t);
            yield return null;
        }
        answerTransform.localScale = overshootVector;

        elapsed = 0f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            float t = settleDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / settleDuration);
            answerTransform.localScale = Vector3.Lerp(overshootVector, normalScale, t);
            yield return null;
        }
        answerTransform.localScale = normalScale;
        isAnimating = false;

        if (answerDisplayDuration > 0f) yield return new WaitForSeconds(answerDisplayDuration);

        if (miniGameAnswerController != null) miniGameAnswerController.CompleteHitungBenda();
        answerCoroutine = null;
    }

    private void HandleWrongAnswer(string inputNumber)
    {
        if (miniGameAnswerController != null) miniGameAnswerController.ShowHitungBendaWrongFeedback();
    }

    /// <summary>
    /// Melanjutkan sesi Hitung Benda ke soal server berikutnya.
    /// Mengembalikan false jika seluruh soal dalam hasil fetch sudah selesai.
    /// </summary>
    public bool TryShowNextServerQuestion()
    {
        if (serverQuestions == null ||
            currentServerQuestionIndex + 1 >= serverQuestions.Count)
        {
            return false;
        }

        requestVersion++;
        ServerQuestionAudioUtility.Stop();
        StopRunningCoroutine();
        questionActive = false;
        answerCompleted = false;
        isAnimating = false;
        currentServerQuestionIndex++;
        HideAllQuestions();
        SetupDynamicQuestion();
        return true;
    }

    private bool ShowQuestion()
    {
        if (currentQuestion == null ||
            currentQuestion.questionObject == null ||
            currentQuestion.soalObject == null ||
            currentQuestion.answerObject == null ||
            currentQuestion.answerTransform == null)
        {
            questionActive = false;
            Debug.LogError("[HitungQuestion] Template soal sudah dihapus atau belum lengkap. " +
                           "Pastikan parent soal memiliki child soal dan child jawaban.");
            return false;
        }
        currentQuestion.questionObject.SetActive(true);
        currentQuestion.soalObject.SetActive(true);
        currentQuestion.answerObject.SetActive(false);
        currentQuestion.answerTransform.localScale = currentQuestion.originalAnswerScale;
        return true;
    }

    private void HideAllQuestions()
    {
        foreach (HitungQuestionData question in questions)
        {
            if (question == null) continue;
            if (question.questionObject != null) question.questionObject.SetActive(false);
            if (question.soalObject != null) question.soalObject.SetActive(true);
            if (question.answerObject != null) question.answerObject.SetActive(false);
            if (question.answerTransform != null) question.answerTransform.localScale = question.originalAnswerScale;
        }
    }

    public void StopQuestion()
    {
        requestVersion++;
        ServerQuestionAudioUtility.Stop();
        StopRunningCoroutine();
        questionActive = false;
        answerCompleted = false;
        isAnimating = false;
        if (miniGameController != null) miniGameController.SetCurrentCorrectAnswer("");
        HideAllQuestions();
        currentQuestion = null;
        currentAnswer = "";
    }

    private void StopRunningCoroutine()
    {
        if (answerCoroutine != null)
        {
            StopCoroutine(answerCoroutine);
            answerCoroutine = null;
        }
    }

    private string NormalizeNumber(string value)
    {
        return RfidAnswerUtility.Normalize("angka", value);
    }

    public bool IsQuestionActive() => questionActive;
    public string GetCurrentAnswer() => currentAnswer;
}
