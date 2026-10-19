using UnityEngine;

public class RewardPanelController : MonoBehaviour
{
    public UIManagerDonimal uiManager;
    public ScoreManager scoreManager;

    public void ShowReward()
    {
        if (scoreManager != null)
        {
            scoreManager.UpdateAllUI();
        }

        if (uiManager != null)
        {
            uiManager.ShowRewardPanel();
        }
    }

    public void BackToDashboard()
    {
        if (uiManager != null)
        {
            uiManager.ShowDashboardPanel();
        }
    }

    public void ResetAndBackToDashboard()
    {
        if (scoreManager != null)
        {
            scoreManager.ResetScore();
        }

        if (uiManager != null)
        {
            uiManager.ShowDashboardPanel();
        }
    }
}