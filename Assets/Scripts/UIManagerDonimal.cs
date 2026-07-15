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

    private void HideAllPanels()
    {
        splashPanel.SetActive(false);
        scanRFIDPanel.SetActive(false);
        dashboardPanel.SetActive(false);
        categoryPanel.SetActive(false);
        miniGamePanel.SetActive(false);
        feedbackCharacterPanel.SetActive(false);
    }

    public void ShowSplashPanel()
    {
        HideAllPanels();
        splashPanel.SetActive(true);
    }

    public void ShowScanRFIDPanel()
    {
        HideAllPanels();
        scanRFIDPanel.SetActive(true);
    }

    public void ShowDashboardPanel()
    {
        HideAllPanels();
        dashboardPanel.SetActive(true);
    }

    public void ShowCategoryPanel()
    {
        HideAllPanels();
        categoryPanel.SetActive(true);
    }

    public void ShowMiniGamePanel()
    {
        HideAllPanels();
        miniGamePanel.SetActive(true);
    }

    public void ShowFeedbackCharacterPanel()
    {
        HideAllPanels();
        feedbackCharacterPanel.SetActive(true);
    }
}