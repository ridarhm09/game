using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

public class LengkapiQuestionController : MonoBehaviour
{
    [Header("Controller")]
    public CategoryToMiniGameImageController miniGameController;
    public MiniGameAnswerController miniGameAnswerController;

    [Header("Satu Template untuk Semua Soal")]
    public GameObject questionObject;
    public GameObject incompleteObject;
    public GameObject completeObject;

    [Header("Pengaturan")]
    public float completeWordDisplayDuration = 1.2f;

    private readonly List<GameQuestionData> serverQuestions = new List<GameQuestionData>();
    private GameQuestionData currentServerQuestion;
    private string currentMissingLetter = "";
    private bool isFetchingQuestions;
    private bool questionActive;
    private bool answerCompleted;
    private bool incompleteTextureReady;
    private bool completeTextureReady;
    private Coroutine completeCoroutine;
    private int requestVersion;

    private void Awake()
    {
        ResolveQuestionTemplate();
    }

    private void Start()
    {
        ResolveQuestionTemplate();
        HideQuestion();
    }

    private void ResolveQuestionTemplate()
    {
        if (questionObject == null)
        {
            Transform found = FindChildRecursive(transform, "soal");
            if (found == null)
            {
                foreach (Transform child in transform)
                {
                    if (child.name.StartsWith("soal", System.StringComparison.OrdinalIgnoreCase) &&
                        FindChildRecursive(child, "soalBL") != null)
                    {
                        found = child;
                        break;
                    }
                }
            }
            if (found != null) questionObject = found.gameObject;
        }

        if (questionObject == null) return;

        if (incompleteObject == null)
        {
            Transform found = FindChildRecursive(questionObject.transform, "soalBL");
            if (found == null) found = FindChildRecursive(questionObject.transform, "front");
            if (found == null) found = FindDirectChild(questionObject.transform, "soal");
            if (found != null) incompleteObject = found.gameObject;
        }

        if (completeObject == null)
        {
            Transform found = FindChildRecursive(questionObject.transform, "soalL");
            if (found == null) found = FindChildRecursive(questionObject.transform, "back");
            if (found == null) found = FindDirectChild(questionObject.transform, "answer");
            if (found != null) completeObject = found.gameObject;
        }

        EnsureGraphic(incompleteObject, "soalBL");
        EnsureGraphic(completeObject, "soalL");
    }

    private Transform FindDirectChild(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Equals(childName, System.StringComparison.OrdinalIgnoreCase)) return child;
        }
        return null;
    }

    private Transform FindChildRecursive(Transform parent, string childName)
    {
        foreach (Transform child in parent)
        {
            if (child.name.Equals(childName, System.StringComparison.OrdinalIgnoreCase)) return child;
            Transform nested = FindChildRecursive(child, childName);
            if (nested != null) return nested;
        }
        return null;
    }

    private void EnsureGraphic(GameObject target, string label)
    {
        if (target == null || target.GetComponent<RawImage>() != null || target.GetComponent<Image>() != null) return;
        target.AddComponent<RawImage>();
        Debug.LogWarning("[LengkapiHuruf] RawImage ditambahkan otomatis pada " + label + ".");
    }

    public void StartQuestion()
    {
        ResolveQuestionTemplate();
        StopRunningCoroutine();
        ServerQuestionAudioUtility.Stop();
        requestVersion++;
        questionActive = false;
        answerCompleted = false;
        currentMissingLetter = "";
        HideQuestion();

        if (questionObject == null || incompleteObject == null)
        {
            Debug.LogError("[LengkapiHuruf] Isi Question Object dan Incomplete Object, atau gunakan hierarchy soal/soalBL.");
            return;
        }

        if (serverQuestions.Count > 0) SetupServerQuestion();
        else FetchQuestionsFromServer();
    }

    private void FetchQuestionsFromServer()
    {
        if (isFetchingQuestions) return;
        isFetchingQuestions = true;
        int version = requestVersion;

        if (DonimalAPI.Instance == null)
        {
            HandleServerFailure("DonimalAPI tidak ditemukan.");
            return;
        }

        DonimalAPI.Instance.GetRandomQuestions("lengkapi_huruf", 50,
            data =>
            {
                isFetchingQuestions = false;
                if (version != requestVersion) return;
                serverQuestions.Clear();
                if (data != null)
                {
                    foreach (GameQuestionData question in data)
                    {
                        if (IsValidServerQuestion(question)) serverQuestions.Add(question);
                    }
                }

                if (serverQuestions.Count == 0)
                    HandleServerFailure("Respons tidak memiliki jawaban dan URL gambar soal yang valid. " +
                                        "Gunakan correct_answer (atau answer), serta incomplete/media_url.");
                else
                    SetupServerQuestion();
            },
            error =>
            {
                isFetchingQuestions = false;
                if (version == requestVersion) HandleServerFailure(error);
            });
    }

    private void HandleServerFailure(string error)
    {
        isFetchingQuestions = false;
        questionActive = false;
        HideQuestion();
        Debug.LogError("[LengkapiHuruf] Soal server gagal dimuat: " + error +
                       " Mode server-only aktif.");
    }

    private bool IsValidServerQuestion(GameQuestionData question)
    {
        return question != null &&
               !string.IsNullOrWhiteSpace(GetCorrectAnswer(question)) &&
               !string.IsNullOrWhiteSpace(GetIncompleteUrl(question));
    }

    private string GetCorrectAnswer(GameQuestionData question)
    {
        if (question == null) return "";
        return !string.IsNullOrWhiteSpace(question.correct_answer)
            ? question.correct_answer
            : question.answer;
    }

    private string GetIncompleteUrl(GameQuestionData question)
    {
        if (question == null) return "";
        if (!string.IsNullOrWhiteSpace(question.incomplete_media_url)) return question.incomplete_media_url;
        if (!string.IsNullOrWhiteSpace(question.incomplete)) return question.incomplete;
        return question.media_url;
    }

    private string GetCompleteUrl(GameQuestionData question)
    {
        if (question == null) return "";
        if (!string.IsNullOrWhiteSpace(question.complete_media_url)) return question.complete_media_url;
        if (!string.IsNullOrWhiteSpace(question.complete)) return question.complete;

        string answerLetter = NormalizeLetter(GetCorrectAnswer(question)).ToUpperInvariant();
        if (string.IsNullOrEmpty(answerLetter)) return "";

        // Fallback untuk struktur file server DONIMAL:
        // /static/uploads/questions/huruf/complete/lengkapi L jwb.png
        return "/static/uploads/questions/huruf/complete/lengkapi%20" +
               answerLetter + "%20jwb.png";
    }

    private void SetupServerQuestion()
    {
        if (serverQuestions.Count == 0) return;
        currentServerQuestion = serverQuestions[Random.Range(0, serverQuestions.Count)];
        currentMissingLetter = NormalizeLetter(GetCorrectAnswer(currentServerQuestion));
        if (string.IsNullOrEmpty(currentMissingLetter))
        {
            HandleServerFailure("correct_answer kosong atau tidak valid.");
            return;
        }

        int version = requestVersion;
        ShowIncompleteVersion();
        ScoreManager.Instance?.SetCurrentQuestionPoint(currentServerQuestion.score_point);
        miniGameController?.SetCurrentCorrectAnswer("huruf:" + currentMissingLetter);
        ServerQuestionAudioUtility.Play(currentServerQuestion, "[LengkapiHuruf]");

        string incompleteUrl = GetIncompleteUrl(currentServerQuestion);
        string completeUrl = GetCompleteUrl(currentServerQuestion);
        incompleteTextureReady = false;
        completeTextureReady = completeObject == null;

        DonimalAPI.Instance.GetTexture(incompleteUrl,
            texture =>
            {
                if (version != requestVersion) return;
                ServerQuestionImageUtility.ApplyTexture(incompleteObject, texture, "[LengkapiHuruf]");
                incompleteTextureReady = true;
                TryActivateQuestion(version);
            },
            error =>
            {
                if (version == requestVersion) HandleServerFailure("Gagal mengunduh gambar: " + error);
            });

        if (completeObject != null && !string.IsNullOrWhiteSpace(completeUrl))
        {
            DonimalAPI.Instance.GetTexture(completeUrl,
                texture =>
                {
                    if (version == requestVersion)
                    {
                        ServerQuestionImageUtility.ApplyTexture(completeObject, texture, "[LengkapiHuruf Answer]");
                        completeTextureReady = true;
                        TryActivateQuestion(version);
                    }
                },
                error =>
                {
                    if (version == requestVersion)
                        HandleServerFailure("Gagal mengunduh gambar complete: " + error +
                                            " | URL: " + completeUrl);
                });
        }
        else if (completeObject != null)
        {
            Debug.LogWarning("[LengkapiHuruf] Server tidak mengirim complete atau complete_media_url.");
        }
    }

    private void TryActivateQuestion(int version)
    {
        if (version != requestVersion || !incompleteTextureReady || !completeTextureReady) return;
        questionActive = true;
        Debug.Log("[LengkapiHuruf] Soal dan gambar complete siap. Jawaban: " + currentMissingLetter);
    }

    public void CheckLetter(string inputData)
    {
        if (!questionActive || answerCompleted) return;
        string inputLetter = NormalizeLetter(inputData);
        if (string.IsNullOrEmpty(inputLetter)) return;
        if (inputLetter == currentMissingLetter) HandleCorrectAnswer();
        else miniGameAnswerController?.ShowLengkapiHurufWrongFeedback();
    }

    private void HandleCorrectAnswer()
    {
        answerCompleted = true;
        questionActive = false;
        if (incompleteObject != null) incompleteObject.SetActive(false);
        if (completeObject != null) completeObject.SetActive(true);
        completeCoroutine = StartCoroutine(CompleteAnswerRoutine());
    }

    private IEnumerator CompleteAnswerRoutine()
    {
        if (completeWordDisplayDuration > 0f) yield return new WaitForSeconds(completeWordDisplayDuration);
        miniGameAnswerController?.CompleteLengkapiHuruf();
        completeCoroutine = null;
    }

    private void ShowIncompleteVersion()
    {
        questionObject.SetActive(true);
        incompleteObject.SetActive(true);
        if (completeObject != null) completeObject.SetActive(false);
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
        currentServerQuestion = null;
        currentMissingLetter = "";
        miniGameController?.SetCurrentCorrectAnswer("");
        HideQuestion();
    }

    private void HideQuestion()
    {
        if (questionObject != null) questionObject.SetActive(false);
    }

    private void StopRunningCoroutine()
    {
        if (completeCoroutine == null) return;
        StopCoroutine(completeCoroutine);
        completeCoroutine = null;
    }

    private string NormalizeLetter(string value)
    {
        return RfidAnswerUtility.Normalize("huruf", value);
    }

    public bool IsQuestionActive()
    {
        return questionActive;
    }
}
