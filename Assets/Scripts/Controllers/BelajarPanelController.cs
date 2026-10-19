using System.Collections;
using UnityEngine;
using UnityEngine.UI; // Untuk tipe RawImage

public class BelajarPanelController : MonoBehaviour
{
    [System.Serializable]
    public class LearningVisual
    {
        public string category;
        public string value;
        public GameObject target;
    }

    [Header("Gambar Materi di Scene")]
    public LearningVisual[] learningVisuals = new LearningVisual[0];

    [Header("Panel Check")]
    public GameObject belajarPanel;

    [Header("Navigasi")]
    public UIManagerDonimal uiManager;

    [Header("Audio Manager")]
    public AudioManagerDonimal audioManager;

    [Header("Display Duration")]
    public float displayDuration = 5f;

    [Tooltip("Aktifkan untuk shortcut lama R=merah dan L=biru. Nonaktif: A-Z membaca huruf.")]
    public bool useLegacyColorShortcuts;

    [Header("Dynamic Learning Display")]
    [Tooltip("Isi dengan RawImage yang akan menampung gambar dari server")]
    public RawImage dynamicLearningImage;

    [Tooltip("Opsional: Image pada BelajarPanel/materi/materi untuk gambar server.")]
    public Image serverLearningImage;
    private Sprite downloadedSprite;

    private Coroutine hideCoroutine;
    private bool wasPanelActive;
    private int requestVersion;

    private void OnDisable()
    {
        ResetLearningDisplay();
    }

    private bool IsCurrentRequest(int version)
    {
        return this != null && version == requestVersion &&
            belajarPanel != null && belajarPanel.activeInHierarchy;
    }

    private void Awake()
    {
        ResolveSceneReferences();
        if (belajarPanel == null)
            Debug.LogError("[BelajarPanel] BelajarPanel tidak ditemukan. Isi field Belajar Panel di Inspector.", this);
        HideLearningDisplay();

        wasPanelActive =
            belajarPanel != null &&
            belajarPanel.activeInHierarchy;
    }

    private void Reset()
    {
        ResolveSceneReferences();
    }

    [ContextMenu("Hubungkan Referensi Belajar")]
    private void ResolveSceneReferences()
    {
        if (!gameObject.scene.IsValid()) return;

        // Cari pada scene yang sama, termasuk panel yang sedang nonaktif.
        foreach (GameObject root in gameObject.scene.GetRootGameObjects())
        {
            foreach (UIManagerDonimal manager in root.GetComponentsInChildren<UIManagerDonimal>(true))
            {
                if (uiManager == null) uiManager = manager;
                if (belajarPanel == null) belajarPanel = manager.belajarPanel;
                if (audioManager == null) audioManager = manager.audioManager;
            }
        }
        if (belajarPanel == null)
        {
            foreach (GameObject root in gameObject.scene.GetRootGameObjects())
            {
                foreach (Transform candidate in root.GetComponentsInChildren<Transform>(true))
                {
                    if (candidate.name != "BelajarPanel") continue;
                    belajarPanel = candidate.gameObject;
                    break;
                }
                if (belajarPanel != null) break;
            }
        }
        if (belajarPanel == null || dynamicLearningImage != null || serverLearningImage != null) return;
        Transform target = belajarPanel.transform.Find("materi/materi");
        if (target == null) return;
        dynamicLearningImage = target.GetComponent<RawImage>();
        if (dynamicLearningImage == null) serverLearningImage = target.GetComponent<Image>();
    }

    private DonimalAPI ResolveApi()
    {
        if (DonimalAPI.Instance != null)
            return DonimalAPI.Instance;

        DonimalAPI api = FindFirstObjectByType<DonimalAPI>(FindObjectsInactive.Include);
        if (api != null)
            Debug.LogWarning("[BelajarPanel] DonimalAPI ditemukan melalui fallback scene.");
        return api;
    }

    // Hubungkan ke Button Back > On Click() di Inspector.
    public void BackToDashboard()
    {
        if (uiManager == null) ResolveSceneReferences();
        if (uiManager == null || uiManager.dashboardPanel == null)
        {
            Debug.LogWarning("[BelajarPanel] UIManager atau DashboardPanel belum diisi.", this);
            return;
        }

        ResetLearningDisplay();
        wasPanelActive = false;
        uiManager.ShowDashboardPanel();
    }

    private void Update()
    {
        if (belajarPanel == null) return;

        bool isPanelActive = belajarPanel.activeInHierarchy;

        if (!isPanelActive && wasPanelActive)
        {
            ResetLearningDisplay();
        }

        wasPanelActive = isPanelActive;

        if (!isPanelActive) return;

        // =========================
        // KEYBOARD SIMULATION
        // =========================
        if (useLegacyColorShortcuts && KeyboardInputUtility.WasPressed(KeyCode.R))
        {
            ShowLearningCard("warna", "merah");
            return;
        }
        if (useLegacyColorShortcuts && KeyboardInputUtility.WasPressed(KeyCode.L))
        {
            ShowLearningCard("warna", "biru");
            return;
        }
        for (int index = 0; index < 26; index++)
        {
            if (!KeyboardInputUtility.WasPressed((KeyCode)((int)KeyCode.A + index))) continue;
            ShowLearningCard("huruf", ((char)('a' + index)).ToString());
            return;
        }
        for (int number = 0; number <= 9; number++)
        {
            if (!KeyboardInputUtility.WasPressed((KeyCode)((int)KeyCode.Alpha0 + number)) &&
                !KeyboardInputUtility.WasPressed((KeyCode)((int)KeyCode.Keypad0 + number))) continue;
            ShowLearningCard("angka", number.ToString());
            return;
        }
    }

    public void ShowLearningCard(string category, string value)
    {
        ShowLearningItem(category + ":" + value);
    }

    public void ShowLearningItem(string inputValue)
    {
        if (belajarPanel == null) ResolveSceneReferences();
        if (belajarPanel == null || !belajarPanel.activeInHierarchy) return;

        string cleanValue = NormalizeValue(inputValue);
        int separator = inputValue == null ? -1 : inputValue.IndexOf(':');
        string category = separator < 0 ? GetAudioCategory(cleanValue) :
            inputValue.Substring(0, separator).Trim().ToLowerInvariant();
        if (category == "animal") category = "hewan";
        cleanValue = RfidAnswerUtility.Normalize(category, cleanValue);
        
        StopHideTimer();
        HideLearningDisplay();
        int version = requestVersion;
        if (string.IsNullOrEmpty(cleanValue)) return;

        // Audio sudah lokal: tetap putar walaupun server/gambar tidak tersedia.
        PlayLearningAudio(category, cleanValue);

        EnsureServerLearningDisplay();
        if (dynamicLearningImage == null && serverLearningImage == null)
        {
            Debug.LogError("[BelajarPanel] Tempat gambar server tidak tersedia.");
            return;
        }
        Debug.Log("[BelajarPanel] Request materi: " + cleanValue);

        DonimalAPI api = ResolveApi();
        if (api != null)
        {
            // Ambil info materi
            api.GetLearningMaterial(
                cleanValue,
                onSuccess: (materialData) => 
                {
                    if (!IsCurrentRequest(version) || materialData == null) return;
                    if (string.IsNullOrWhiteSpace(materialData.media_url))
                    {
                        Debug.LogError("[BelajarPanel] Server tidak mengirim media_url untuk: " + cleanValue);
                        return;
                    }
                    Debug.Log("[BelajarPanel] Info didapat, mengunduh gambar: " + materialData.media_url);
                    // Download & tampilkan gambar
                    api.GetTexture(
                        materialData.media_url,
                        onSuccess: (texture) => 
                        {
                            if (!IsCurrentRequest(version)) return;
                            if (texture == null) return;
                            if (serverLearningImage != null)
                            {
                                downloadedSprite = Sprite.Create(texture,
                                    new Rect(0, 0, texture.width, texture.height), new Vector2(0.5f, 0.5f));
                                serverLearningImage.sprite = downloadedSprite;
                                serverLearningImage.overrideSprite = null;
                                serverLearningImage.preserveAspect = true;
                                serverLearningImage.enabled = true;
                                ActivateLearningTarget(serverLearningImage.transform);
                            }
                            if (dynamicLearningImage != null)
                            {
                                dynamicLearningImage.texture = texture;
                                AspectRatioFitter fitter = dynamicLearningImage.GetComponent<AspectRatioFitter>();
                                if (fitter != null) fitter.aspectRatio = (float)texture.width / texture.height;
                                ActivateLearningTarget(dynamicLearningImage.transform);
                            }
                            StartHideTimer();
                        },
                        onError: (err) => 
                        {
                            if (!IsCurrentRequest(version)) return;
                            Debug.LogError("[BelajarPanel] Gagal unduh gambar untuk " + inputValue +
                                " | media_url: " + materialData.media_url + " | " + err);
                        }
                    );
                },
                onError: (err) => 
                {
                    if (!IsCurrentRequest(version)) return;
                    Debug.LogError("[BelajarPanel] Gagal request materi " + inputValue + " | " + err);
                }
            );
        }
        else
        {
            Debug.LogError("DonimalAPI tidak ada di Scene!");
        }
    }

    private static readonly string[] LearningGroups = { "Huruf", "Angka", "Hewan", "Warna" };

    private void HideLocalLearningImages()
    {
        if (belajarPanel == null) return;
        foreach (LearningVisual visual in learningVisuals)
            if (visual != null && visual.target != null) visual.target.SetActive(false);
        foreach (string groupName in LearningGroups)
        {
            Transform group = belajarPanel.transform.Find(groupName);
            if (group == null) continue;
            foreach (Transform item in group) item.gameObject.SetActive(false);
        }
    }

    private void EnsureServerLearningDisplay()
    {
        if (dynamicLearningImage != null || serverLearningImage != null || belajarPanel == null) return;

        // Gunakan layout materi yang sudah disiapkan di scene, termasuk objek nonaktif.
        Transform target = belajarPanel.transform.Find("materi/materi");
        if (target != null)
        {
            dynamicLearningImage = target.GetComponent<RawImage>();
            if (dynamicLearningImage == null) serverLearningImage = target.GetComponent<Image>();
            if (dynamicLearningImage != null || serverLearningImage != null) return;
        }

        RectTransform area = new GameObject("ServerLearningArea", typeof(RectTransform))
            .GetComponent<RectTransform>();
        area.SetParent(belajarPanel.transform, false);
        area.anchorMin = new Vector2(0.15f, 0.15f);
        area.anchorMax = new Vector2(0.85f, 0.85f);
        area.offsetMin = Vector2.zero;
        area.offsetMax = Vector2.zero;
        GameObject display = new GameObject("ServerLearningImage", typeof(RectTransform),
            typeof(RawImage), typeof(AspectRatioFitter));
        display.SetActive(false);
        display.transform.SetParent(area, false);
        dynamicLearningImage = display.GetComponent<RawImage>();
        dynamicLearningImage.raycastTarget = false;
        RectTransform rect = dynamicLearningImage.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
        AspectRatioFitter fitter = display.GetComponent<AspectRatioFitter>();
        fitter.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
        fitter.aspectRatio = 1f;
    }

    private void ActivateLearningTarget(Transform target)
    {
        while (target != null && target != belajarPanel.transform)
        {
            target.gameObject.SetActive(true);
            target = target.parent;
        }
    }

    private string NormalizeValue(string inputValue)
    {
        if (string.IsNullOrWhiteSpace(inputValue)) return "";
        string cleanValue = inputValue.ToLowerInvariant().Trim();
        if (cleanValue.Contains(":"))
        {
            string[] parts = cleanValue.Split(':');
            if (parts.Length >= 2) cleanValue = parts[parts.Length - 1].Trim();
        }
        return cleanValue;
    }

    private void PlayLearningAudio(string audioCategory, string value)
    {
        if (audioManager == null) ResolveSceneReferences();
        if (audioManager == null)
        {
            Debug.LogWarning("[BelajarPanel] AudioManager belum terhubung untuk " + audioCategory + ":" + value);
            return;
        }
        if (audioCategory == "") return;
        audioManager.PlayBelajarCardAudio(audioCategory, value);
    }

    private string GetAudioCategory(string value)
    {
        if (value.StartsWith("hrf-") || value.StartsWith("huruf ")) return "huruf";
        if (value.StartsWith("ang-") || value.StartsWith("angka ")) return "angka";
        if (value.StartsWith("hwn-") || value.StartsWith("hewan ") || value.StartsWith("animal ")) return "hewan";
        if (value.StartsWith("wrn-") || value.StartsWith("warna ") || value == "orange" || value == "oranye") return "warna";
        if (value.Length == 1 && value[0] >= 'a' && value[0] <= 'z') return "huruf";
        if (int.TryParse(value, out _)) return "angka";
        if (value == "merah" || value == "biru" || value == "kuning" || value == "hijau" ||
            value == "ungu" || value == "jingga" || value == "hitam" || value == "putih") return "warna";
        return "";
    }

    private void StartHideTimer()
    {
        StopHideTimer();
        hideCoroutine = StartCoroutine(HideAfterDelay());
    }

    private void StopHideTimer()
    {
        if (hideCoroutine != null)
        {
            StopCoroutine(hideCoroutine);
            hideCoroutine = null;
        }
    }

    private IEnumerator HideAfterDelay()
    {
        yield return new WaitForSeconds(displayDuration);
        HideLearningDisplay();
        hideCoroutine = null;
    }

    public void ResetLearningDisplay()
    {
        StopHideTimer();
        HideLearningDisplay();
    }

    public void HideLearningDisplay()
    {
        requestVersion++;
        EnsureServerLearningDisplay();
        HideLocalLearningImages();
        if (serverLearningImage != null)
        {
            serverLearningImage.overrideSprite = null;
            serverLearningImage.sprite = null;
            serverLearningImage.gameObject.SetActive(false);
        }
        if (downloadedSprite != null)
        {
            Destroy(downloadedSprite);
            downloadedSprite = null;
        }
        if (dynamicLearningImage != null)
        {
            dynamicLearningImage.texture = null; // Clear
            dynamicLearningImage.gameObject.SetActive(false);
        }
    }
}
