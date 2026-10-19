using System.Collections.Generic;
using UnityEngine;

public class TebakQuestionController : MonoBehaviour
{
    [Header("Controller")]
    public CategoryToMiniGameImageController miniGameController;
    public MiniGameAnswerController miniGameAnswerController;

    [Header("Tampilan Soal Tunggal")]
    [Tooltip("Isi dengan satu GameObject yang memiliki Image atau RawImage.")]
    public GameObject questionObject;

    [Header("Question Settings")]
    public bool preventImmediateRepeat = true;

    private readonly List<GameQuestionData> serverQuestions =
        new List<GameQuestionData>();

    private int requestVersion;
    private GameQuestionData currentServerQuestion;
    private GameQuestionData lastPresentedServerQuestion;
    private string currentColor = "";
    private string previousQuestionId = "";
    private bool isFetchingQuestions;
    private bool questionActive;

    private void Start()
    {
        HideQuestion();
    }

    private void OnDisable()
    {
        StopQuestion();
    }

    public void StartQuestion()
    {
        requestVersion++;
        isFetchingQuestions = false;
        ScoreManager.Instance?.UseDefaultQuestionPoint();
        ServerQuestionAudioUtility.Stop();

        questionActive = false;
        currentColor = "";
        currentServerQuestion = null;
        HideQuestion();

        if (miniGameController != null)
        {
            miniGameController.SetCurrentCorrectAnswer("");
        }

        if (questionObject == null)
        {
            Debug.LogError(
                "[TebakWarna] Question Object belum diisi di Inspector."
            );
            return;
        }

        if (serverQuestions.Count > 0)
        {
            SetupDynamicQuestion();
            return;
        }

        if (!isFetchingQuestions)
        {
            FetchQuestionsFromServer();
        }
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
        Debug.Log("[TebakWarna] Fetching questions from server...");

        DonimalAPI.Instance.GetRandomQuestions(
            "tebak_warna",
            50,
            onSuccess: data =>
            {
                if (version != requestVersion) return;
                isFetchingQuestions = false;
                serverQuestions.Clear();

                if (data != null)
                {
#if UNITY_EDITOR
                    LocalQuestionAudioCatalog catalog = Resources.Load<LocalQuestionAudioCatalog>("LocalQuestionAudioCatalog");
                    if (catalog != null) catalog.ImportWarnaQuestionIds(data);
                    else Debug.LogWarning("[TebakWarna] Katalog lokal tidak ditemukan; ID belum dapat disimpan.");
#endif
                    foreach (GameQuestionData question in data)
                    {
                        if (IsValidServerQuestion(question))
                        {
                            serverQuestions.Add(question);
                        }
                    }
                }

                if (serverQuestions.Count == 0)
                {
                    HandleServerFailure(
                        "Server tidak mengirim soal tebak_warna yang memiliki " +
                        "correct_answer dan media_url."
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
        return question != null &&
               !string.IsNullOrWhiteSpace(question.correct_answer) &&
               !string.IsNullOrWhiteSpace(question.media_url);
    }

    private void SetupDynamicQuestion()
    {
        if (serverQuestions.Count == 0)
        {
            HandleServerFailure("Daftar soal server kosong.");
            return;
        }

        int selectedIndex = SelectQuestionIndex();
        currentServerQuestion = serverQuestions[selectedIndex];
        lastPresentedServerQuestion = currentServerQuestion;
        currentColor = NormalizeColor(currentServerQuestion.correct_answer);

        if (string.IsNullOrEmpty(currentColor))
        {
            HandleServerFailure("correct_answer soal tebak_warna kosong.");
            return;
        }

        previousQuestionId = currentServerQuestion.id;

        ScoreManager.Instance?.SetCurrentQuestionPoint(
            currentServerQuestion.score_point
        );

        if (miniGameController != null)
        {
            miniGameController.SetCurrentCorrectAnswer(
                "warna:" + currentColor
            );
        }

        questionActive = false;

        int version = requestVersion;
        DonimalAPI.Instance.GetTexture(
            currentServerQuestion.media_url,
            onSuccess: texture =>
            {
                if (version != requestVersion) return;
                if (currentServerQuestion == null)
                {
                    return;
                }

                if (!ServerQuestionImageUtility.ApplyTexture(
                    questionObject,
                    texture,
                    "[TebakWarna]"
                )) return;
                questionObject.SetActive(true);
                questionActive = true;
            },
            onError: error =>
            {
                if (version != requestVersion) return;
                Debug.LogError(
                    "[TebakWarna] Gagal unduh gambar soal: " + error
                );
            }
        );

        ServerQuestionAudioUtility.Play(currentServerQuestion, "[TebakWarna]");

        Debug.Log(
            "[TebakWarna] Soal aktif. Jawaban: " + currentColor
        );
    }

    private int SelectQuestionIndex()
    {
        if (!preventImmediateRepeat ||
            serverQuestions.Count == 1 ||
            string.IsNullOrEmpty(previousQuestionId))
        {
            return Random.Range(0, serverQuestions.Count);
        }

        int selectedIndex = Random.Range(0, serverQuestions.Count);
        int safety = 0;

        while (serverQuestions[selectedIndex].id == previousQuestionId &&
               safety < 30)
        {
            selectedIndex = Random.Range(0, serverQuestions.Count);
            safety++;
        }

        return selectedIndex;
    }

    public void CheckColor(string inputData)
    {
        if (!questionActive)
        {
            return;
        }

        string inputColor = NormalizeColor(inputData);
        if (string.IsNullOrEmpty(inputColor))
        {
            return;
        }

        if (inputColor == currentColor)
        {
            questionActive = false;
            miniGameAnswerController?.CompleteTebakWarna();
            return;
        }

        miniGameAnswerController?.ShowTebakWarnaWrongFeedback();
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
        questionActive = false;
        currentColor = "";
        currentServerQuestion = null;

        if (miniGameController != null)
        {
            miniGameController.SetCurrentCorrectAnswer("");
        }

        HideQuestion();
    }

    private void HideQuestion()
    {
        if (questionObject != null)
        {
            questionObject.SetActive(false);
        }
    }

    private void HandleServerFailure(string error)
    {
        // Jika offline mode aktif, coba buat soal lokal dari daftar warna
        if (DonimalAPI.Instance != null && DonimalAPI.Instance.offlineMode)
        {
            SetupLocalOfflineQuestion();
            return;
        }

        questionActive = false;
        HideQuestion();

        if (miniGameController != null)
        {
            miniGameController.SetCurrentCorrectAnswer("");
        }

        Debug.LogError(
            "[TebakWarna] Soal server gagal dimuat: " + error +
            " Mode server-only aktif; perbaiki endpoint/API agar " +
            "mengirim correct_answer dan media_url."
        );
    }

    private static readonly string[] LocalColors =
        { "merah", "kuning", "hijau", "biru", "ungu", "oranye", "putih", "hitam" };

    private void SetupLocalOfflineQuestion()
    {
        if (serverQuestions.Count == 0)
        {
            foreach (string color in LocalColors)
            {
                serverQuestions.Add(new GameQuestionData
                {
                    id = "local_" + color,
                    correct_answer = color,
                    media_url = ""
                });
            }
        }

        if (serverQuestions.Count == 0)
        {
            Debug.LogError("[TebakWarna] Tidak ada data warna lokal.");
            questionActive = false;
            HideQuestion();
            return;
        }

        // Pick random color
        int index = Random.Range(0, serverQuestions.Count);
        currentServerQuestion = serverQuestions[index];
        lastPresentedServerQuestion = currentServerQuestion;
        currentColor = NormalizeColor(currentServerQuestion.correct_answer);
        previousQuestionId = currentServerQuestion.id;

        miniGameController?.SetCurrentCorrectAnswer("warna:" + currentColor);

        // Tampilkan questionObject tanpa gambar (warna lewat teks/audio)
        if (questionObject != null) questionObject.SetActive(true);
        questionActive = true;

        Debug.Log("[TebakWarna] Soal lokal aktif. Jawaban: " + currentColor);
    }

    private string NormalizeColor(string value)
    {
        return RfidAnswerUtility.Normalize("warna", value);
    }

    public bool IsQuestionActive()
    {
        return questionActive;
    }

    public string GetCurrentColor()
    {
        return currentColor;
    }
}
