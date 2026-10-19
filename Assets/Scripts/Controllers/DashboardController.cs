using UnityEngine;

public class DashboardController : MonoBehaviour
{
    [Header("UI Manager")]
    public UIManagerDonimal uiManager;

    [Header("Panel Check")]
    public GameObject dashboardPanel;

    [Header("Category MiniGame Groups")]
    [Tooltip("Isi dengan GROUP SUBGAME HURUF di CategoryPanel, bukan tombol Huruf di Dashboard.")]
    public GameObject hurufMiniGameGroup;

    [Tooltip("Isi dengan GROUP SUBGAME ANGKA di CategoryPanel, bukan tombol Angka di Dashboard.")]
    public GameObject angkaMiniGameGroup;

    [Tooltip("Isi dengan GROUP SUBGAME HEWAN di CategoryPanel, bukan tombol Hewan di Dashboard.")]
    public GameObject hewanMiniGameGroup;

    [Tooltip("Isi dengan GROUP SUBGAME WARNA di CategoryPanel, bukan tombol Warna di Dashboard.")]
    public GameObject warnaMiniGameGroup;

    private string selectedCategory = "";

    private void Awake()
    {
        // Yang disembunyikan hanya isi subgame di CategoryPanel.
        // Tombol Huruf / Angka / Hewan / Warna di Dashboard
        // TIDAK boleh dimasukkan ke field ini.
        HideAllMiniGameGroups();
    }

    private void Update()
    {
        // Keyboard hanya aktif kalau Dashboard sedang tampil.
        if (dashboardPanel == null || !dashboardPanel.activeInHierarchy)
            return;

        // BELAJAR
        if (KeyboardInputUtility.WasPressed(KeyCode.B))
        {
            OpenBelajarPanel();
        }

        // KATEGORI
        if (KeyboardInputUtility.WasPressed(KeyCode.H))
        {
            ClickHuruf();
        }

        if (KeyboardInputUtility.WasPressed(KeyCode.A))
        {
            ClickAngka();
        }

        if (KeyboardInputUtility.WasPressed(KeyCode.N))
        {
            ClickHewan();
        }

        if (KeyboardInputUtility.WasPressed(KeyCode.W))
        {
            ClickWarna();
        }
    }

    //====================================================
    // BELAJAR
    //====================================================

    public void OpenBelajarPanel()
    {
        if (uiManager == null)
        {
            Debug.LogError("[DashboardController] UIManager belum dihubungkan.");
            return;
        }

        uiManager.ShowBelajarPanel();

        Debug.Log("[DashboardController] Masuk ke BelajarPanel.");
    }

    //====================================================
    // CATEGORY
    //====================================================

    public void SelectCategory(string category)
    {
        if (string.IsNullOrEmpty(category))
        {
            Debug.LogWarning("[DashboardController] Category kosong.");
            return;
        }

        selectedCategory = category.ToLower().Trim();

        // Matikan semua GROUP SUBGAME terlebih dahulu.
        HideAllMiniGameGroups();

        switch (selectedCategory)
        {
            case "huruf":
                if (hurufMiniGameGroup != null)
                    hurufMiniGameGroup.SetActive(true);
                else
                    Debug.LogError("[DashboardController] Huruf MiniGame Group belum diisi.");
                break;

            case "angka":
                if (angkaMiniGameGroup != null)
                    angkaMiniGameGroup.SetActive(true);
                else
                    Debug.LogError("[DashboardController] Angka MiniGame Group belum diisi.");
                break;

            case "hewan":
                if (hewanMiniGameGroup != null)
                    hewanMiniGameGroup.SetActive(true);
                else
                    Debug.LogError("[DashboardController] Hewan MiniGame Group belum diisi.");
                break;

            case "warna":
                if (warnaMiniGameGroup != null)
                    warnaMiniGameGroup.SetActive(true);
                else
                    Debug.LogError("[DashboardController] Warna MiniGame Group belum diisi.");
                break;

            default:
                Debug.LogWarning(
                    "[DashboardController] Kategori tidak dikenali: " +
                    selectedCategory
                );
                return;
        }

        if (uiManager == null)
        {
            Debug.LogError("[DashboardController] UIManager belum dihubungkan.");
            return;
        }

        uiManager.ShowCategoryPanel();

        Debug.Log(
            "[DashboardController] Kategori dipilih: " +
            selectedCategory
        );
    }

    //====================================================
    // BUTTON / KEYBOARD
    //====================================================

    public void ClickBelajar()
    {
        OpenBelajarPanel();
    }

    public void ClickHuruf()
    {
        SelectCategory("huruf");
    }

    public void ClickAngka()
    {
        SelectCategory("angka");
    }

    public void ClickHewan()
    {
        SelectCategory("hewan");
    }

    public void ClickWarna()
    {
        SelectCategory("warna");
    }

    //====================================================
    // RFID
    //====================================================

    public void SelectDashboardByRFID(string rfid)
    {
        if (string.IsNullOrEmpty(rfid))
            return;

        rfid = rfid.ToLower().Trim();

        switch (rfid)
        {
            case "huruf:n":
                ClickBelajar();
                break;

            case "huruf:o":
                ClickHuruf();
                break;

            case "huruf:p":
                ClickAngka();
                break;

            case "huruf:q":
                ClickHewan();
                break;

            case "huruf:r":
                ClickWarna();
                break;

            default:
                Debug.Log(
                    "[DashboardController] RFID Dashboard tidak dikenali: " +
                    rfid
                );
                break;
        }
    }

    //====================================================
    // HELPER
    //====================================================

    private void HideAllMiniGameGroups()
    {
        // Referensi scene lama pernah menunjuk ikon Dashboard.
        // Hanya grup di dalam CategoryPanel yang boleh disembunyikan.
        if (uiManager == null || uiManager.categoryPanel == null) return;
        Transform panel = uiManager.categoryPanel.transform;
        hurufMiniGameGroup = panel.Find("huruf")?.gameObject;
        angkaMiniGameGroup = panel.Find("angka")?.gameObject;
        hewanMiniGameGroup = panel.Find("hewan")?.gameObject;
        warnaMiniGameGroup = panel.Find("warna")?.gameObject;
        if (hurufMiniGameGroup != null)
            hurufMiniGameGroup.SetActive(false);

        if (angkaMiniGameGroup != null)
            angkaMiniGameGroup.SetActive(false);

        if (hewanMiniGameGroup != null)
            hewanMiniGameGroup.SetActive(false);

        if (warnaMiniGameGroup != null)
            warnaMiniGameGroup.SetActive(false);
    }

    public string GetSelectedCategory()
    {
        return selectedCategory;
    }
}
