using TMPro;
using UnityEngine;
using UnityEngine.UI;

public class SubGameLevelView : MonoBehaviour
{
    [Header("Level Identity")]
    public string category;
    public string subGame;

    [Header("UI")]
    public Button playButton;
    [Tooltip("Object lencana yang muncul setelah level selesai.")]
    public GameObject completedObject;
    public TMP_Text levelText;
    [Tooltip("Isi berurutan dengan indikator 1, 2, dan 3.")]
    public GameObject[] activeStars = new GameObject[3];

    private void OnEnable()
    {
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.ProgressChanged += Refresh;
        }

        Refresh();
    }

    private void OnDisable()
    {
        if (AchievementManager.Instance != null)
        {
            AchievementManager.Instance.ProgressChanged -= Refresh;
        }
    }

    public void Refresh()
    {
        AchievementManager manager = AchievementManager.Instance;
        if (manager == null) return;

        bool unlocked = manager.CanPlaySubGame(category, subGame);
        int stars = manager.GetBestStars(subGame);
        int level = manager.GetLevelNumber(category, subGame);

        if (playButton != null) playButton.interactable = unlocked;
        if (completedObject != null) completedObject.SetActive(stars > 0);
        if (levelText != null) levelText.text = level > 0 ? "LEVEL " + level : "";

        if (activeStars == null) return;

        for (int i = 0; i < activeStars.Length; i++)
        {
            if (activeStars[i] != null)
            {
                activeStars[i].SetActive(i < stars);
            }
        }
    }
}
