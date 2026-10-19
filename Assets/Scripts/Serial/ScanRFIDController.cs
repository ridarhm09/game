using System.Collections;
using UnityEngine;

public class ScanRFIDController : MonoBehaviour
{
    [Header("UI Manager")]
    public UIManagerDonimal uiManager;

    [Header("Audio Manager")]
    public AudioManagerDonimal audioManager;

    [Header("Scan Feedback Objects")]
    public GameObject trueObject;
    public GameObject falseObject;

    [Header("True Character Objects")]
    public GameObject trueKiraObject;
    public GameObject trueEyraObject;
    public GameObject truePasaObject;

    [Header("Timer")]
    [Tooltip("Waktu menunggu sejak panel scan dibuka sebelum pemberitahuan belum terbaca.")]
    public float scanTimeout = 15f;
    public float trueFeedbackDuration = 5f;
    public float falseFeedbackDuration = 10f;

    [Header("Editor Simulation")]
    [Tooltip("Izinkan K, E, dan P untuk memilih karakter saat testing.")]
    public bool enableKeyboardSimulation = true;

    private string selectedCharacter = "";
    private float scanTimer = 0f;
    private bool isWaitingScan = false;
    private bool characterDetected = false;
    private Coroutine feedbackCoroutine;

    private void Update()
    {
        if (enableKeyboardSimulation &&
            KeyboardInputUtility.WasPressed(KeyCode.K))
        {
            SelectKiraForTesting();
        }

        if (enableKeyboardSimulation &&
            KeyboardInputUtility.WasPressed(KeyCode.E))
        {
            SelectEyraForTesting();
        }

        if (enableKeyboardSimulation &&
            KeyboardInputUtility.WasPressed(KeyCode.P))
        {
            SelectPasaForTesting();
        }

        if (isWaitingScan && !characterDetected)
        {
            scanTimer += Time.unscaledDeltaTime;

            if (scanTimer >= scanTimeout)
            {
                if (feedbackCoroutine == null)
                {
                    feedbackCoroutine = StartCoroutine(ShowFalseFeedbackThenBackToScan());
                }
            }
        }
    }

    [ContextMenu("Testing/Pilih Kira")]
    public void SelectKiraForTesting()
    {
        SelectCharacterForTesting("kelinci");
    }

    [ContextMenu("Testing/Pilih Eyra")]
    public void SelectEyraForTesting()
    {
        SelectCharacterForTesting("elang");
    }

    [ContextMenu("Testing/Pilih Pasa")]
    public void SelectPasaForTesting()
    {
        SelectCharacterForTesting("penyu");
    }

    private void SelectCharacterForTesting(string rfidValue)
    {
        // Shortcut editor tetap bekerja walaupun input Splash tidak mendapat fokus.
        if (!isWaitingScan)
        {
            StartScanCharacter();
        }

        CheckRFIDString(rfidValue);
    }

    public void StartScanCharacter()
    {
        if (DonimalAPI.Instance != null &&
            DonimalAPI.Instance.requireChildRfid &&
            !DonimalAPI.Instance.HasChildRfid())
        {
            Debug.LogWarning("[ScanRFID] Ditolak: RFID anak belum terbaca.");
            return;
        }

        selectedCharacter = "";
        characterDetected = false;
        isWaitingScan = true;
        scanTimer = 0f;

        if (feedbackCoroutine != null)
        {
            StopCoroutine(feedbackCoroutine);
            feedbackCoroutine = null;
        }

        HideAllScanFeedbackObjects();

        if (uiManager != null)
        {
            uiManager.ShowScanRFIDPanel();
        }
        else
        {
            Debug.LogWarning("UI Manager belum diisi di ScanRFIDController.");
        }

        if (audioManager != null)
        {
            audioManager.PlayRFIDScanBoneka();
        }

        Debug.Log("Mulai scan boneka RFID.");
    }

    public void CheckRFIDString(string rfidValue, string uid = "")
    {
        if (DonimalAPI.Instance != null &&
            DonimalAPI.Instance.requireChildRfid &&
            !DonimalAPI.Instance.HasChildRfid())
        {
            Debug.LogWarning("[ScanRFID] Kartu karakter diabaikan: RFID anak harus dibaca terlebih dahulu.");
            return;
        }

        bool isInitialScan = isWaitingScan;
        if (!isInitialScan && string.IsNullOrWhiteSpace(uid))
        {
            Debug.LogWarning("Pergantian karakter diabaikan karena UID RFID tidak tersedia.");
            return;
        }

        string input = rfidValue.ToLower().Trim();

        Debug.Log("RFID boneka terbaca: " + input);

        if (input == "kelinci")
        {
            ApplyCharacter("kira", trueKiraObject, isInitialScan);
        }
        else if (input == "elang")
        {
            ApplyCharacter("eyra", trueEyraObject, isInitialScan);
        }
        else if (input == "penyu")
        {
            ApplyCharacter("pasa", truePasaObject, isInitialScan);
        }
        else
        {
            Debug.LogWarning("Boneka tidak dikenali.");
        }
    }

    private void ApplyCharacter(string character, GameObject selectedTrueCharacter, bool isInitialScan)
    {
        selectedCharacter = character;
        characterDetected = true;
        isWaitingScan = false;
        ShowTrueFeedback(selectedTrueCharacter);
        Debug.Log(
            (isInitialScan ? "Karakter dipilih: " : "Karakter diganti: ") +
            selectedCharacter + " | Menampilkan feedback karakter."
        );
    }

    private void ShowTrueFeedback(GameObject selectedTrueCharacter)
    {
        if (feedbackCoroutine != null)
        {
            StopCoroutine(feedbackCoroutine);
            feedbackCoroutine = null;
        }

        feedbackCoroutine = StartCoroutine(ShowTrueFeedbackThenDashboard(selectedTrueCharacter));
    }

    private IEnumerator ShowTrueFeedbackThenDashboard(GameObject selectedTrueCharacter)
    {
        HideAllScanFeedbackObjects();

        if (trueObject != null)
        {
            trueObject.SetActive(true);
        }

        if (selectedTrueCharacter != null)
        {
            selectedTrueCharacter.SetActive(true);
        }

        if (uiManager != null)
        {
            uiManager.ShowFeedbackCharacterPanel();
        }

        if (audioManager != null)
        {
            audioManager.PlayCharacterIntroAudio();
        }

        Debug.Log("Scan boneka benar. Tampil ScanFeedback true + karakter.");

        yield return AudioManagerDonimal.WaitUntilFinished(audioManager, trueFeedbackDuration);

        HideAllScanFeedbackObjects();

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

        HideAllScanFeedbackObjects();

        if (falseObject != null)
        {
            falseObject.SetActive(true);
        }

        if (uiManager != null)
        {
            uiManager.ShowFeedbackCharacterPanel();
        }

        if (audioManager != null)
        {
            audioManager.PlayRFIDScanGagal();
        }

        Debug.LogWarning("Scan boneka gagal. Tampil ScanFeedback false.");

        yield return AudioManagerDonimal.WaitUntilFinished(audioManager, falseFeedbackDuration);

        HideAllScanFeedbackObjects();

        if (!characterDetected)
        {
            StartScanCharacter();
        }

        feedbackCoroutine = null;
    }

    private void HideAllScanFeedbackObjects()
    {
        if (trueObject != null) trueObject.SetActive(false);
        if (falseObject != null) falseObject.SetActive(false);

        if (trueKiraObject != null) trueKiraObject.SetActive(false);
        if (trueEyraObject != null) trueEyraObject.SetActive(false);
        if (truePasaObject != null) truePasaObject.SetActive(false);
    }

    public string GetSelectedCharacter()
    {
        return selectedCharacter;
    }
}
