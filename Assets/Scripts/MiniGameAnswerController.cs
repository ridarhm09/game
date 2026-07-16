using System.Collections;
using UnityEngine;

public class MiniGameAnswerController : MonoBehaviour
{
    [Header("UI Manager")]
    public UIManagerDonimal uiManager;

    [Header("Category To Mini Game Controller")]
    public CategoryToMiniGameImageController miniGameController;

    [Header("Feedback Objects")]
    public GameObject trueObject;
    public GameObject falseObject;

    [Header("Feedback Duration")]
    public float trueFeedbackDuration = 3f;
    public float falseFeedbackDuration = 3f;

    private bool isCheckingAnswer = false;

    private void Update()
    {
        /*
         * Simulasi RFID jawaban sementara:
         * Nanti bagian ini bisa diganti dengan data dari docking RFID.
         */

        if (Input.GetKeyDown(KeyCode.A))
        {
            CheckAnswer("a");
        }

        if (Input.GetKeyDown(KeyCode.U))
        {
            CheckAnswer("u");
        }

        if (Input.GetKeyDown(KeyCode.B))
        {
            CheckAnswer("b");
        }

        if (Input.GetKeyDown(KeyCode.C))
        {
            CheckAnswer("c");
        }
    }

    public void CheckAnswer(string answerValue)
    {
        if (isCheckingAnswer)
        {
            return;
        }

        string answer = answerValue.ToLower().Trim();
        string selectedSubGame = miniGameController.GetSelectedSubGame();

        Debug.Log("Jawaban terbaca: " + answer);
        Debug.Log("Subgame aktif: " + selectedSubGame);

        bool isCorrect = false;

        if (selectedSubGame == "tebak_bunyi")
        {
            // SoalHuruf1
            // Jawaban benar: A
            isCorrect = answer == "a";
        }
        else if (selectedSubGame == "lengkapi_huruf")
        {
            // SoalHuruf2
            // K _ C I N G
            // Jawaban benar: U
            isCorrect = answer == "u";
        }
        else if (selectedSubGame == "berburu_huruf")
        {
            // SoalHuruf3
            // Cari kartu huruf A
            // Jawaban benar: A
            isCorrect = answer == "a";
        }
        else
        {
            Debug.LogWarning("Subgame belum dikenali atau belum dipilih.");
            return;
        }

        if (isCorrect)
        {
            StartCoroutine(ShowTrueFeedback());
        }
        else
        {
            StartCoroutine(ShowFalseFeedback());
        }
    }

    private IEnumerator ShowTrueFeedback()
    {
        isCheckingAnswer = true;

        HideFeedbackObjects();

        if (trueObject != null)
        {
            trueObject.SetActive(true);
        }

        if (uiManager != null)
        {
            uiManager.ShowFeedbackCharacterPanel();
        }

        Debug.Log("Jawaban benar.");

        yield return new WaitForSeconds(trueFeedbackDuration);

        HideFeedbackObjects();

        // Sementara balik ke dashboard setelah benar
        if (uiManager != null)
        {
            uiManager.ShowDashboardPanel();
        }

        isCheckingAnswer = false;
    }

    private IEnumerator ShowFalseFeedback()
    {
        isCheckingAnswer = true;

        HideFeedbackObjects();

        if (falseObject != null)
        {
            falseObject.SetActive(true);
        }

        if (uiManager != null)
        {
            uiManager.ShowFeedbackCharacterPanel();
        }

        Debug.Log("Jawaban salah.");

        yield return new WaitForSeconds(falseFeedbackDuration);

        HideFeedbackObjects();

        // Kalau salah, balik lagi ke soal yang sama
        if (uiManager != null)
        {
            uiManager.ShowMiniGamePanel();
        }

        isCheckingAnswer = false;
    }

    private void HideFeedbackObjects()
    {
        if (trueObject != null) trueObject.SetActive(false);
        if (falseObject != null) falseObject.SetActive(false);
    }
}