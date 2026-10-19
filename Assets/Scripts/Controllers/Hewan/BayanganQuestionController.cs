using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class BayanganQuestionController : MonoBehaviour
{
    [Header("Controller")]
    public CategoryToMiniGameImageController miniGameController;
    public MiniGameAnswerController miniGameAnswerController;

    [Header("Satu Template Soal")]
    public GameObject questionObject;
    public GameObject answerObject;
    public Image questionImage;
    public RawImage questionRawImage;

    [Header("Question Settings")]
    public int questionLimit = 5;
    public bool preventImmediateRepeat = true;

    [Header("Animasi Jawaban")]
    public float startScale = 0f;
    public float overshootScale = 1.2f;
    public float popUpDuration = 0.25f;
    public float settleDuration = 0.15f;
    public float answerDisplayDuration = 0.8f;

    private List<GameQuestionData> serverQuestions = new List<GameQuestionData>();
    private GameQuestionData currentServerQuestion;
    private GameQuestionData lastPresentedServerQuestion;
    private int previousQuestionIndex = -1;
    private bool isFetchingQuestions;
    private bool questionActive;
    private bool answerCompleted;
    private bool questionTextureReady;
    private bool answerTextureReady;
    private string currentAnswer = "";
    private string activeServerMode = "";
    private string pendingAnimalInput = "";
    private Transform answerTransform;
    private Vector3 originalAnswerScale = Vector3.one;
    private Coroutine answerCoroutine;
    private int requestVersion;

    private void Awake()
    {
        ResolveTemplate();
        if (miniGameAnswerController == null)
            miniGameAnswerController = FindFirstObjectByType<MiniGameAnswerController>(FindObjectsInactive.Include);
    }

    private void Start()
    {
        ResolveTemplate();
        HideTemplate();
    }

    private void OnDisable()
    {
        StopQuestion();
    }

    private void ResolveTemplate()
    {
        if (questionObject == null)
        {
            Transform template = FindTemplate(transform);
            if (template != null) questionObject = template.gameObject;
        }

        if (questionObject != null)
        {
            Transform visual = questionObject.transform.Find("soal");
            if (visual != null && questionImage == null && questionRawImage == null)
            {
                questionRawImage = visual.GetComponent<RawImage>();
                questionImage = visual.GetComponent<Image>();
                if (questionRawImage == null && questionImage == null)
                    questionRawImage = visual.gameObject.AddComponent<RawImage>();
            }

            if (answerObject == null)
            {
                Transform answer = FindAnswerChild(questionObject.transform);
                if (answer != null) answerObject = answer.gameObject;
            }
        }

        if (answerObject != null)
        {
            answerTransform = answerObject.transform;
            originalAnswerScale = SafeScale(answerTransform.localScale);
            EnsureAnswerGraphic();
        }
    }

    private Transform FindTemplate(Transform root)
    {
        foreach (Transform child in root)
        {
            Transform question = child.Find("soal");
            Transform answer = FindAnswerChild(child);
            if (question != null && answer != null) return child;
            Transform nested = FindTemplate(child);
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

    private void EnsureAnswerGraphic()
    {
        if (answerObject.GetComponentInChildren<RawImage>(true) != null ||
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
        ResolveTemplate();
        StopAnswerCoroutine();
        ServerQuestionAudioUtility.Stop();
        requestVersion++;
        questionActive = false;
        answerCompleted = false;
        currentAnswer = "";
        pendingAnimalInput = "";
        ClearQuestionImage();
        HideTemplate();
        miniGameController?.SetCurrentCorrectAnswer("");

        if (questionObject == null || answerObject == null ||
            (questionImage == null && questionRawImage == null))
        {
            Debug.LogError("[BayanganQuestion] Gunakan hierarchy bayangan/soal/soal dan bayangan/soal/jawaban.");
            return;
        }

        if (serverQuestions.Count > 0) SetupDynamicQuestion();
        else FetchQuestionsFromServer();
    }

    private void FetchQuestionsFromServer()
    {
        if (isFetchingQuestions) return;
        DonimalAPI api = ResolveApi();
        if (api == null)
        {
            Debug.LogError("[BayanganQuestion] DonimalAPI tidak ditemukan.");
            return;
        }

        isFetchingQuestions = true;
        int version = requestVersion;
        FetchQuestionsForMode(
            api,
            new[] { "tebak_bayangan", "bayangan", "tebak_habitat", "habitat" },
            0,
            version);
    }

    private void FetchQuestionsForMode(DonimalAPI api, string[] modes, int modeIndex, int version)
    {
        if (version != requestVersion) return;
        if (modeIndex >= modes.Length)
        {
            isFetchingQuestions = false;
            Debug.LogWarning("[BayanganQuestion] Server tidak menemukan soal. Menggunakan fallback lokal.");
            SetupLocalFallbackQuestion();
            return;
        }

        string mode = modes[modeIndex];
        api.GetRandomQuestions(mode, 50,
            data =>
            {
                if (version != requestVersion) return;
                if (data != null && data.Count > 0)
                {
                    // Satu hewan hanya dimainkan sekali meskipun server mengirim beberapa ID.
                    serverQuestions = new List<GameQuestionData>();
                    var animals = new HashSet<string>();
                    foreach (GameQuestionData question in data)
                    {
                        if (question == null || string.IsNullOrWhiteSpace(question.media_url)) continue;
                        string animal = mode == "habitat" || mode == "tebak_habitat"
                            ? GetAnimalNameFromMediaUrl(question.media_url)
                            : NormalizeAnimal(question.correct_answer);
                        if (!string.IsNullOrEmpty(animal) && animals.Add(animal)) serverQuestions.Add(question);
                    }
                    if (serverQuestions.Count == 0)
                    {
                        FetchQuestionsForMode(api, modes, modeIndex + 1, version);
                        return;
                    }
                    activeServerMode = mode;
                    isFetchingQuestions = false;
                    Debug.Log("[BayanganQuestion] Soal ditemukan menggunakan mode server: " + mode);
                    SetupDynamicQuestion();
                    return;
                }

                Debug.LogWarning("[BayanganQuestion] Mode " + mode +
                                 " kosong, mencoba alias berikutnya.");
                FetchQuestionsForMode(api, modes, modeIndex + 1, version);
            },
            error =>
            {
                if (version != requestVersion) return;
                Debug.LogWarning("[BayanganQuestion] Mode " + mode + " gagal: " + error +
                                 ". Mencoba alias berikutnya.");
                FetchQuestionsForMode(api, modes, modeIndex + 1, version);
            });
    }

    private void SetupDynamicQuestion()
    {
        DonimalAPI api = ResolveApi();
        if (api == null)
        {
            Debug.LogError("[BayanganQuestion] DonimalAPI tidak ditemukan saat menyiapkan soal.");
            return;
        }

        int selectedIndex = SelectQuestionIndex();
        if (selectedIndex < 0) return;
        previousQuestionIndex = selectedIndex;
        currentServerQuestion = serverQuestions[selectedIndex];
        lastPresentedServerQuestion = currentServerQuestion;
        currentAnswer = NormalizeAnimal(currentServerQuestion.correct_answer);

        if (activeServerMode == "tebak_habitat" || activeServerMode == "habitat")
        {
            string migratedAnswer = GetAnimalNameFromMediaUrl(currentServerQuestion.media_url);
            if (!string.IsNullOrEmpty(migratedAnswer))
            {
                Debug.LogWarning("[BayanganQuestion] Data lama Habitat dikonversi ke Bayangan. " +
                                 "correct_answer '" + currentAnswer + "' diganti menjadi nama hewan '" +
                                 migratedAnswer + "'.");
                currentAnswer = migratedAnswer;
            }
        }
        if (string.IsNullOrEmpty(currentAnswer) || string.IsNullOrWhiteSpace(currentServerQuestion.media_url))
        {
            Debug.LogError("[BayanganQuestion] correct_answer atau media_url kosong.");
            return;
        }

        int version = requestVersion;
        questionTextureReady = false;
        answerTextureReady = false;
        questionObject.SetActive(true);
        answerObject.SetActive(false);
        answerTransform.localScale = originalAnswerScale;
        ScoreManager.Instance?.SetCurrentQuestionPoint(currentServerQuestion.score_point);
        miniGameController?.SetCurrentCorrectAnswer("hewan:" + currentAnswer);
        ServerQuestionAudioUtility.Play(currentServerQuestion, "[BayanganQuestion]", "Tebak Bayangan " + currentAnswer);

        api.GetTexture(currentServerQuestion.media_url,
            texture =>
            {
                if (version != requestVersion) return;
                SetQuestionImage(texture);
                questionTextureReady = true;
                TryActivateQuestion(version);
            },
            error =>
            {
                if (version != requestVersion) return;
                Debug.LogWarning("[BayanganQuestion] Gagal download gambar bayangan (soal tetap ditampilkan tanpa gambar): " + error);
                questionTextureReady = true;
                TryActivateQuestion(version);
            });

        api.GetLearningMaterial(currentAnswer,
            material =>
            {
                if (version != requestVersion) return;
                if (material == null || string.IsNullOrWhiteSpace(material.media_url))
                {
                    Debug.LogWarning("[BayanganQuestion] Tabel belajar tidak memiliki media_url untuk " + currentAnswer + "; jawaban tampil tanpa gambar.");
                    answerTextureReady = true;
                    TryActivateQuestion(version);
                    return;
                }

                if (!material.media_url.ToLowerInvariant().Contains(currentAnswer.ToLowerInvariant()))
                {
                    string correctedUrl = "/static/belajar/hewan/" +
                                          currentAnswer.ToLowerInvariant().Replace(" ", "%20") + ".png";
                    Debug.LogWarning("[BayanganQuestion] Tabel belajar mengembalikan media yang tidak cocok: " +
                                     material.media_url + ". Menggunakan: " + correctedUrl);
                    DownloadLearningAnswer(api, correctedUrl, currentAnswer, version, true);
                    return;
                }
                DownloadLearningAnswer(api, material.media_url, currentAnswer, version, false);
            },
            error =>
            {
                if (version != requestVersion) return;
                Debug.LogWarning("[BayanganQuestion] Gagal mengambil tabel belajar (soal tetap dilanjutkan): " + error);
                answerTextureReady = true;
                TryActivateQuestion(version);
            });
    }

    private void DownloadLearningAnswer(
        DonimalAPI api, string mediaUrl, string animal, int version, bool isFallback)
    {
        api.GetTexture(mediaUrl,
            texture =>
            {
                if (version != requestVersion) return;
                if (!ServerQuestionImageUtility.ApplyTexture(
                        answerObject, texture, "[BayanganQuestion Jawaban]")) return;
                answerTextureReady = true;
                TryActivateQuestion(version);
            },
            error =>
            {
                if (version != requestVersion) return;
                if (!isFallback)
                {
                    string fallbackUrl = "/static/belajar/hewan/" +
                                         animal.ToLowerInvariant().Replace(" ", "%20") + ".png";
                    Debug.Log("[BayanganQuestion] URL tabel belajar tidak cocok: " + mediaUrl +
                                     ". Mencoba gambar berdasarkan correct_answer: " + fallbackUrl);
                    DownloadLearningAnswer(api, fallbackUrl, animal, version, true);
                    return;
                }

                Debug.LogWarning("[BayanganQuestion] Gagal download gambar jawaban (soal tetap dilanjutkan): " + error +
                               " | URL terakhir: " + mediaUrl);
                // Tetap aktifkan soal agar permainan tidak berhenti.
                answerTextureReady = true;
                TryActivateQuestion(version);
            });
    }

    private void SetupLocalFallbackQuestion()
    {
        // Kumpulkan label hewan dari LocalQuestionImageCatalog.
        LocalQuestionImageCatalog imageCatalog = Resources.Load<LocalQuestionImageCatalog>("LocalQuestionImageCatalog");
        List<string> labels = imageCatalog != null
            ? imageCatalog.GetLabels("Hewan", "Tebak Bayangan")
            : new List<string>();

        if (labels.Count == 0)
        {
            Debug.LogWarning("[BayanganLokal] Tidak ada soal di LocalQuestionImageCatalog > Hewan > Tebak Bayangan.");
            return;
        }

        int selectedIndex = Random.Range(0, labels.Count);
        if (preventImmediateRepeat && labels.Count > 1)
        {
            int safety = 0;
            while (selectedIndex == previousQuestionIndex && safety++ < 50)
                selectedIndex = Random.Range(0, labels.Count);
        }
        previousQuestionIndex = selectedIndex;
        currentAnswer = labels[selectedIndex];

        questionObject.SetActive(true);
        answerObject.SetActive(false);
        answerTransform.localScale = originalAnswerScale;
        ScoreManager.Instance?.UseDefaultQuestionPoint();
        miniGameController?.SetCurrentCorrectAnswer("hewan:" + currentAnswer);
        questionActive = true;

        Sprite questionSprite = imageCatalog.GetQuestionImage(currentAnswer, "Hewan", "Tebak Bayangan");
        if (questionSprite != null)
        {
            if (questionRawImage != null) { questionRawImage.texture = questionSprite.texture; questionRawImage.gameObject.SetActive(true); }
            else if (questionImage != null) { questionImage.sprite = questionSprite; questionImage.preserveAspect = true; questionImage.gameObject.SetActive(true); }
        }

        Sprite answerSprite = imageCatalog.GetAnswerImage(currentAnswer, "Hewan");
        if (answerSprite != null)
            ServerQuestionImageUtility.ApplySprite(answerObject, answerSprite, "[BayanganLokal Jawaban]");

        Debug.Log("[BayanganLokal] Soal hewan aktif: " + currentAnswer);
    }

    private DonimalAPI ResolveApi()
    {
        if (DonimalAPI.Instance != null) return DonimalAPI.Instance;
        return FindFirstObjectByType<DonimalAPI>(FindObjectsInactive.Include);
    }

    private void TryActivateQuestion(int version)
    {
        if (version != requestVersion || !questionTextureReady || questionActive || answerCompleted) return;
        questionActive = true;
        Debug.Log("[BayanganQuestion] Soal siap: " + currentAnswer +
                  " | gambar jawaban siap: " + answerTextureReady);

        if (!string.IsNullOrEmpty(pendingAnimalInput))
        {
            string queuedInput = pendingAnimalInput;
            pendingAnimalInput = "";
            CheckAnimal(queuedInput);
        }
    }

    public void CheckAnimal(string inputData)
    {
        string input = NormalizeAnimal(inputData);
        if (string.IsNullOrEmpty(input)) return;
        if (answerCompleted) return;
        if (!questionActive)
        {
            pendingAnimalInput = input;
            Debug.LogWarning("[BayanganQuestion] Gambar soal masih dimuat. Jawaban disimpan: " + input);
            return;
        }
        if (input == currentAnswer)
        {
            answerCompleted = true;
            questionActive = false;
            answerCoroutine = StartCoroutine(ShowAnswerRoutine());
        }
        else
        {
            miniGameAnswerController?.ShowDeskripsiHewanWrongFeedback();
        }
    }

    private IEnumerator ShowAnswerRoutine()
    {
        Vector3 start = originalAnswerScale * startScale;
        Vector3 overshoot = originalAnswerScale * overshootScale;
        answerTransform.localScale = start;
        answerObject.SetActive(true);
        yield return AnimateScale(start, overshoot, popUpDuration);
        yield return AnimateScale(overshoot, originalAnswerScale, settleDuration);
        if (answerDisplayDuration > 0f) yield return new WaitForSeconds(answerDisplayDuration);
        miniGameAnswerController?.CompleteDeskripsiHewan();
        answerCoroutine = null;
    }

    private IEnumerator AnimateScale(Vector3 from, Vector3 to, float duration)
    {
        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            answerTransform.localScale = Vector3.Lerp(from, to,
                duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration));
            yield return null;
        }
        answerTransform.localScale = to;
    }

    private int SelectQuestionIndex()
    {
        if (serverQuestions == null || serverQuestions.Count == 0) return -1;
        if (serverQuestions.Count == 1) return 0;
        int selected = Random.Range(0, serverQuestions.Count);
        int safety = 0;
        while (preventImmediateRepeat && selected == previousQuestionIndex && safety++ < 50)
            selected = Random.Range(0, serverQuestions.Count);
        return selected;
    }

    private void SetQuestionImage(Texture2D texture)
    {
        if (questionRawImage != null)
        {
            questionRawImage.texture = texture;
            questionRawImage.gameObject.SetActive(true);
        }
        else if (questionImage != null)
        {
            questionImage.sprite = Sprite.Create(texture,
                new Rect(0f, 0f, texture.width, texture.height), new Vector2(0.5f, 0.5f));
            questionImage.preserveAspect = true;
            questionImage.gameObject.SetActive(true);
        }
    }

    private void ClearQuestionImage()
    {
        if (questionRawImage != null) questionRawImage.texture = null;
        if (questionImage != null) questionImage.sprite = null;
    }

    private void HideTemplate()
    {
        if (questionObject != null) questionObject.SetActive(false);
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
        ServerQuestionAudioUtility.Stop();
        StopAnswerCoroutine();
        questionActive = false;
        answerCompleted = false;
        currentServerQuestion = null;
        currentAnswer = "";
        pendingAnimalInput = "";
        miniGameController?.SetCurrentCorrectAnswer("");
        ClearQuestionImage();
        HideTemplate();
    }

    private void StopAnswerCoroutine()
    {
        if (answerCoroutine == null) return;
        StopCoroutine(answerCoroutine);
        answerCoroutine = null;
    }

    private Vector3 SafeScale(Vector3 scale)
    {
        if (Mathf.Approximately(scale.x, 0f)) scale.x = 1f;
        if (Mathf.Approximately(scale.y, 0f)) scale.y = 1f;
        if (Mathf.Approximately(scale.z, 0f)) scale.z = 1f;
        return scale;
    }

    private string NormalizeAnimal(string value)
    {
        return RfidAnswerUtility.Normalize("hewan", value);
    }

    private string GetAnimalNameFromMediaUrl(string mediaUrl)
    {
        if (string.IsNullOrWhiteSpace(mediaUrl)) return "";
        string cleanUrl = mediaUrl.Split('?')[0].Replace("%20", " ");
        int slashIndex = cleanUrl.LastIndexOf('/');
        string fileName = slashIndex >= 0 ? cleanUrl.Substring(slashIndex + 1) : cleanUrl;
        int extensionIndex = fileName.LastIndexOf('.');
        if (extensionIndex > 0) fileName = fileName.Substring(0, extensionIndex);
        return NormalizeAnimal(fileName.Replace("bayangan ", "").Replace("habitat ", ""));
    }

    public bool IsQuestionActive() { return questionActive; }
    public string GetCurrentAnswer() { return currentAnswer; }
    public GameQuestionData GetCurrentServerQuestion() { return currentServerQuestion; }
}
