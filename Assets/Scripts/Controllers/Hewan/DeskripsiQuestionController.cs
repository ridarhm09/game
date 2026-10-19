using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class DeskripsiQuestionController : MonoBehaviour
{
    [Header("Controller")]
    public CategoryToMiniGameImageController miniGameController;
    public MiniGameAnswerController miniGameAnswerController;

    [Header("Root Tebak Deskripsi")]
    [Tooltip("Isi dengan parent 'deskripsi' yang berisi soal1 sampai soal10. Akan dijadikan template layout.")]
    public Transform deskripsiRoot;

    [Header("Question Settings")]
    public bool randomQuestion = true;
    public bool preventImmediateRepeat = true;

    [Header("Animasi Pop Up Hewan")]
    public float startScale = 0f;
    public float overshootScale = 1.2f;
    public float popUpDuration = 0.25f;
    public float settleDuration = 0.15f;
    public float animalDisplayDuration = 0.8f;

    private class DeskripsiQuestionData
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

    private readonly List<DeskripsiQuestionData> questions = new List<DeskripsiQuestionData>();
    private DeskripsiQuestionData currentQuestion;
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
        if (deskripsiRoot == null)
        {
            deskripsiRoot = transform;
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
        if (deskripsiRoot == null) return;

        Transform[] allTransforms = deskripsiRoot.GetComponentsInChildren<Transform>(true);
        foreach (Transform candidate in allTransforms)
        {
            if (candidate == null || candidate == deskripsiRoot) continue;
            string objectName = candidate.name.ToLower().Trim();
            if (!objectName.StartsWith("soal") || objectName == "soal") continue;

            Transform questionVisual = candidate.Find("soal");
            if (questionVisual == null) continue;

            Transform animalTransform = FindAnimalChild(candidate);
            if (animalTransform == null) continue;

            string animalName = NormalizeAnimalName(animalTransform.name);
            DeskripsiQuestionData data = new DeskripsiQuestionData
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
            Transform template = FindSingleTemplate(deskripsiRoot);
            if (template != null)
            {
                Transform questionVisual = template.Find("soal");
                Transform answerVisual = FindAnswerChild(template);
                if (questionVisual != null && answerVisual != null)
                {
                    GameObject templateObject = template.gameObject;
                    GameObject questionObject = questionVisual.gameObject;
                    GameObject answerObject = answerVisual.gameObject;
                    Vector3 answerScale = GetSafeScale(answerVisual.localScale);
                    questions.Add(new DeskripsiQuestionData
                    {
                        questionName = template.name,
                        animalName = "",
                        questionObject = templateObject,
                        questionVisual = questionObject,
                        animalObject = answerObject,
                        animalTransform = answerVisual,
                        originalAnimalScale = answerScale,
                        isDynamicTemplate = true
                    });
                }
            }
        }
        Debug.Log("[DeskripsiQuestion] TOTAL TEMPLATE: " + questions.Count);
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
        questionActive = false;
        answerCompleted = false;
        isAnimating = false;
        pendingAnimalInput = "";

        if (questions.Count == 0) BuildQuestionList();
        HideAllQuestions();
        if (questions.Count == 0)
        {
            Debug.LogError("[DeskripsiQuestion] Template tidak ditemukan. Gunakan hierarchy deskripsi/soal/soal dan deskripsi/soal/jawaban.");
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
        Debug.Log("[DeskripsiQuestion] Fetching questions from server...");

        if (DonimalAPI.Instance != null)
        {
            DonimalAPI.Instance.GetRandomQuestions("tebak_deskripsi", 50,
                onSuccess: (data) =>
                {
                    if (version != requestVersion) return;
                    serverQuestions = data ?? new List<GameQuestionData>();
                    isFetchingQuestions = false;
                    if (serverQuestions.Count > 0) SetupDynamicQuestion();
                    else
                    {
                        Debug.LogWarning("[DeskripsiQuestion] Soal server kosong. Menggunakan fallback lokal.");
                        SetupLocalFallbackQuestion();
                    }
                },
                onError: (err) =>
                {
                    if (version != requestVersion) return;
                    Debug.LogError("[DeskripsiQuestion] Gagal fetch soal: " + err);
                    isFetchingQuestions = false;
                    SetupLocalFallbackQuestion();
                });
        }
        else
        {
            Debug.LogError("[DeskripsiQuestion] DonimalAPI tidak ditemukan! Menggunakan fallback lokal.");
            isFetchingQuestions = false;
            SetupLocalFallbackQuestion();
        }
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
            Debug.LogError($"[DeskripsiQuestion] Tidak ada template soal di Inspector untuk hewan {targetAnimal}! Buatlah GameObject dengan child {targetAnimal}.");
            return;
        }

        EnsureAnswerGraphic(currentQuestion.animalObject);
        EnsureQuestionGraphic(currentQuestion.questionVisual);
        int version = requestVersion;
        questionTextureReady = false;
        answerVisualReady = false;

        ShowQuestionOnly();
        ServerQuestionAudioUtility.Play(currentServerQuestion, "[DeskripsiQuestion]");

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
                    ServerQuestionImageUtility.ApplyTexture(currentQuestion.questionVisual, tex, "[DeskripsiQuestion]");
                    questionTextureReady = true;
                    TryActivateDynamicQuestion(version, targetAnimal);
                },
                onError: (err) =>
                {
                    Debug.LogError("[DeskripsiQuestion] Gagal unduh gambar soal: " + err);
                }
            );
        }

        LoadAnswerFromLearningTable(targetAnimal, version);
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
        RawImage rawImage = imageObject.GetComponent<RawImage>();
        rawImage.raycastTarget = false;

        SpriteRenderer oldRenderer = answerObject.GetComponent<SpriteRenderer>();
        if (oldRenderer != null) oldRenderer.enabled = false;
    }

    private void EnsureQuestionGraphic(GameObject questionObject)
    {
        if (questionObject == null || questionObject.GetComponent<RawImage>() != null ||
            questionObject.GetComponent<Image>() != null) return;
        questionObject.AddComponent<RawImage>();
        Debug.LogWarning("[DeskripsiQuestion] RawImage ditambahkan otomatis pada tampilan soal.");
    }

    private void LoadAnswerFromLearningTable(string animal, int version)
    {
        DonimalAPI.Instance.GetLearningMaterial(animal,
            material =>
            {
                if (version != requestVersion) return;
                if (material == null || string.IsNullOrWhiteSpace(material.media_url))
                {
                    Debug.LogError("[DeskripsiQuestion] Tabel belajar tidak memiliki media_url untuk " + animal + ".");
                    return;
                }

                // Record hewan dari server kadang menunjuk folder huruf.
                // Koreksi sebelum request agar tidak selalu gagal satu kali per soal.
                if (material.media_url.IndexOf("/static/belajar/huruf/",
                        System.StringComparison.OrdinalIgnoreCase) >= 0)
                {
                    string correctedUrl = "/static/belajar/hewan/" +
                                          animal.ToLowerInvariant().Replace(" ", "%20") + ".png";
                    Debug.Log("[DeskripsiQuestion] Koreksi URL gambar hewan: " +
                              material.media_url + ". Menggunakan: " + correctedUrl);
                    DownloadLearningAnswerImage(correctedUrl, animal, version, true);
                    return;
                }

                DownloadLearningAnswerImage(material.media_url, animal, version, false);
            },
            error =>
            {
                if (version == requestVersion)
                    Debug.LogError("[DeskripsiQuestion] Gagal mengambil jawaban dari tabel belajar: " + error);
            });
    }

    private void DownloadLearningAnswerImage(string mediaUrl, string animal, int version, bool isFallback)
    {
        DonimalAPI.Instance.GetTexture(mediaUrl,
                    texture =>
                    {
                        if (version != requestVersion) return;
                        if (!ServerQuestionImageUtility.ApplyTexture(
                                currentQuestion.animalObject, texture, "[DeskripsiQuestion Jawaban]")) return;
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
                            Debug.LogWarning("[DeskripsiQuestion] URL tabel belajar gagal: " + mediaUrl +
                                             ". Mencoba: " + fallbackUrl);
                            DownloadLearningAnswerImage(fallbackUrl, animal, version, true);
                            return;
                        }

                        Debug.LogError("[DeskripsiQuestion] Gagal mengunduh gambar jawaban: " + error +
                                       " | URL terakhir: " + mediaUrl);
                    });
    }

    private void TryActivateDynamicQuestion(int version, string animal)
    {
        if (version != requestVersion || !questionTextureReady || questionActive || answerCompleted) return;
        questionActive = true;
        Debug.Log("[DESKRIPSI DINAMIS] Soal siap menerima jawaban: " + animal +
                  " | gambar jawaban siap: " + answerVisualReady);

        if (!string.IsNullOrEmpty(pendingAnimalInput))
        {
            string queuedInput = pendingAnimalInput;
            pendingAnimalInput = "";
            Debug.Log("[DeskripsiQuestion] Memeriksa jawaban keyboard yang menunggu: " + queuedInput);
            CheckAnimal(queuedInput);
        }
    }

    private void SetupLocalFallbackQuestion()
    {
        if (randomQuestion)
        {
            currentQuestionIndex = Random.Range(0, questions.Count);
            if (preventImmediateRepeat && questions.Count > 1)
            {
                int safety = 0;
                while (currentQuestionIndex == previousQuestionIndex && safety < 50)
                {
                    currentQuestionIndex = Random.Range(0, questions.Count);
                    safety++;
                }
            }
        }
        else
        {
            currentQuestionIndex++;
            if (currentQuestionIndex >= questions.Count) currentQuestionIndex = 0;
        }

        previousQuestionIndex = currentQuestionIndex;
        currentQuestion = questions[currentQuestionIndex];

        ShowQuestionOnly();
        questionActive = true;

        if (miniGameController != null) miniGameController.SetCurrentCorrectAnswer("hewan:" + currentQuestion.animalName);
        Debug.Log($"[DESKRIPSI LOKAL] Soal Aktif: {currentQuestion.animalName}");

        // Load gambar lokal dari katalog saat offline mode.
        LocalQuestionImageCatalog imageCatalog = Resources.Load<LocalQuestionImageCatalog>("LocalQuestionImageCatalog");
        if (imageCatalog != null)
        {
            Sprite questionSprite = imageCatalog.GetQuestionImage(currentQuestion.animalName, "Hewan", "Tebak Deskripsi");
            if (questionSprite != null)
                ServerQuestionImageUtility.ApplySprite(currentQuestion.questionVisual, questionSprite, "[DeskripsiLokal Soal]");

            Sprite answerSprite = imageCatalog.GetAnswerImage(currentQuestion.animalName, "Hewan");
            if (answerSprite != null)
            {
                ServerQuestionImageUtility.ApplySprite(currentQuestion.animalObject, answerSprite, "[DeskripsiLokal Jawaban]");
            }
        }
        else
        {
            Debug.Log("[DeskripsiLokal] LocalQuestionImageCatalog tidak ditemukan di Resources/. Tampilkan soal tanpa gambar.");
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
            Debug.LogWarning("[DeskripsiQuestion] Gambar masih dimuat. Jawaban disimpan: " + inputAnimal);
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
        if (miniGameAnswerController != null) miniGameAnswerController.CompleteDeskripsiHewan();

        popUpCoroutine = null;
    }

    private void HandleWrongAnswer(string inputAnimal)
    {
        if (miniGameAnswerController != null) miniGameAnswerController.ShowDeskripsiHewanWrongFeedback();
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
        foreach (DeskripsiQuestionData question in questions)
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
