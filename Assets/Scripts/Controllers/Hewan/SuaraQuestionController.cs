using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class SuaraQuestionController : MonoBehaviour
{
    [Header("Controller")]
    public CategoryToMiniGameImageController miniGameController;
    public MiniGameAnswerController miniGameAnswerController;

    [Header("Root Tebak Suara")]
    [Tooltip("Isi dengan parent 'suara' yang berisi soal1 sampai soal10. Akan dijadikan template layout.")]
    public Transform suaraRoot;

    [Header("Audio Suara Hewan")]
    public AudioSource animalAudioSource;
    public bool playAnimalSound = true;

    [Header("Question Settings")]
    public bool randomQuestion = true;
    public bool preventImmediateRepeat = true;

    [Header("Animasi Pop Up Hewan")]
    public float startScale = 0f;
    public float overshootScale = 1.2f;
    public float popUpDuration = 0.25f;
    public float settleDuration = 0.15f;
    public float animalDisplayDuration = 0.8f;

    private class SuaraQuestionData
    {
        public string questionName;
        public string animalName;
        public GameObject questionObject;
        public GameObject questionVisual;
        public GameObject animalObject;
        public Transform animalTransform;
        public Vector3 originalAnimalScale;
        public bool isDynamicTemplate;
    }

    private readonly List<SuaraQuestionData> questions = new List<SuaraQuestionData>();
    private SuaraQuestionData currentQuestion;

    private int currentQuestionIndex = -1;
    private int previousQuestionIndex = -1;

    private bool questionActive = false;
    private bool answerCompleted = false;
    private bool isAnimating = false;
    private bool questionTextureReady;
    private bool answerVisualReady;
    private string pendingAnimalInput = "";
    private Coroutine popUpCoroutine;
    private int requestVersion;

    // --- DYNAMIC API VARIABLES ---
    private List<GameQuestionData> serverQuestions = new List<GameQuestionData>();
    private bool isFetchingQuestions = false;
    private GameQuestionData currentServerQuestion;

    private void Awake()
    {
        if (suaraRoot == null)
        {
            suaraRoot = transform;
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
        if (suaraRoot == null) return;

        Transform[] allTransforms = suaraRoot.GetComponentsInChildren<Transform>(true);
        foreach (Transform candidate in allTransforms)
        {
            if (candidate == null || candidate == suaraRoot) continue;
            string objectName = candidate.name.ToLower().Trim();
            if (!objectName.StartsWith("soal") || objectName == "soal") continue;

            Transform questionVisual = candidate.Find("soal");
            if (questionVisual == null) continue;

            Transform animalTransform = FindAnimalChild(candidate);
            if (animalTransform == null) continue;

            string animalName = NormalizeAnimalName(animalTransform.name);
            SuaraQuestionData data = new SuaraQuestionData
            {
                questionName = candidate.name,
                animalName = animalName,
                questionObject = candidate.gameObject,
                questionVisual = questionVisual.gameObject,
                animalObject = animalTransform.gameObject,
                animalTransform = animalTransform,
                originalAnimalScale = GetSafeScale(animalTransform.localScale)
            };
            questions.Add(data);
        }

        if (questions.Count == 0)
        {
            Transform template = FindSingleTemplate(suaraRoot);
            if (template != null)
            {
                Transform questionVisual = template.Find("soal");
                Transform answerVisual = FindAnswerChild(template);
                if (questionVisual != null && answerVisual != null)
                {
                    questions.Add(new SuaraQuestionData
                    {
                        questionName = template.name,
                        animalName = "",
                        questionObject = template.gameObject,
                        questionVisual = questionVisual.gameObject,
                        animalObject = answerVisual.gameObject,
                        animalTransform = answerVisual,
                        originalAnimalScale = GetSafeScale(answerVisual.localScale),
                        isDynamicTemplate = true
                    });
                }
            }
        }
        Debug.Log("[SuaraQuestion] TOTAL TEMPLATE: " + questions.Count);
    }

    private Transform FindSingleTemplate(Transform root)
    {
        foreach (Transform child in root)
        {
            if (child.Find("soal") != null && FindAnswerChild(child) != null) return child;
            Transform nested = FindSingleTemplate(child);
            if (nested != null) return nested;
        }
        return null;
    }

    private Transform FindAnswerChild(Transform template)
    {
        Transform answer = template.Find("jawaban");
        if (answer == null) answer = template.Find("answer");
        if (answer != null) return answer;
        foreach (Transform child in template)
        {
            if (!child.name.Equals("soal", System.StringComparison.OrdinalIgnoreCase)) return child;
        }
        return null;
    }

    private Transform FindAnimalChild(Transform questionParent)
    {
        foreach (Transform child in questionParent)
        {
            if (child.name.Equals("soal", System.StringComparison.OrdinalIgnoreCase)) continue;
            return child;
        }
        return null;
    }

    private Vector3 GetSafeScale(Vector3 scale)
    {
        if (Mathf.Approximately(scale.x, 0f)) scale.x = 1f;
        if (Mathf.Approximately(scale.y, 0f)) scale.y = 1f;
        if (Mathf.Approximately(scale.z, 0f)) scale.z = 1f;
        return scale;
    }

    public void StartQuestion()
    {
        
        // Default digunakan jika nanti harus fallback ke soal lokal.
        ScoreManager.Instance?.UseDefaultQuestionPoint();

StopRunningCoroutine();
        requestVersion++;
        StopAnimalAudio();

        questionActive = false;
        answerCompleted = false;
        isAnimating = false;
        pendingAnimalInput = "";

        if (questions.Count == 0) BuildQuestionList();
        HideAllQuestions();
        if (questions.Count == 0)
        {
            Debug.LogError("[SuaraQuestion] Gunakan hierarchy suara/soal/soal dan suara/soal/jawaban.");
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
        Debug.Log("[SuaraQuestion] Fetching questions from server...");

        // Offline mode – langsung ke local fallback
        if (DonimalAPI.Instance == null || DonimalAPI.Instance.offlineMode)
        {
            if (DonimalAPI.Instance == null)
                Debug.LogError("[SuaraQuestion] DonimalAPI tidak ditemukan! Menggunakan fallback lokal.");
            else
                Debug.LogWarning("[SuaraQuestion] Offline mode aktif. Menggunakan fallback lokal.");
            isFetchingQuestions = false;
            SetupLocalFallbackQuestion();
            return;
        }

        DonimalAPI.Instance.GetRandomQuestions("tebak_suara", 50,
            onSuccess: (data) =>
            {
                if (version != requestVersion) return;
                serverQuestions = data ?? new List<GameQuestionData>();
#if UNITY_EDITOR
                LocalQuestionAudioCatalog catalog = Resources.Load<LocalQuestionAudioCatalog>("LocalQuestionAudioCatalog");
                if (catalog != null) catalog.ImportSuaraQuestionIds(serverQuestions);
                else Debug.LogWarning("[SuaraQuestion] Katalog lokal tidak ditemukan; ID belum dapat disimpan.");
#endif
                isFetchingQuestions = false;
                if (serverQuestions.Count > 0) SetupDynamicQuestion();
                else
                {
                    Debug.LogWarning("[SuaraQuestion] Soal server kosong. Menggunakan fallback lokal.");
                    SetupLocalFallbackQuestion();
                }
            },
            onError: (err) =>
            {
                if (version != requestVersion) return;
                Debug.LogError("[SuaraQuestion] Gagal fetch soal: " + err);
                isFetchingQuestions = false;
                SetupLocalFallbackQuestion();
            });
    }

    private void SetupDynamicQuestion()
    {
        if (serverQuestions.Count == 0) return;

        int randIdx = Random.Range(0, serverQuestions.Count);
        currentServerQuestion = serverQuestions[randIdx];

        
        // Poin soal mengikuti game_questions.score_point dari server.
        ScoreManager.Instance?.SetCurrentQuestionPoint(
            currentServerQuestion.score_point
        );

string targetAnimal = NormalizeAnimalName(currentServerQuestion.correct_answer);

        // Find template by expected animal name
        currentQuestion = questions.Find(q => q.animalName == targetAnimal);

        if (currentQuestion == null)
        {
            currentQuestion = questions.Find(q => q.isDynamicTemplate);
            if (currentQuestion != null) currentQuestion.animalName = targetAnimal;
        }

        if (currentQuestion == null)
        {
            Debug.LogError($"[SuaraQuestion] Tidak ada template soal di Inspector untuk hewan {targetAnimal}! Buatlah GameObject dengan child {targetAnimal}.");
            return;
        }

        EnsureAnswerGraphic(currentQuestion.animalObject);
        EnsureQuestionGraphic(currentQuestion.questionVisual);
        int version = requestVersion;
        // Pada Tebak Suara, audio adalah soal utama. Gambar pertanyaan bersifat opsional.
        questionTextureReady = true;
        answerVisualReady = false;

        ShowQuestionOnly();
        ServerQuestionAudioUtility.Play(currentServerQuestion, "[SuaraQuestion]");

        if (miniGameController != null)
        {
            miniGameController.SetCurrentCorrectAnswer("hewan:" + currentQuestion.animalName);
        }

        if (DonimalAPI.Instance != null && !string.IsNullOrEmpty(currentServerQuestion.media_url))
        {
            DonimalAPI.Instance.GetTexture(currentServerQuestion.media_url,
                onSuccess: (tex) =>
                {
                    if (version != requestVersion) return;
                    ServerQuestionImageUtility.ApplyTexture(currentQuestion.questionVisual, tex, "[SuaraQuestion]");
                    questionTextureReady = true;
                    TryActivateDynamicQuestion(version, targetAnimal);
                },
                onError: (err) =>
                {
                    // Gambar soal bersifat opsional pada Tebak Suara (audio adalah soal utama).
                    Debug.LogWarning("[SuaraQuestion] Gagal unduh gambar soal (audio tetap diputar): " + err);
                }
            );
        }

        TryActivateDynamicQuestion(version, targetAnimal);
        LoadAnswerFromLearningTable(targetAnimal, version);
    }

    private void EnsureAnswerGraphic(GameObject answerObject)
    {
        if (answerObject == null || answerObject.GetComponentInChildren<RawImage>(true) != null ||
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

    private void EnsureQuestionGraphic(GameObject questionObject)
    {
        if (questionObject == null || questionObject.GetComponent<RawImage>() != null ||
            questionObject.GetComponent<Image>() != null) return;
        questionObject.AddComponent<RawImage>();
    }

    private void LoadAnswerFromLearningTable(string animal, int version)
    {
        DonimalAPI.Instance.GetLearningMaterial(animal,
            material =>
            {
                if (version != requestVersion) return;
                if (material == null || string.IsNullOrWhiteSpace(material.media_url))
                {
                    Debug.LogWarning("[SuaraQuestion] Tabel belajar tidak memiliki media_url untuk " + animal + "; jawaban tampil tanpa gambar.");
                    return;
                }

                // Sebagian record hewan salah menunjuk folder huruf. Nama hewan
                // saja tidak cukup untuk memvalidasi URL tersebut.
                if (material.media_url.IndexOf("/static/belajar/huruf/",
                        System.StringComparison.OrdinalIgnoreCase) >= 0 ||
                    !material.media_url.ToLowerInvariant().Contains(animal.ToLowerInvariant()))
                {
                    string correctedUrl = "/static/belajar/hewan/" +
                                          animal.ToLowerInvariant().Replace(" ", "%20") + ".png";
                    Debug.Log("[SuaraQuestion] Koreksi URL gambar hewan: " +
                                     material.media_url + ". Menggunakan: " + correctedUrl);
                    DownloadLearningAnswer(correctedUrl, animal, version, true);
                    return;
                }
                DownloadLearningAnswer(material.media_url, animal, version, false);
            },
            error =>
            {
                if (version == requestVersion)
                    Debug.LogWarning("[SuaraQuestion] Gagal mengambil tabel belajar (soal tetap berjalan): " + error);
            });
    }

    private void DownloadLearningAnswer(string mediaUrl, string animal, int version, bool isFallback)
    {
        DonimalAPI.Instance.GetTexture(mediaUrl,
            texture =>
            {
                if (version != requestVersion) return;
                if (!ServerQuestionImageUtility.ApplyTexture(
                        currentQuestion.animalObject, texture, "[SuaraQuestion Jawaban]")) return;
                answerVisualReady = true;
                TryActivateDynamicQuestion(version, animal);
            },
            error =>
            {
                if (version != requestVersion) return;
                if (!isFallback)
                {
                    string fallbackUrl = "/static/belajar/hewan/" +
                                         animal.ToLowerInvariant().Replace(" ", "%20") + ".png";
                    Debug.LogWarning("[SuaraQuestion] URL tabel belajar gagal: " + mediaUrl +
                                     ". Mencoba: " + fallbackUrl);
                    DownloadLearningAnswer(fallbackUrl, animal, version, true);
                    return;
                }
                Debug.LogError("[SuaraQuestion] Gagal download gambar jawaban: " + error +
                               " | URL terakhir: " + mediaUrl);
            });
    }

    private void TryActivateDynamicQuestion(int version, string animal)
    {
        if (version != requestVersion || !questionTextureReady || questionActive || answerCompleted) return;
        questionActive = true;
        Debug.Log("[SUARA DINAMIS] Soal siap: " + animal +
                  " | gambar jawaban siap: " + answerVisualReady);
        if (!string.IsNullOrEmpty(pendingAnimalInput))
        {
            string queuedInput = pendingAnimalInput;
            pendingAnimalInput = "";
            CheckAnimal(queuedInput);
        }
    }

    private static readonly string[] LocalAnimals =
    {
        "kucing", "anjing", "sapi", "kambing", "bebek",
        "ayam", "burung", "ikan", "kelinci", "kuda"
    };
    private int localAnimalIndex = -1;

    private void SetupLocalFallbackQuestion()
    {
        if (questions.Count == 0) BuildQuestionList();

        if (questions.Count == 0)
        {
            Debug.LogError("[SuaraQuestion] Tidak ada template soal di scene. " +
                "Buat hierarchy suara/soal dengan child 'soal' dan satu child lain untuk jawaban.");
            return;
        }

        // Pilih hewan berikutnya dari daftar lokal (rotasi)
        localAnimalIndex = (localAnimalIndex + 1) % LocalAnimals.Length;
        string animal = LocalAnimals[localAnimalIndex];

        // Pakai template pertama (atau satu-satunya) dan assign nama hewan dinamis
        currentQuestion = questions[0];
        currentQuestion.animalName = animal;

        ShowQuestionOnly();
        questionActive = true;

        if (miniGameController != null)
            miniGameController.SetCurrentCorrectAnswer("hewan:" + animal);

        Debug.Log("[SUARA LOKAL] Soal aktif: " + animal);

        // Coba cari audio dari LocalQuestionAudioCatalog
        if (playAnimalSound && animalAudioSource != null)
        {
            AudioClip clip = FindAnimalClip(animal);
            if (clip != null)
            {
                animalAudioSource.Stop();
                animalAudioSource.clip = clip;
                animalAudioSource.Play();
            }
            else
            {
                Debug.LogWarning("[SuaraQuestion] Audio '" + animal + "' belum ada di LocalQuestionAudioCatalog.");
            }
        }
    }

    private void PlayCurrentAnimalSound()
    {
        if (!playAnimalSound) return;
        if (currentServerQuestion != null)
        {
            ServerQuestionAudioUtility.Play(currentServerQuestion, "[SuaraQuestion]");
            return;
        }
        if (animalAudioSource == null) return;
        if (string.IsNullOrEmpty(currentQuestion?.animalName))
        {
            Debug.LogWarning("[SuaraQuestion] Template soal tidak memiliki nama hewan. " +
                "Pastikan child GameObject di dalam template diberi nama sesuai hewan (contoh: 'kucing', 'bebek').");
            return;
        }

        AudioClip clip = FindAnimalClip(currentQuestion.animalName);
        if (clip == null)
        {
            Debug.LogWarning("[SuaraQuestion] Audio untuk '" + currentQuestion.animalName + "' belum diisi di LocalQuestionAudioCatalog.");
            return;
        }

        animalAudioSource.Stop();
        animalAudioSource.clip = clip;
        animalAudioSource.Play();
    }

    private AudioClip FindAnimalClip(string animalName)
    {
        LocalQuestionAudioCatalog catalog = Resources.Load<LocalQuestionAudioCatalog>("LocalQuestionAudioCatalog");
        AudioManagerDonimal manager = FindFirstObjectByType<AudioManagerDonimal>();
        return catalog != null && manager != null
            ? catalog.GetAnimalQuestionClip(animalName, manager.GetActiveCharacterName())
            : null;
    }

    public void ReplayCurrentAnimalSound()
    {
        if (!questionActive || currentQuestion == null) return;
        PlayCurrentAnimalSound();
    }

    private void StopAnimalAudio()
    {
        if (animalAudioSource != null && animalAudioSource.isPlaying)
        {
            animalAudioSource.Stop();
        }
    }

    public void CheckAnimal(string inputData)
    {
        string inputAnimal = NormalizeAnimalName(inputData);
        if (string.IsNullOrEmpty(inputAnimal)) return;

        if (answerCompleted || isAnimating) return;
        if (!questionActive)
        {
            pendingAnimalInput = inputAnimal;
            Debug.LogWarning("[SuaraQuestion] Soal masih dimuat. Jawaban disimpan: " + inputAnimal);
            return;
        }

        if (inputAnimal == currentQuestion.animalName) HandleCorrectAnswer();
        else HandleWrongAnswer(inputAnimal);
    }

    private void HandleCorrectAnswer()
    {
        answerCompleted = true;
        questionActive = false;
        if (popUpCoroutine != null) StopCoroutine(popUpCoroutine);
        popUpCoroutine = StartCoroutine(ShowAnimalPopUpRoutine());
    }

    private IEnumerator ShowAnimalPopUpRoutine()
    {
        isAnimating = true;
        Transform animalTransform = currentQuestion.animalTransform;
        Vector3 normalScale = currentQuestion.originalAnimalScale;
        Vector3 startVector = normalScale * startScale;
        Vector3 overshootVector = normalScale * overshootScale;

        animalTransform.localScale = startVector;
        currentQuestion.animalObject.SetActive(true);

        float elapsed = 0f;
        while (elapsed < popUpDuration)
        {
            elapsed += Time.deltaTime;
            float t = popUpDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / popUpDuration);
            animalTransform.localScale = Vector3.Lerp(startVector, overshootVector, t);
            yield return null;
        }
        animalTransform.localScale = overshootVector;

        elapsed = 0f;
        while (elapsed < settleDuration)
        {
            elapsed += Time.deltaTime;
            float t = settleDuration <= 0f ? 1f : Mathf.Clamp01(elapsed / settleDuration);
            animalTransform.localScale = Vector3.Lerp(overshootVector, normalScale, t);
            yield return null;
        }
        animalTransform.localScale = normalScale;
        isAnimating = false;

        if (animalDisplayDuration > 0f) yield return new WaitForSeconds(animalDisplayDuration);
        if (miniGameAnswerController != null) miniGameAnswerController.CompleteSuaraHewan();

        popUpCoroutine = null;
    }

    private void HandleWrongAnswer(string inputAnimal)
    {
        if (miniGameAnswerController != null) miniGameAnswerController.ShowSuaraHewanWrongFeedback();
    }

    private void ShowQuestionOnly()
    {
        if (currentQuestion == null) return;
        currentQuestion.questionObject.SetActive(true);
        currentQuestion.questionVisual.SetActive(true);
        currentQuestion.animalObject.SetActive(false);
        currentQuestion.animalTransform.localScale = currentQuestion.originalAnimalScale;
    }

    private void HideAllQuestions()
    {
        foreach (SuaraQuestionData question in questions)
        {
            if (question == null) continue;
            if (question.questionObject != null) question.questionObject.SetActive(false);
            if (question.questionVisual != null) question.questionVisual.SetActive(true);
            if (question.animalObject != null) question.animalObject.SetActive(false);
            if (question.animalTransform != null) question.animalTransform.localScale = question.originalAnimalScale;
        }
    }

    public bool TryShowNextServerQuestion()
    {
        if (currentServerQuestion != null)
        {
            serverQuestions.Remove(currentServerQuestion);
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
        ServerQuestionAudioUtility.Stop();
        StopRunningCoroutine();
        StopAnimalAudio();

        questionActive = false;
        answerCompleted = false;
        isAnimating = false;
        if (miniGameController != null) miniGameController.SetCurrentCorrectAnswer("");
        HideAllQuestions();
        currentQuestion = null;
        pendingAnimalInput = "";
    }

    private void StopRunningCoroutine()
    {
        if (popUpCoroutine != null)
        {
            StopCoroutine(popUpCoroutine);
            popUpCoroutine = null;
        }
    }

    private string NormalizeAnimalName(string value)
    {
        return RfidAnswerUtility.Normalize("hewan", value);
    }

    public bool IsQuestionActive() { return questionActive; }
}
