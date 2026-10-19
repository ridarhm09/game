using System;
using System.Collections.Generic;
using UnityEngine;

public class AchievementManager : MonoBehaviour
{
    public static AchievementManager Instance { get; private set; }

    private const string KeyPrefix = "donimal.achievement.";
    private static string ProfilePrefix => DonimalAPI.Instance != null &&
        DonimalAPI.Instance.requireChildRfid && DonimalAPI.Instance.CurrentChildProgress != null
        ? KeyPrefix + DonimalAPI.Instance.CurrentChildProgress.child_id + "."
        : KeyPrefix;
    private static string CoinKey => ProfilePrefix + "coins";
    private const int CategoryBadgeReward = 100;
    private const int MasterBadgeReward = 500;

    private static readonly Dictionary<string, string[]> CategorySubGames =
        new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase)
        {
            { "huruf", new[] { "tebak_bunyi", "lengkapi_huruf", "berburu_huruf" } },
            { "angka", new[] { "hitung_benda", "menyusun_angka", "cari_angka" } },
            { "hewan", new[] { "tebak_suara", "tebak_bayangan", "tebak_deskripsi" } },
            { "warna", new[] { "tebak_warna" } }
        };

    public event Action<string, int> AchievementUnlocked;
    public event Action<int> CoinsChanged;
    public event Action<string, string> SubGameUnlocked;
    public event Action ProgressChanged;

    public void RefreshServerProgress() { ProgressChanged?.Invoke(); }

    public int Coins => PlayerPrefs.GetInt(CoinKey, 0);

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    private static void Bootstrap()
    {
        if (Instance != null) return;

        GameObject managerObject = new GameObject("AchievementManager");
        managerObject.AddComponent<AchievementManager>();
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);
    }

    public int CompleteSubGame(
        string category,
        string subGame,
        int correctCount,
        int wrongCount,
        int earnedCoins)
    {
        string normalizedCategory = Normalize(category);
        string normalizedSubGame = Normalize(subGame);
        int stars = CalculateStars(correctCount, wrongCount);
        int previousBest = GetBestStars(normalizedSubGame);

        if (stars > previousBest)
        {
            PlayerPrefs.SetInt(StarsKey(normalizedSubGame), stars);

            if (previousBest == 0 && stars > 0)
            {
                NotifyNextSubGameUnlocked(normalizedCategory, normalizedSubGame);
            }
        }

        if (earnedCoins > 0)
        {
            AddCoins(earnedCoins);
        }

        TryUnlockCategoryBadge(normalizedCategory);
        TryUnlockMasterBadge();
        PlayerPrefs.Save();
        ProgressChanged?.Invoke();

        Debug.Log(
            "[Achievement] Subgame selesai: " + normalizedSubGame +
            " | benar=" + correctCount +
            " | salah=" + wrongCount +
            " | bintang=" + stars +
            " | terbaik=" + GetBestStars(normalizedSubGame)
        );

        return stars;
    }

    public int GetBestStars(string subGame)
    {
        return PlayerPrefs.GetInt(StarsKey(Normalize(subGame)), 0);
    }

    public bool IsSubGameCompleted(string subGame)
    {
        return GetBestStars(subGame) > 0;
    }

    public bool CanPlaySubGame(string category, string subGame)
    {
        string normalizedCategory = Normalize(category);
        string normalizedSubGame = Normalize(subGame);

        if (!CategorySubGames.TryGetValue(normalizedCategory, out string[] subGames))
            return true;

        int levelIndex = Array.IndexOf(subGames, normalizedSubGame);
        if (levelIndex < 0) return true;
        DonimalAPI api = DonimalAPI.Instance;
        if (api != null && api.requireChildRfid && !api.HasChildRfid())
            return false;
        if (levelIndex == 0) return true;

        return IsSubGameCompleted(subGames[levelIndex - 1]);
    }

    public int GetLevelNumber(string category, string subGame)
    {
        if (!CategorySubGames.TryGetValue(Normalize(category), out string[] subGames))
            return 0;

        int index = Array.IndexOf(subGames, Normalize(subGame));
        return index < 0 ? 0 : index + 1;
    }

    public string GetRequiredPreviousSubGame(string category, string subGame)
    {
        if (!CategorySubGames.TryGetValue(Normalize(category), out string[] subGames))
            return "";

        int index = Array.IndexOf(subGames, Normalize(subGame));
        return index <= 0 ? "" : subGames[index - 1];
    }

    public bool IsCategoryBadgeUnlocked(string category)
    {
        return PlayerPrefs.GetInt(BadgeKey(Normalize(category)), 0) == 1;
    }

    public bool IsMasterBadgeUnlocked()
    {
        return PlayerPrefs.GetInt(BadgeKey("master_donimal"), 0) == 1;
    }

    public int GetCompletedSubGameCount(string category)
    {
        string normalizedCategory = Normalize(category);
        if (!CategorySubGames.TryGetValue(normalizedCategory, out string[] subGames))
            return 0;

        int completed = 0;
        foreach (string subGame in subGames)
        {
            if (IsSubGameCompleted(subGame)) completed++;
        }
        return completed;
    }

    public int GetTotalSubGameCount(string category)
    {
        return CategorySubGames.TryGetValue(Normalize(category), out string[] subGames)
            ? subGames.Length
            : 0;
    }

    public bool SpendCoins(int amount)
    {
        amount = Mathf.Max(0, amount);
        if (Coins < amount) return false;

        PlayerPrefs.SetInt(CoinKey, Coins - amount);
        PlayerPrefs.Save();
        CoinsChanged?.Invoke(Coins);
        return true;
    }

    private int CalculateStars(int correctCount, int wrongCount)
    {
        int total = Mathf.Max(0, correctCount) + Mathf.Max(0, wrongCount);
        if (total == 0 || correctCount == 0) return 0;

        float accuracy = (float)correctCount / total;
        if (accuracy >= 0.9f) return 3;
        if (accuracy >= 0.7f) return 2;
        return 1;
    }

    private void TryUnlockCategoryBadge(string category)
    {
        if (!CategorySubGames.TryGetValue(category, out string[] subGames)) return;
        if (IsCategoryBadgeUnlocked(category)) return;

        foreach (string subGame in subGames)
        {
            if (!IsSubGameCompleted(subGame)) return;
        }

        UnlockBadge(category, CategoryBadgeReward);
    }

    private void NotifyNextSubGameUnlocked(string category, string completedSubGame)
    {
        if (!CategorySubGames.TryGetValue(category, out string[] subGames)) return;

        int completedIndex = Array.IndexOf(subGames, completedSubGame);
        int nextIndex = completedIndex + 1;
        if (completedIndex < 0 || nextIndex >= subGames.Length) return;

        SubGameUnlocked?.Invoke(category, subGames[nextIndex]);
        Debug.Log(
            "[Leveling] Level terbuka: " + category + " / " +
            subGames[nextIndex] + " (Level " + (nextIndex + 1) + ")"
        );
    }

    private void TryUnlockMasterBadge()
    {
        if (IsMasterBadgeUnlocked()) return;

        foreach (string category in CategorySubGames.Keys)
        {
            if (!IsCategoryBadgeUnlocked(category)) return;
        }

        UnlockBadge("master_donimal", MasterBadgeReward);
    }

    private void UnlockBadge(string badgeId, int reward)
    {
        PlayerPrefs.SetInt(BadgeKey(badgeId), 1);
        AddCoins(reward);
        AchievementUnlocked?.Invoke(badgeId, reward);
        Debug.Log("[Achievement] Lencana terbuka: " + badgeId + " | bonus koin=" + reward);
    }

    private void AddCoins(int amount)
    {
        PlayerPrefs.SetInt(CoinKey, Coins + Mathf.Max(0, amount));
        CoinsChanged?.Invoke(Coins);
    }

    private static string StarsKey(string subGame)
    {
        return ProfilePrefix + "stars." + subGame;
    }

    private static string BadgeKey(string badge)
    {
        return ProfilePrefix + "badge." + badge;
    }

    private static string Normalize(string value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? ""
            : value.Trim().ToLowerInvariant().Replace(" ", "_");
    }
}
