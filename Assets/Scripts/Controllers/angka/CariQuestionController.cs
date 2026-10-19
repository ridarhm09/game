using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class CariQuestionController : MonoBehaviour
{
    private static readonly HashSet<string> IgnoredQuestionIds =
        new HashSet<string>(System.StringComparer.OrdinalIgnoreCase)
        {
            "50d6cfa9-0000-44f2-9856-000000000001",
            "50d6cfa9-0000-44f2-9856-000000000002",
            "50d6cfa9-0000-44f2-9856-000000000003",
            "50d6cfa9-0000-44f2-9856-000000000004",
            "50d6cfa9-0000-44f2-9856-000000000005",
            "50d6cfa9-0000-44f2-9856-000000000006",
            "50d6cfa9-0000-44f2-9856-000000000007",
            "50d6cfa9-0000-44f2-9856-000000000008",
            "50d6cfa9-0000-44f2-9856-000000000009"
        };

    [Header("Controller")]
    public CategoryToMiniGameImageController miniGameController;
    public MiniGameAnswerController miniGameAnswerController;

    [Header("Tampilan Soal Tunggal")]
    public GameObject questionObject;
    public RectTransform cardRect;
    public GameObject frontObject;
    public GameObject backObject;

    [Header("Gambar Jawaban (nama file 0 sampai 9)")]
    public Texture2D[] answerImages;

    [Header("Question Settings")]
    public bool preventImmediateRepeat = true;

    [Header("Animasi Flip")]
    public float flipCloseDuration = 0.2f;
    public float flipOpenDuration = 0.2f;
    public float numberDisplayDuration = 0.8f;

    private readonly List<GameQuestionData> serverQuestions = new List<GameQuestionData>();
    private int requestVersion;
    private GameQuestionData currentServerQuestion;
    private GameQuestionData lastPresentedServerQuestion;
    private string currentTargetNumber = "";
    private string previousQuestionId = "";
    private bool isFetchingQuestions;
    private bool questionActive;
    private bool answerCompleted;
    private bool isFlipping;
    private Coroutine flipCoroutine;
    private bool initialized;

    private void Awake()
    {
        InitializeSession();
    }

    private void InitializeSession()
    {
        if (initialized) return;
        initialized = true;
        ResolveQuestionTemplate();
        ResetSession();
        HideQuestion();
    }

    private void OnDisable()
    {
        PauseQuestion();
    }

    public void StartQuestion()
    {
        InitializeSession();
        requestVersion++;
        isFetchingQuestions = false;
        ScoreManager.Instance?.UseDefaultQuestionPoint();
        ServerQuestionAudioUtility.Stop();
        StopRunningCoroutine();
        ResolveQuestionTemplate();

        questionActive = false;
        answerCompleted = false;
        isFlipping = false;
        currentTargetNumber = "";
        currentServerQuestion = null;
        HideQuestion();

        miniGameController?.SetCurrentCorrectAnswer("");

        if (!HasValidTemplate())
        {
            Debug.LogError(
                "[CariAngka] Template belum lengkap. Pastikan hierarchy " +
                "cari/soal/card/front dan cari/soal/card/back tersedia."
            );
            return;
        }

        ShowCardBack();

        if (serverQuestions.Count > 0)
        {
            SetupDynamicQuestion();
        }
        else if (!isFetchingQuestions)
        {
            FetchQuestionsFromServer();
        }
    }

    public bool HasResumableSession()
    {
        return currentServerQuestion != null &&
               serverQuestions.Count > 0 &&
               !answerCompleted;
    }

    private void ResetSession()
    {
        requestVersion++;
        StopRunningCoroutine();
        ServerQuestionAudioUtility.Stop();
        serverQuestions.Clear();
        currentServerQuestion = null;
        lastPresentedServerQuestion = null;
        currentTargetNumber = "";
        previousQuestionId = "";
        isFetchingQuestions = false;
        questionActive = false;
        answerCompleted = false;
        isFlipping = false;
        miniGameController?.SetCurrentCorrectAnswer("");
    }

    public void ResumeQuestion()
    {
        if (!HasResumableSession()) return;

        ResolveQuestionTemplate();
        ShowCardBack();
        questionActive = true;
        isFlipping = false;
        Debug.Log("[CariAngka] Melanjutkan soal dan skor sebelumnya.");
    }

    public void PauseQuestion()
    {
        StopRunningCoroutine();
        ServerQuestionAudioUtility.Stop();
        questionActive = false;
        isFlipping = false;
        HideQuestion();
    }

    private void ResolveQuestionTemplate()
    {
        if (questionObject == null)
        {
            Transform question = transform.Find("soal");
            if (question != null) questionObject = question.gameObject;
        }

        if (questionObject == null) return;

        Transform questionTransform = questionObject.transform;
        Transform card = questionTransform.Find("card");
        if (card == null) card = questionTransform;
        if (cardRect == null) cardRect = card as RectTransform;

        if (frontObject == null)
        {
            Transform front = card.Find("front");
            if (front != null) frontObject = front.gameObject;
        }

        EnsureFrontGraphic();

        if (backObject == null)
        {
            Transform back = card.Find("back");
            if (back != null) backObject = back.gameObject;
        }

        if (miniGameController == null)
        {
            miniGameController = FindFirstObjectByType<CategoryToMiniGameImageController>(
                FindObjectsInactive.Include
            );
        }

        if (miniGameAnswerController == null)
        {
            miniGameAnswerController = FindFirstObjectByType<MiniGameAnswerController>(
                FindObjectsInactive.Include
            );
        }
    }

    private void EnsureFrontGraphic()
    {
        if (frontObject == null)
        {
            return;
        }

        if (frontObject.GetComponentInChildren<RawImage>(true) != null ||
            frontObject.GetComponentInChildren<Image>(true) != null)
        {
            return;
        }

        RawImage rawImage = frontObject.AddComponent<RawImage>();
        rawImage.color = Color.white;
        rawImage.raycastTarget = false;

        Debug.LogWarning(
            "[CariAngka] RawImage ditambahkan otomatis pada front."
        );
    }

    private bool HasValidTemplate()
    {
        return questionObject != null && cardRect != null &&
               frontObject != null && backObject != null;
    }

    private void FetchQuestionsFromServer()
    {
        if (DonimalAPI.Instance == null)
        {
            HandleServerFailure("DonimalAPI tidak ditemukan.");
            return;
        }

        // Offline mode – skip server
        if (DonimalAPI.Instance.offlineMode)
        {
            HandleServerFailure("Offline mode aktif. Gunakan soal lokal.");
            return;
        }

        int version = requestVersion;
        isFetchingQuestions = true;
        Debug.Log("[CariAngka] Fetching questions from server...");

        DonimalAPI.Instance.GetRandomQuestions(
            "cari_angka",
            50,
            onSuccess: data =>
            {
                if (version != requestVersion) return;
                isFetchingQuestions = false;
                serverQuestions.Clear();

                if (data != null)
                {
                    var audioIdList = new System.Text.StringBuilder("[CariAngka IDs] Daftar soal dari server:");
                    foreach (GameQuestionData question in SelectDigitQuestions(data))
                    {
                        if (question != null)
                            audioIdList.Append("\nAngka: ").Append(question.correct_answer)
                                .Append(" | Question Id: ").Append(question.id);
                        if (IsValidServerQuestion(question)) serverQuestions.Add(question);
                    }
                    Debug.Log(audioIdList.ToString());
                }

                if (serverQuestions.Count == 0)
                {
                    HandleServerFailure(
                        "Server tidak mengirim soal cari_angka dengan correct_answer dan media_url."
                    );
                    return;
                }

                SetupDynamicQuestion();
            },
            onError: error =>
            {
                if (version != requestVersion) return;
                isFetchingQuestions = false;
                HandleServerFailure(error);
            }
        );
    }

    private bool IsValidServerQuestion(GameQuestionData question)
    {
         return !IsIgnoredQuestion(question) &&
             question != null &&
               !string.IsNullOrWhiteSpace(question.correct_answer);
    }

        private bool IsIgnoredQuestion(GameQuestionData question)
        {
         return false;
        }

        private List<GameQuestionData> SelectDigitQuestions(List<GameQuestionData> data)
        {
            var selected = new List<GameQuestionData>(10);
            var selectedDigits = new HashSet<string>();

            foreach (GameQuestionData question in data)
            {
                if (IsIgnoredQuestion(question)) continue;

                string digit = NormalizeNumber(question != null ? question.correct_answer : "");
                if (digit.Length != 1 || !char.IsDigit(digit[0]) || !selectedDigits.Add(digit))
                    continue;

                selected.Add(question);
            }

            selected.Sort((left, right) =>
                int.Parse(NormalizeNumber(left.correct_answer)).CompareTo(
                    int.Parse(NormalizeNumber(right.correct_answer))));
            return selected;
        }

    private void SetupDynamicQuestion()
    {
        if (serverQuestions.Count == 0)
        {
            HandleServerFailure("Daftar soal server kosong.");
            return;
        }

        currentServerQuestion = serverQuestions[SelectQuestionIndex()];
        lastPresentedServerQuestion = currentServerQuestion;
        currentTargetNumber = NormalizeNumber(currentServerQuestion.correct_answer);

        if (string.IsNullOrEmpty(currentTargetNumber))
        {
            HandleServerFailure("correct_answer cari_angka kosong.");
            return;
        }

        previousQuestionId = currentServerQuestion.id;
        ScoreManager.Instance?.SetCurrentQuestionPoint(currentServerQuestion.score_point);
        miniGameController?.SetCurrentCorrectAnswer("angka:" + currentTargetNumber);

        ShowCardBack();
        questionActive = false;

        GameQuestionData requestedQuestion = currentServerQuestion;
        int version = requestVersion;

        Texture2D localAnswer = answerImages == null ? null : System.Array.Find(
            answerImages, texture => texture != null && texture.name == currentTargetNumber);
        if (localAnswer != null && ServerQuestionImageUtility.ApplyTexture(
            frontObject, localAnswer, "[CariAngka Jawaban]"))
        {
            questionActive = true;
            ServerQuestionAudioUtility.Play(requestedQuestion, "[CariAngka]");
            return;
        }
        
        if (!string.IsNullOrWhiteSpace(requestedQuestion.media_url))
        {
            DonimalAPI.Instance.GetTexture(
                requestedQuestion.media_url,
                onSuccess: texture =>
                {
                    if (version != requestVersion) return;
                    if (currentServerQuestion != requestedQuestion) return;

                    ServerQuestionImageUtility.ApplyTexture(
                        frontObject,
                        texture,
                        "[CariAngka]"
                    );
                    questionActive = true;
                },
                onError: error =>
                {
                    if (version != requestVersion) return;
                    Debug.LogWarning("[CariAngka] Gagal unduh gambar soal: " + error + ". Lanjut tanpa gambar.");
                    questionActive = true;
                }
            );
        }
        else
        {
            Debug.Log("[CariAngka] media_url kosong. Lanjut tanpa gambar.");
            questionActive = true;
        }

        // Audio berasal dari katalog lokal, sehingga tidak memerlukan audio_url server.
        ServerQuestionAudioUtility.Play(requestedQuestion, "[CariAngka]");

        Debug.Log("[CariAngka] Soal aktif. Jawaban: " + currentTargetNumber);
    }

    private int SelectQuestionIndex()
    {
        if (!preventImmediateRepeat || serverQuestions.Count == 1 ||
            string.IsNullOrEmpty(previousQuestionId))
        {
            return Random.Range(0, serverQuestions.Count);
        }

        int selectedIndex = Random.Range(0, serverQuestions.Count);
        int safety = 0;
        while (serverQuestions[selectedIndex].id == previousQuestionId && safety < 30)
        {
            selectedIndex = Random.Range(0, serverQuestions.Count);
            safety++;
        }
        return selectedIndex;
    }

    public void CheckNumber(string inputData)
    {
        if (!questionActive || answerCompleted || isFlipping) return;

        string inputNumber = NormalizeNumber(inputData);
        if (string.IsNullOrEmpty(inputNumber)) return;

        if (inputNumber == currentTargetNumber)
        {
            answerCompleted = true;
            questionActive = false;
            StopRunningCoroutine();
            flipCoroutine = StartCoroutine(FlipCardRoutine());
        }
        else
        {
            miniGameAnswerController?.ShowCariAngkaWrongFeedback();
        }
    }

    private IEnumerator FlipCardRoutine()
    {
        if (!HasValidTemplate()) yield break;

        isFlipping = true;
        Vector3 originalScale = cardRect.localScale;
        originalScale.x = Mathf.Abs(originalScale.x);
        if (Mathf.Approximately(originalScale.x, 0f)) originalScale.x = 1f;

        yield return ScaleCardX(originalScale, originalScale.x, 0f, flipCloseDuration);
        backObject.SetActive(false);
        frontObject.SetActive(true);
        yield return ScaleCardX(originalScale, 0f, originalScale.x, flipOpenDuration);

        cardRect.localScale = originalScale;
        isFlipping = false;

        if (numberDisplayDuration > 0f)
            yield return new WaitForSeconds(numberDisplayDuration);

        miniGameAnswerController?.CompleteCariAngka();
        flipCoroutine = null;
    }

    private IEnumerator ScaleCardX(Vector3 baseScale, float from, float to, float duration)
    {
        if (duration <= 0f)
        {
            baseScale.x = to;
            cardRect.localScale = baseScale;
            yield break;
        }

        float elapsed = 0f;
        while (elapsed < duration)
        {
            elapsed += Time.deltaTime;
            Vector3 scale = baseScale;
            scale.x = Mathf.Lerp(from, to, Mathf.Clamp01(elapsed / duration));
            cardRect.localScale = scale;
            yield return null;
        }

        baseScale.x = to;
        cardRect.localScale = baseScale;
    }

    private void ShowCardBack()
    {
        questionObject.SetActive(true);
        cardRect.gameObject.SetActive(true);
        backObject.SetActive(true);
        frontObject.SetActive(false);

        Vector3 scale = cardRect.localScale;
        scale.x = Mathf.Abs(scale.x);
        if (Mathf.Approximately(scale.x, 0f)) scale.x = 1f;
        cardRect.localScale = scale;
    }

    private void HideQuestion()
    {
        if (questionObject != null) questionObject.SetActive(false);
    }

    private void HandleServerFailure(string error)
    {
        // Jika offline mode aktif, coba buat soal lokal dari answerImages
        if (DonimalAPI.Instance != null && DonimalAPI.Instance.offlineMode)
        {
            SetupLocalOfflineQuestion();
            return;
        }

        questionActive = false;
        HideQuestion();
        miniGameController?.SetCurrentCorrectAnswer("");

        Debug.LogError(
            "[CariAngka] Soal server gagal dimuat: " + error +
            " Mode server-only aktif; endpoint harus mengirim correct_answer dan media_url."
        );
    }

    private void SetupLocalOfflineQuestion()
    {
        // Build fake question list from answerImages (nama file = digit 0-9)
        if (serverQuestions.Count == 0 && answerImages != null)
        {
            foreach (Texture2D tex in answerImages)
            {
                if (tex == null) continue;
                string digit = tex.name.Trim();
                if (digit.Length != 1 || !char.IsDigit(digit[0])) continue;
                GameQuestionData fakeQ = new GameQuestionData
                {
                    id = "local_" + digit,
                    correct_answer = digit,
                    media_url = ""
                };
                serverQuestions.Add(fakeQ);
            }
        }

        if (serverQuestions.Count == 0)
        {
            // Fallback hardcoded 0-9
            for (int i = 0; i <= 9; i++)
            {
                serverQuestions.Add(new GameQuestionData
                {
                    id = "local_" + i,
                    correct_answer = i.ToString(),
                    media_url = ""
                });
            }
        }

        SetupDynamicQuestion();
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
        StopRunningCoroutine();
        questionActive = false;
        answerCompleted = false;
        isFlipping = false;
        currentTargetNumber = "";
        currentServerQuestion = null;
        miniGameController?.SetCurrentCorrectAnswer("");
        HideQuestion();
    }

    private void StopRunningCoroutine()
    {
        if (flipCoroutine == null) return;
        StopCoroutine(flipCoroutine);
        flipCoroutine = null;
    }

    private string NormalizeNumber(string value)
    {
        return RfidAnswerUtility.Normalize("angka", value);
    }

    public bool IsQuestionActive()
    {
        return questionActive;
    }
}
