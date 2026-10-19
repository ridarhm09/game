using UnityEngine;

public class DashboardCategoryProgressView : MonoBehaviour
{
    [Header("Category")]
    [Tooltip("Contoh: huruf, angka, hewan, atau warna.")]
    public string category;

    [Header("UI")]
    [Tooltip("Lencana kategori. Aktif setelah semua subgame selesai.")]
    public GameObject badgeObject;

    [Tooltip("Indikator progres secara berurutan dari kiri ke kanan.")]
    public GameObject[] progressIndicators;

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
        if (manager == null)
        {
            return;
        }

        int completedCount = manager.GetCompletedSubGameCount(category);
        bool badgeUnlocked = manager.IsCategoryBadgeUnlocked(category);

        if (badgeObject != null)
        {
            badgeObject.SetActive(badgeUnlocked);
        }

        if (progressIndicators == null)
        {
            return;
        }

        for (int i = 0; i < progressIndicators.Length; i++)
        {
            if (progressIndicators[i] != null)
            {
                progressIndicators[i].SetActive(i < completedCount);
            }
        }
    }
}
