using System.Collections;
using UnityEngine;

public class UIManagerDonimal : MonoBehaviour
{
    [Header("Panels")]
    public GameObject splashPanel;
    public GameObject scanRFIDPanel;
    public GameObject dashboardPanel;
    public GameObject belajarPanel;
    public GameObject categoryPanel;
    public GameObject miniGamePanel;
    public GameObject feedbackCharacterPanel;
    public GameObject rewardPanel;

    [Header("Controllers")]
    public ScanRFIDController scanRFIDController;

    [Header("Visual Controller")]
    public CharacterVisualController characterVisualController;

    [Header("Audio Manager")]
    public AudioManagerDonimal audioManager;
    private bool isStartingFromSplash;

    private void Start()
    {
        Debug.Log("UIManagerDonimal aktif.");
        ShowSplashPanel();

        if (audioManager != null)
        {
            audioManager.PlaySplashWelcome();
        }
        else
        {
            Debug.LogWarning("AudioManager belum diisi di UIManagerDonimal.");
        }
    }

    private void Update()
    {
        // Testing manual: B untuk masuk BelajarPanel dari DashboardPanel
        // Kalau sudah full RFID, bagian ini boleh dibiarkan atau dihapus.
        if (dashboardPanel != null && dashboardPanel.activeSelf)
        {
            if (KeyboardInputUtility.WasPressed(KeyCode.B))
            {
                ShowBelajarPanel();
                Debug.Log("Masuk ke BelajarPanel lewat tombol B.");
            }
        }
    }

    public void StartFromSplash()
    {
        if (isStartingFromSplash) return;

        isStartingFromSplash = true;
        StartCoroutine(StartFromSplashAfterAudio());
    }

    private IEnumerator StartFromSplashAfterAudio()
    {
        yield return AudioManagerDonimal.WaitUntilFinished(audioManager, 0f);
        Debug.Log("Mulai dari SplashScreen.");

        isStartingFromSplash = false;

        if (scanRFIDController != null)
        {
            scanRFIDController.StartScanCharacter();
        }
        else
        {
            Debug.LogWarning("ScanRFIDController belum diisi, pindah manual ke ScanRFIDPanel.");
            ShowScanRFIDPanel();
            if (audioManager != null)
            {
                audioManager.PlayRFIDScanBoneka();
            }
        }
    }

    private void OnDisable()
    {
        StopAllCoroutines();
        isStartingFromSplash = false;
    }

    private void HideAllPanels()
    {
        FindFirstObjectByType<BelajarPanelController>(FindObjectsInactive.Include)?.ResetLearningDisplay();
        if (splashPanel != null) splashPanel.SetActive(false);
        if (scanRFIDPanel != null) scanRFIDPanel.SetActive(false);
        if (dashboardPanel != null) dashboardPanel.SetActive(false);
        if (belajarPanel != null) belajarPanel.SetActive(false);
        if (categoryPanel != null) categoryPanel.SetActive(false);
        if (miniGamePanel != null) miniGamePanel.SetActive(false);
        if (feedbackCharacterPanel != null) feedbackCharacterPanel.SetActive(false);
        if (rewardPanel != null) rewardPanel.SetActive(false);
    }

    private void RefreshCharacterVisual()
    {
        if (characterVisualController != null && scanRFIDController != null)
        {
            string selectedCharacter = scanRFIDController.GetSelectedCharacter();

            if (selectedCharacter != "")
            {
                characterVisualController.UpdateVisualByCharacter(selectedCharacter);
                Debug.Log("Refresh visual karakter: " + selectedCharacter);
            }
            else
            {
                Debug.Log("Belum ada karakter terpilih, visual tidak di-refresh.");
            }
        }
        else
        {
            if (characterVisualController == null)
            {
                Debug.LogWarning("CharacterVisualController belum diisi di UIManagerDonimal.");
            }

            if (scanRFIDController == null)
            {
                Debug.LogWarning("ScanRFIDController belum diisi di UIManagerDonimal.");
            }
        }
    }

    public void ShowSplashPanel()
    {
        HideAllPanels();

        if (splashPanel != null)
        {
            splashPanel.SetActive(true);
            Debug.Log("Masuk ke SplashPanel.");
        }
        else
        {
            Debug.LogWarning("SplashPanel belum diisi.");
        }
    }

    public void ShowScanRFIDPanel()
    {
        HideAllPanels();

        if (scanRFIDPanel != null)
        {
            scanRFIDPanel.SetActive(true);
            Debug.Log("Masuk ke ScanRFIDPanel.");
        }
        else
        {
            Debug.LogWarning("ScanRFIDPanel belum diisi.");
        }
    }

    private void RestoreDashboardCategories()
    {
        Transform categories = dashboardPanel.transform.Find("kategori");
        if (categories == null) return;
        categories.gameObject.SetActive(true);
        foreach (string category in new[] { "huruf", "angka", "hewan", "warna" })
        {
            Transform item = categories.Find(category);
            if (item == null) continue;
            item.gameObject.SetActive(true);
            Transform icon = item.Find(category);
            if (icon != null) icon.gameObject.SetActive(true);
        }
    }

    public void ShowDashboardPanel()
    {
        HideAllPanels();

        if (dashboardPanel != null)
        {
            dashboardPanel.SetActive(true);
            RestoreDashboardCategories();
            Debug.Log("Masuk ke DashboardPanel.");
        }
        else
        {
            Debug.LogWarning("DashboardPanel belum diisi.");
        }

        RefreshCharacterVisual();

        if (audioManager != null)
        {
            audioManager.PlayDashboardAudio();
        }
    }

    public void ShowBelajarPanel()
    {
        HideAllPanels();

        if (belajarPanel != null)
        {
            belajarPanel.SetActive(true);
            Debug.Log("Masuk ke BelajarPanel.");
        }
        else
        {
            Debug.LogWarning("BelajarPanel belum diisi.");
        }

        RefreshCharacterVisual();

        if (audioManager != null)
        {
            audioManager.PlayMenungguKategoriAudio();
        }
    }

    public void ShowCategoryPanel()
    {
        DonimalAPI.Instance?.CancelActivity();
        HideAllPanels();

        if (categoryPanel != null)
        {
            categoryPanel.SetActive(true);
            Debug.Log("Masuk ke CategoryPanel.");
        }
        else
        {
            Debug.LogWarning("CategoryPanel belum diisi.");
        }

        RefreshCharacterVisual();

        if (audioManager != null)
        {
            audioManager.PlayMenungguSubgameAudio();
        }
    }

    public void ShowMiniGamePanel()
    {
        HideAllPanels();

        if (miniGamePanel != null)
        {
            miniGamePanel.SetActive(true);
            Debug.Log("Masuk ke MiniGamePanel.");
        }
        else
        {
            Debug.LogWarning("MiniGamePanel belum diisi.");
        }

        RefreshCharacterVisual();
    }

    public void ShowFeedbackCharacterPanel()
    {
        HideAllPanels();

        if (feedbackCharacterPanel != null)
        {
            feedbackCharacterPanel.SetActive(true);
            Debug.Log("Masuk ke FeedbackCharacterPanel.");
        }
        else
        {
            Debug.LogWarning("FeedbackCharacterPanel belum diisi.");
        }

        RefreshCharacterVisual();
    }

    public void ShowRewardPanel()
    {
        HideAllPanels();

        if (rewardPanel != null)
        {
            rewardPanel.SetActive(true);
            Debug.Log("Masuk ke RewardPanel.");
        }
        else
        {
            Debug.LogWarning("RewardPanel belum diisi.");
        }

        RefreshCharacterVisual();
    }

    public void QuitGame()
    {
        Debug.Log("Menutup game.");

#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }
}
