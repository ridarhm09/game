using UnityEngine;

public class UIManagerDonimal : MonoBehaviour
{
    [Header("Panels")]
    public GameObject splashPanel;
    public GameObject scanRFIDPanel;
    public GameObject dashboardPanel;
    public GameObject categoryPanel;
    public GameObject miniGamePanel;
    public GameObject feedbackCharacterPanel;

    private void Start()
    {
        ShowSplashPanel();
    }

    private void Update()
    {
        // Testing sementara: tekan Space untuk masuk ke Scan RFID
        if (Input.GetKeyDown(KeyCode.Space))
        {
            ShowScanRFIDPanel();
        }
    }

    private void HideAllPanels()
    {
        if (splashPanel != null) splashPanel.SetActive(false);
        if (scanRFIDPanel != null) scanRFIDPanel.SetActive(false);
        if (dashboardPanel != null) dashboardPanel.SetActive(false);
        if (categoryPanel != null) categoryPanel.SetActive(false);
        if (miniGamePanel != null) miniGamePanel.SetActive(false);
        if (feedbackCharacterPanel != null) feedbackCharacterPanel.SetActive(false);
    }

    public void ShowSplashPanel()
    {
        HideAllPanels();

        if (splashPanel != null)
        {
            splashPanel.SetActive(true);
        }
    }

    public void ShowScanRFIDPanel()
    {
        Debug.Log("Pindah ke ScanRFIDPanel");

        HideAllPanels();

        if (scanRFIDPanel != null)
        {
            scanRFIDPanel.SetActive(true);
        }
    }

    public void ShowDashboardPanel()
    {
        Debug.Log("Pindah ke DashboardPanel");

        HideAllPanels();

        if (dashboardPanel != null)
        {
            dashboardPanel.SetActive(true);
        }
    }

    public void ShowCategoryPanel()
    {
        Debug.Log("Pindah ke CategoryPanel");

        HideAllPanels();

        if (categoryPanel != null)
        {
            categoryPanel.SetActive(true);
        }
    }

    public void ShowMiniGamePanel()
    {
        Debug.Log("Pindah ke MiniGamePanel");

        HideAllPanels();

        if (miniGamePanel != null)
        {
            miniGamePanel.SetActive(true);
        }
    }

    public void ShowFeedbackCharacterPanel()
    {
        Debug.Log("Pindah ke FeedbackCharacterPanel");

        HideAllPanels();

        if (feedbackCharacterPanel != null)
        {
            feedbackCharacterPanel.SetActive(true);
        }
    }
}