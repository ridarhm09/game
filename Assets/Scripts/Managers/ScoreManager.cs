using UnityEngine;
using TMPro;

public class ScoreManager : MonoBehaviour
{
    public static ScoreManager Instance { get; private set; }

    [Header("Point Settings")]
    [Tooltip("Poin default untuk fallback lokal jika soal server tidak tersedia.")]
    public int pointPerCorrectAnswer = 20;

    [SerializeField]
    private int currentQuestionPoint = 20;

    [Header("Current Score")]
    public int currentPoint = 0;

    [Header("Answer Count")]
    public int correctCount = 0;
    public int wrongCount = 0;

    [Header("Poin Keseluruhan di Semua Panel")]
    public TMP_Text dashboardPointText;
    public TMP_Text belajarPointText;
    public TMP_Text miniGamePointText;

    [Header("Reward Panel UI")]
    public TMP_Text rewardPointText;
    public TMP_Text benarValue;
    public TMP_Text salahValue;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Debug.LogWarning(
                "[ScoreManager] Ada lebih dari satu ScoreManager. " +
                "Instance lama tetap digunakan."
            );
            return;
        }

        Instance = this;
        currentQuestionPoint = Mathf.Max(0, pointPerCorrectAnswer);
    }

    private void Start()
    {
        UseDefaultQuestionPoint();
        UpdateAllUI();
    }

    /// <summary>
    /// Dipanggil controller ketika soal dari server dipilih.
    /// Nilai berasal dari game_questions.score_point.
    /// </summary>
    public void SetCurrentQuestionPoint(int point)
    {
        currentQuestionPoint = Mathf.Max(0, point);

        Debug.Log(
            "[ScoreManager] score_point soal aktif = " +
            currentQuestionPoint
        );
    }

    /// <summary>
    /// Dipakai untuk fallback lokal.
    /// </summary>
    public void UseDefaultQuestionPoint()
    {
        currentQuestionPoint =
            Mathf.Max(0, pointPerCorrectAnswer);

        Debug.Log(
            "[ScoreManager] Menggunakan poin default = " +
            currentQuestionPoint
        );
    }

    public int GetCurrentQuestionPoint()
    {
        return currentQuestionPoint;
    }

    public void AddCorrectAnswer()
    {
        int earnedPoint =
            Mathf.Max(0, currentQuestionPoint);

        currentPoint += earnedPoint;
        correctCount++;

        Debug.Log(
            "Jawaban benar. Poin bertambah +" +
            earnedPoint
        );

        Debug.Log(
            "Total poin keseluruhan: " +
            currentPoint
        );

        DockingSerialReader.Instance
            ?.SendLogToDocking(true);

        UpdateAllUI();
    }

    public void AddWrongAnswer()
    {
        wrongCount++;

        Debug.Log("Jawaban salah.");

        Debug.Log(
            "Total poin tetap: " +
            currentPoint
        );

        DockingSerialReader.Instance
            ?.SendLogToDocking(false);

        UpdateAllUI();
    }

    public void ResetScore()
    {
        currentPoint = 0;
        correctCount = 0;
        wrongCount = 0;

        UseDefaultQuestionPoint();

        Debug.Log("Score direset.");

        UpdateAllUI();
    }

    public void UpdateAllUI()
    {
        if (dashboardPointText != null)
        {
            dashboardPointText.text =
                currentPoint.ToString();
        }

        if (belajarPointText != null)
        {
            belajarPointText.text =
                currentPoint.ToString();
        }

        if (miniGamePointText != null)
        {
            miniGamePointText.text =
                currentPoint.ToString();
        }

        if (rewardPointText != null)
        {
            rewardPointText.text =
                currentPoint.ToString();
        }

        if (benarValue != null)
        {
            benarValue.text =
                correctCount.ToString();
        }

        if (salahValue != null)
        {
            salahValue.text =
                wrongCount.ToString();
        }
    }

    public int GetPoint()
    {
        return currentPoint;
    }

    public int GetCorrectCount()
    {
        return correctCount;
    }

    public int GetWrongCount()
    {
        return wrongCount;
    }
}
