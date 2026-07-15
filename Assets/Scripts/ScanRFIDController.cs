using System.Collections;
using UnityEngine;

public class ScanRFIDController : MonoBehaviour
{
    [Header("UI Manager")]
    public UIManagerDonimal uiManager;

    [Header("Feedback Character Objects")]
    public GameObject trueObject;
    public GameObject falseObject;
    public GameObject kiraObject;
    public GameObject eyraObject;
    public GameObject pasaObject;

    [Header("Timer Scan")]
    public float scanTimeout = 30f;
    public float falseFeedbackDuration = 10f;
    public float trueFeedbackDuration = 5f;

    private string selectedCharacter = "";
    private float scanTimer = 0f;
    private bool isWaitingScan = false;
    private bool characterDetected = false;
    private Coroutine feedbackCoroutine;

    private void Update()
    {
        // Simulasi RFID sementara pakai keyboard
        if (Input.GetKeyDown(KeyCode.K))
        {
            CheckRFIDString("kucing");
        }

        if (Input.GetKeyDown(KeyCode.E))
        {
            CheckRFIDString("elang");
        }

        if (Input.GetKeyDown(KeyCode.P))
        {
            CheckRFIDString("penyu");
        }

        // Timer berjalan saat ScanRFIDPanel sedang menunggu karakter
        if (isWaitingScan && !characterDetected)
        {
            scanTimer += Time.deltaTime;

            if (scanTimer >= scanTimeout)
            {
                if (feedbackCoroutine == null)
                {
                    feedbackCoroutine = StartCoroutine(ShowFalseFeedbackThenBackToScan());
                }
            }
        }
    }

    public void StartScanCharacter()
    {
        selectedCharacter = "";
        characterDetected = false;
        isWaitingScan = true;
        scanTimer = 0f;

        if (feedbackCoroutine != null)
        {
            StopCoroutine(feedbackCoroutine);
            feedbackCoroutine = null;
        }

        HideAllFeedbackObjects();

        if (uiManager != null)
        {
            uiManager.ShowScanRFIDPanel();
        }

        Debug.Log("Mulai scan karakter. Menunggu kucing, elang, atau penyu.");
    }

    public void CheckRFIDString(string rfidValue)
    {
        string input = rfidValue.ToLower().Trim();

        Debug.Log("RFID terbaca: " + input);

        if (input == "kucing")
        {
            selectedCharacter = "Kira";
            characterDetected = true;
            isWaitingScan = false;

            ShowTrueFeedback(kiraObject);
        }
        else if (input == "elang")
        {
            selectedCharacter = "Eyra";
            characterDetected = true;
            isWaitingScan = false;

            ShowTrueFeedback(eyraObject);
        }
        else if (input == "penyu")
        {
            selectedCharacter = "Pasa";
            characterDetected = true;
            isWaitingScan = false;

            ShowTrueFeedback(pasaObject);
        }
        else
        {
            selectedCharacter = "";
            Debug.LogWarning("Boneka tidak dikenali.");
        }
    }

    private void ShowTrueFeedback(GameObject characterObject)
    {
        if (feedbackCoroutine != null)
        {
            StopCoroutine(feedbackCoroutine);
            feedbackCoroutine = null;
        }

        feedbackCoroutine = StartCoroutine(ShowTrueFeedbackThenDashboard(characterObject));
    }

    private IEnumerator ShowTrueFeedbackThenDashboard(GameObject characterObject)
    {
        HideAllFeedbackObjects();

        if (trueObject != null)
        {
            trueObject.SetActive(true);
        }

        if (characterObject != null)
        {
            characterObject.SetActive(true);
        }

        if (uiManager != null)
        {
            uiManager.ShowFeedbackCharacterPanel();
        }

        Debug.Log("Karakter terdeteksi. Feedback true tampil selama 5 detik.");

        yield return new WaitForSeconds(trueFeedbackDuration);

        HideAllFeedbackObjects();

        if (uiManager != null)
        {
            uiManager.ShowDashboardPanel();
        }

        feedbackCoroutine = null;
    }

    private IEnumerator ShowFalseFeedbackThenBackToScan()
    {
        isWaitingScan = false;
        scanTimer = 0f;

        HideAllFeedbackObjects();

        if (falseObject != null)
        {
            falseObject.SetActive(true);
        }

        if (uiManager != null)
        {
            uiManager.ShowFeedbackCharacterPanel();
        }

        Debug.LogWarning("Scan timeout. Feedback false tampil selama 10 detik.");

        yield return new WaitForSeconds(falseFeedbackDuration);

        HideAllFeedbackObjects();

        if (!characterDetected)
        {
            StartScanCharacter();
        }

        feedbackCoroutine = null;
    }

    private void HideAllFeedbackObjects()
    {
        if (trueObject != null) trueObject.SetActive(false);
        if (falseObject != null) falseObject.SetActive(false);
        if (kiraObject != null) kiraObject.SetActive(false);
        if (eyraObject != null) eyraObject.SetActive(false);
        if (pasaObject != null) pasaObject.SetActive(false);
    }

    public string GetSelectedCharacter()
    {
        return selectedCharacter;
    }
}