using UnityEngine;
using UnityEngine.Serialization;

public class CategoryToMiniGameImageController : MonoBehaviour
{
    [Header("Main Controllers")]
    public UIManagerDonimal uiManager;
    public DashboardController dashboardController;
    public AudioManagerDonimal audioManager;

    [Header("Panel Check")]
    public GameObject miniGamePanel;

    [Header("Question Controllers")]
    public BunyiQuestionController bunyiQuestionController;
    public BerburuQuestionController berburuQuestionController;
    public LengkapiQuestionController lengkapiQuestionController;
    public CariQuestionController cariQuestionController;
    public HitungQuestionController hitungQuestionController;
    public SusunQuestionController susunQuestionController;
    public DeskripsiQuestionController deskripsiQuestionController;
    public SuaraQuestionController suaraQuestionController;
    public BayanganQuestionController bayanganQuestionController;
    public TebakQuestionController tebakQuestionController;

    [Header("Audio Settings")]
    [Tooltip("Audio pembuka digunakan untuk subgame selain Tebak Bunyi.")]
    public bool playDefaultAudioOnOpen = true;

    // ==================================================
    // PARENT KATEGORI MINIGAME
    // ==================================================

    [Header("Parent Kategori MiniGame")]
    public GameObject hurufGroup;
    public GameObject angkaGroup;
    public GameObject hewanGroup;
    public GameObject warnaGroup;

    // ==================================================
    // SUBGAME HURUF
    // ==================================================

    [Header("Soal Huruf")]
    public GameObject bunyi;
    public GameObject berburu;
    public GameObject lengkapi;

    // ==================================================
    // SUBGAME ANGKA
    // ==================================================

    [Header("Soal Angka")]
    public GameObject hitung;
    public GameObject susun;
    public GameObject cari;

    // ==================================================
    // SUBGAME HEWAN
    // ==================================================

    [Header("Soal Hewan")]
    public GameObject suara;
    [FormerlySerializedAs("habitat")]
    public GameObject bayangan;
    public GameObject deskripsi;

    // ==================================================
    // SUBGAME WARNA
    // ==================================================

    [Header("Soal Warna")]
    public GameObject tebak;

    private string selectedCategory = "";
    private string selectedSubGame = "";
    private string currentCorrectAnswer = "";

    // ==================================================
    // UNITY EVENTS
    // ==================================================

    private void Awake()
    {
        ResolveSceneReferences();

        if (uiManager == null)
        {
            uiManager = FindFirstObjectByType<UIManagerDonimal>(FindObjectsInactive.Include);
        }

        ResolveBunyiQuestionController();
        ResolveBerburuQuestionController();
        ResolveLengkapiQuestionController();
        ResolveCariQuestionController();
        ResolveHitungQuestionController();
        ResolveSusunQuestionController();
        ResolveDeskripsiQuestionController();
        ResolveSuaraQuestionController();
        ResolveBayanganQuestionController();
        ResolveTebakQuestionController();
    }

    private void ResolveSceneReferences()
    {
        if (uiManager == null)
        {
            uiManager = FindFirstObjectByType<UIManagerDonimal>(FindObjectsInactive.Include);
        }

        if (dashboardController == null)
        {
            dashboardController = FindFirstObjectByType<DashboardController>(FindObjectsInactive.Include);
        }

        if (audioManager == null && uiManager != null)
        {
            audioManager = uiManager.audioManager;
        }

        if (miniGamePanel == null && uiManager != null)
        {
            miniGamePanel = uiManager.miniGamePanel;
        }
    }

    private void Start()
    {
        HideAllSoal();
    }

    public void BackToDashboard()
    {
        ResolveSceneReferences();

        HideAllSoal();
        DonimalAPI.Instance?.CancelActivity();

        if (uiManager == null)
        {
            Debug.LogWarning("[MiniGamePanel] UIManager belum ditemukan.");
            return;
        }

        uiManager.ShowDashboardPanel();
        Debug.Log("[MiniGamePanel] Kembali ke DashboardPanel.");
    }

    // ==================================================
    // MENCARI BUNYI QUESTION CONTROLLER
    // ==================================================

    private void ResolveBunyiQuestionController()
    {
        // Pertama cari pada GameObject yang sama.
        if (bunyiQuestionController == null)
        {
            bunyiQuestionController =
                GetComponent<BunyiQuestionController>();
        }

        // Jika tidak ditemukan, cari di seluruh Scene,
        // termasuk GameObject yang sedang nonaktif.
        if (bunyiQuestionController == null)
        {
            bunyiQuestionController =
                FindFirstObjectByType<BunyiQuestionController>(
                    FindObjectsInactive.Include
                );
        }

        if (bunyiQuestionController != null)
        {
            Debug.Log(
                "BunyiQuestionController ditemukan pada object: " +
                bunyiQuestionController.gameObject.name
            );
        }
        else
        {
            Debug.LogError(
                "BunyiQuestionController tidak ditemukan di Scene. " +
                "Pastikan komponennya sudah dipasang pada GlobalManager."
            );
        }
    }

    // ==================================================
    // MENCARI BERBURU QUESTION CONTROLLER
    // ==================================================

    private void ResolveBerburuQuestionController()
    {
        // Pertama cari pada GameObject yang sama.
        if (berburuQuestionController == null)
        {
            berburuQuestionController =
                GetComponent<BerburuQuestionController>();
        }

        // Jika tidak ditemukan, cari di seluruh Scene,
        // termasuk GameObject yang sedang nonaktif.
        if (berburuQuestionController == null)
        {
            berburuQuestionController =
                FindFirstObjectByType<BerburuQuestionController>(
                    FindObjectsInactive.Include
                );
        }

        if (berburuQuestionController != null)
        {
            Debug.Log(
                "BerburuQuestionController ditemukan pada object: " +
                berburuQuestionController.gameObject.name
            );
        }
        else
        {
            Debug.LogError(
                "BerburuQuestionController tidak ditemukan di Scene. " +
                "Pastikan komponennya sudah dipasang pada GlobalManager."
            );
        }
    }

    // ==================================================
    // MENCARI LENGKAPI HURUF QUESTION CONTROLLER
    // ==================================================

    private void ResolveLengkapiQuestionController()
    {
        if (lengkapiQuestionController == null)
        {
            lengkapiQuestionController =
                GetComponent<LengkapiQuestionController>();
        }

        if (lengkapiQuestionController == null)
        {
            lengkapiQuestionController =
                FindFirstObjectByType<LengkapiQuestionController>(
                    FindObjectsInactive.Include
                );
        }

        if (lengkapiQuestionController != null)
        {
            Debug.Log(
                "LengkapiQuestionController ditemukan pada object: " +
                lengkapiQuestionController.gameObject.name
            );
        }
        else
        {
            Debug.LogError(
                "LengkapiQuestionController tidak ditemukan di Scene. " +
                "Pasang komponennya pada GlobalManager."
            );
        }
    }

    // ==================================================
    // BUTTON HURUF
    // ==================================================

    public void ClickHurufBunyi()
    {
        bool berhasil = OpenSubGame(
            "huruf",
            "tebak_bunyi",
            hurufGroup,
            bunyi,
            false
        );

        if (!berhasil)
        {
            return;
        }

        // Cari ulang apabila referensi masih kosong.
        if (bunyiQuestionController == null)
        {
            ResolveBunyiQuestionController();
        }

        if (bunyiQuestionController == null)
        {
            Debug.LogError(
                "Subgame Bunyi terbuka, tetapi " +
                "BunyiQuestionController tidak ditemukan."
            );
            return;
        }

        Debug.Log("Memanggil StartQuestion Bunyi.");

        bunyiQuestionController.StartQuestion();
    }

    public void ClickHurufBerburu()
    {
        bool berhasil = OpenSubGame(
            "huruf",
            "berburu_huruf",
            hurufGroup,
            berburu,
            false
        );

        if (!berhasil)
        {
            return;
        }

        // Cari ulang apabila referensi masih kosong.
        if (berburuQuestionController == null)
        {
            ResolveBerburuQuestionController();
        }

        if (berburuQuestionController == null)
        {
            Debug.LogError(
                "Subgame Berburu Huruf terbuka, tetapi " +
                "BerburuQuestionController tidak ditemukan."
            );
            return;
        }

        Debug.Log(
            "Memanggil StartQuestion Berburu Huruf."
        );

        berburuQuestionController.StartQuestion();
    }

    public void ClickHurufLengkapi()
    {
        bool berhasil = OpenSubGame(
            "huruf",
            "lengkapi_huruf",
            hurufGroup,
            lengkapi,
            true
        );

        if (!berhasil)
        {
            return;
        }

        if (lengkapiQuestionController == null)
        {
            ResolveLengkapiQuestionController();
        }

        if (lengkapiQuestionController == null)
        {
            Debug.LogError(
                "Subgame Melengkapi Huruf terbuka, tetapi " +
                "LengkapiQuestionController tidak ditemukan."
            );
            return;
        }

        Debug.Log(
            "Memanggil StartQuestion Melengkapi Huruf."
        );

        lengkapiQuestionController.StartQuestion();
    }

    // ==================================================
    // MENCARI CARI ANGKA QUESTION CONTROLLER
    // ==================================================

    private void ResolveCariQuestionController()
    {
        if (cariQuestionController == null)
        {
            cariQuestionController =
                GetComponent<CariQuestionController>();
        }

        if (cariQuestionController == null)
        {
            cariQuestionController =
                FindFirstObjectByType<CariQuestionController>(
                    FindObjectsInactive.Include
                );
        }

        if (cariQuestionController != null)
        {
            Debug.Log(
                "CariQuestionController ditemukan pada object: " +
                cariQuestionController.gameObject.name
            );
        }
        else
        {
            Debug.LogError(
                "CariQuestionController tidak ditemukan di Scene. " +
                "Pasang komponennya pada GlobalManager."
            );
        }
    }


    // ==================================================
    // MENCARI HITUNG QUESTION CONTROLLER
    // ==================================================

    private void ResolveHitungQuestionController()
    {
        if (hitungQuestionController == null)
        {
            hitungQuestionController =
                GetComponent<HitungQuestionController>();
        }

        if (hitungQuestionController == null)
        {
            hitungQuestionController =
                FindFirstObjectByType<HitungQuestionController>(
                    FindObjectsInactive.Include
                );
        }

        if (hitungQuestionController != null)
        {
            Debug.Log(
                "HitungQuestionController ditemukan pada object: " +
                hitungQuestionController.gameObject.name
            );
        }
        else
        {
            Debug.LogError(
                "HitungQuestionController tidak ditemukan di Scene. " +
                "Pasang komponennya pada GlobalManager."
            );
        }
    }

    // ==================================================
    // MENCARI SUSUN QUESTION CONTROLLER
    // ==================================================

    private void ResolveSusunQuestionController()
    {
        if (susunQuestionController == null)
        {
            susunQuestionController =
                GetComponent<SusunQuestionController>();
        }

        if (susunQuestionController == null)
        {
            susunQuestionController =
                FindFirstObjectByType<SusunQuestionController>(
                    FindObjectsInactive.Include
                );
        }

        if (susunQuestionController != null)
        {
            Debug.Log(
                "SusunQuestionController ditemukan pada object: " +
                susunQuestionController.gameObject.name
            );
        }
        else
        {
            Debug.LogError(
                "SusunQuestionController tidak ditemukan di Scene. " +
                "Pasang komponennya pada GlobalManager."
            );
        }
    }

    // ==================================================
    // BUTTON ANGKA
    // ==================================================

    public void ClickAngkaHitung()
    {
        bool berhasil = OpenSubGame(
            "angka",
            "hitung_benda",
            angkaGroup,
            hitung,
            true
        );

        if (!berhasil)
        {
            return;
        }

        if (hitungQuestionController == null)
        {
            ResolveHitungQuestionController();
        }

        if (hitungQuestionController == null)
        {
            Debug.LogError(
                "Subgame Hitung Benda terbuka, tetapi " +
                "HitungQuestionController tidak ditemukan."
            );
            return;
        }

        Debug.Log(
            "Memanggil StartQuestion Hitung Benda."
        );

        hitungQuestionController.StartQuestion();
    }

    public void ClickAngkaSusun()
    {
        bool berhasil = OpenSubGame(
            "angka",
            "menyusun_angka",
            angkaGroup,
            susun,
            false
        );

        if (!berhasil)
        {
            return;
        }

        if (susunQuestionController == null)
        {
            ResolveSusunQuestionController();
        }

        if (susunQuestionController == null)
        {
            Debug.LogError(
                "Subgame Menyusun Angka terbuka, tetapi " +
                "SusunQuestionController tidak ditemukan."
            );
            return;
        }

        Debug.Log(
            "Memanggil StartQuestion Menyusun Angka."
        );

        susunQuestionController.StartQuestion();
    }

    public void ClickAngkaCari()
    {
        bool berhasil = OpenSubGame(
            "angka",
            "cari_angka",
            angkaGroup,
            cari,
            true
        );

        if (!berhasil)
        {
            return;
        }

        if (cariQuestionController == null)
        {
            ResolveCariQuestionController();
        }

        if (cariQuestionController == null)
        {
            Debug.LogError(
                "Subgame Cari Angka terbuka, tetapi " +
                "CariQuestionController tidak ditemukan."
            );
            return;
        }

        Debug.Log(
            "Memanggil StartQuestion Cari Angka."
        );

        cariQuestionController.StartQuestion();
    }

    // ==================================================
    // MENCARI DESKRIPSI QUESTION CONTROLLER
    // ==================================================

    private void ResolveDeskripsiQuestionController()
    {
        if (deskripsiQuestionController == null)
        {
            deskripsiQuestionController =
                GetComponent<DeskripsiQuestionController>();
        }

        if (deskripsiQuestionController == null)
        {
            deskripsiQuestionController =
                FindFirstObjectByType<DeskripsiQuestionController>(
                    FindObjectsInactive.Include
                );
        }

        if (deskripsiQuestionController != null)
        {
            Debug.Log(
                "DeskripsiQuestionController ditemukan pada object: " +
                deskripsiQuestionController.gameObject.name
            );
        }
        else
        {
            Debug.LogError(
                "DeskripsiQuestionController tidak ditemukan di Scene."
            );
        }
    }

    // ==================================================
    // MENCARI SUARA QUESTION CONTROLLER
    // ==================================================

    private void ResolveSuaraQuestionController()
    {
        if (suaraQuestionController == null)
        {
            suaraQuestionController =
                GetComponent<SuaraQuestionController>();
        }

        if (suaraQuestionController == null)
        {
            suaraQuestionController =
                FindFirstObjectByType<SuaraQuestionController>(
                    FindObjectsInactive.Include
                );
        }

        if (suaraQuestionController != null)
        {
            Debug.Log(
                "SuaraQuestionController ditemukan pada object: " +
                suaraQuestionController.gameObject.name
            );
        }
        else
        {
            Debug.LogError(
                "SuaraQuestionController tidak ditemukan di Scene."
            );
        }
    }

    private void ResolveBayanganQuestionController()
    {
        if (bayanganQuestionController == null)
        {
            bayanganQuestionController =
                FindFirstObjectByType<BayanganQuestionController>(
                    FindObjectsInactive.Include
                );
        }

        if (bayanganQuestionController == null)
        {
            Debug.LogError(
                "BayanganQuestionController tidak ditemukan di Scene."
            );
        }
    }

    // ==================================================
    // BUTTON HEWAN
    // ==================================================

    public void ClickHewanSuara()
    {
        bool berhasil = OpenSubGame(
            "hewan",
            "tebak_suara",
            hewanGroup,
            suara,
            false
        );

        if (!berhasil)
        {
            return;
        }

        if (suaraQuestionController == null)
        {
            ResolveSuaraQuestionController();
        }

        if (suaraQuestionController == null)
        {
            Debug.LogError(
                "Subgame Tebak Suara terbuka, tetapi " +
                "SuaraQuestionController tidak ditemukan."
            );
            return;
        }

        suaraQuestionController.StartQuestion();
    }

    public void ClickHewanBayangan()
    {
        bool berhasil = OpenSubGame(
            "hewan",
            "tebak_bayangan",
            hewanGroup,
            bayangan,
            false
        );

        if (!berhasil)
        {
            return;
        }

        if (bayanganQuestionController == null)
        {
            ResolveBayanganQuestionController();
        }

        if (bayanganQuestionController == null)
        {
            Debug.LogError(
                "Subgame Tebak Bayangan terbuka, tetapi " +
                "BayanganQuestionController tidak ditemukan."
            );
            return;
        }

        bayanganQuestionController.StartQuestion();
    }

    // Kompatibilitas untuk Button/UnityEvent pada scene lama.
    public void ClickHewanHabitat()
    {
        ClickHewanBayangan();
    }

    public void ClickHewanDeskripsi()
    {
        bool berhasil = OpenSubGame(
            "hewan",
            "tebak_deskripsi",
            hewanGroup,
            deskripsi,
            false
        );

        if (!berhasil)
        {
            return;
        }

        if (deskripsiQuestionController == null)
        {
            ResolveDeskripsiQuestionController();
        }

        if (deskripsiQuestionController == null)
        {
            Debug.LogError(
                "Subgame Tebak Deskripsi terbuka, tetapi " +
                "DeskripsiQuestionController tidak ditemukan."
            );
            return;
        }

        deskripsiQuestionController.StartQuestion();
    }

    // ==================================================
    // MENCARI TEBAK WARNA QUESTION CONTROLLER
    // ==================================================

    private void ResolveTebakQuestionController()
    {
        if (tebakQuestionController == null)
        {
            tebakQuestionController =
                GetComponent<TebakQuestionController>();
        }

        if (tebakQuestionController == null)
        {
            tebakQuestionController =
                FindFirstObjectByType<TebakQuestionController>(
                    FindObjectsInactive.Include
                );
        }

        if (tebakQuestionController != null)
        {
            Debug.Log(
                "TebakQuestionController ditemukan pada object: " +
                tebakQuestionController.gameObject.name
            );
        }
        else
        {
            Debug.LogError(
                "TebakQuestionController tidak ditemukan di Scene."
            );
        }
    }

    // ==================================================
    // BUTTON WARNA
    // ==================================================

    public void ClickWarnaTebak()
    {
        bool berhasil = OpenSubGame(
            "warna",
            "tebak_warna",
            warnaGroup,
            tebak,
            false
        );

        if (!berhasil)
        {
            return;
        }

        if (tebakQuestionController == null)
        {
            ResolveTebakQuestionController();
        }

        if (tebakQuestionController == null)
        {
            Debug.LogError(
                "Subgame Tebak Warna terbuka, tetapi " +
                "TebakQuestionController tidak ditemukan."
            );

            return;
        }

        tebakQuestionController.StartQuestion();
    }

    // ==================================================
    // RFID SUBGAME
    // K = 1, L = 2, M = 3
    // ==================================================

    public void SelectSubGame(int subGameNumber)
    {
        if (dashboardController == null)
        {
            Debug.LogWarning(
                "DashboardController belum diisi."
            );
            return;
        }

        string category = NormalizeText(
            dashboardController.GetSelectedCategory()
        );

        if (string.IsNullOrEmpty(category))
        {
            Debug.LogWarning(
                "Kategori belum dipilih dari DashboardPanel."
            );
            return;
        }

        switch (category)
        {
            case "huruf":

                if (subGameNumber == 1)
                {
                    ClickHurufBunyi();
                }
                else if (subGameNumber == 2)
                {
                    ClickHurufBerburu();
                }
                else if (subGameNumber == 3)
                {
                    ClickHurufLengkapi();
                }
                else
                {
                    Debug.LogWarning(
                        "Subgame Huruf tidak tersedia: " +
                        subGameNumber
                    );
                }

                break;

            case "angka":

                if (subGameNumber == 1)
                {
                    ClickAngkaHitung();
                }
                else if (subGameNumber == 2)
                {
                    ClickAngkaSusun();
                }
                else if (subGameNumber == 3)
                {
                    ClickAngkaCari();
                }
                else
                {
                    Debug.LogWarning(
                        "Subgame Angka tidak tersedia: " +
                        subGameNumber
                    );
                }

                break;

            case "hewan":

                if (subGameNumber == 1)
                {
                    ClickHewanSuara();
                }
                else if (subGameNumber == 2)
                {
                    ClickHewanBayangan();
                }
                else if (subGameNumber == 3)
                {
                    ClickHewanDeskripsi();
                }
                else
                {
                    Debug.LogWarning(
                        "Subgame Hewan tidak tersedia: " +
                        subGameNumber
                    );
                }

                break;

            case "warna":

                if (subGameNumber == 1)
                {
                    ClickWarnaTebak();
                }
                else
                {
                    Debug.LogWarning(
                        "Kategori Warna sementara hanya " +
                        "memiliki satu subgame."
                    );
                }

                break;

            default:

                Debug.LogWarning(
                    "Kategori tidak dikenali: " +
                    category
                );

                break;
        }
    }

    public void SelectSubGameByString(string subGameCode)
    {
        Debug.Log("Memilih Subgame by String: " + subGameCode);
        
        switch (subGameCode.ToUpper())
        {
            case "HURUF-TEBAK":
                ClickHurufBunyi();
                break;
            case "HURUF-COCOK":
                ClickHurufBerburu();
                break;
            case "HURUF-SUSUN":
                ClickHurufLengkapi();
                break;
            case "ANGKA-HITUNG":
                ClickAngkaHitung();
                break;
            case "ANGKA-URUT":
                ClickAngkaSusun();
                break;
            case "ANGKA-TEBAK":
                ClickAngkaCari();
                break;
            case "HEWAN-TEBAK":
                ClickHewanSuara();
                break;
            case "HEWAN-HABITAT":
                ClickHewanBayangan();
                break;
            case "WARNA-TEBAK":
                ClickWarnaTebak();
                break;
            default:
                Debug.LogWarning("Kode Subgame tidak dikenali: " + subGameCode);
                break;
        }
    }

    // ==================================================
    // MEMBUKA SUBGAME
    // ==================================================

    private bool OpenSubGame(
        string category,
        string subGame,
        GameObject categoryGroup,
        GameObject subGameObject,
        bool playAudio
    )
    {
        Debug.Log(
            "Mencoba membuka subgame: " +
            category + " / " + subGame
        );

        if (uiManager == null)
        {
            Debug.LogError(
                "[MiniGame] UIManager belum diisi dan tidak ditemukan pada " +
                "CategoryToMiniGameImageController."
            );
            return false;
        }

        if (categoryGroup == null)
        {
            Debug.LogError(
                "[MiniGame] Parent kategori belum diisi pada Inspector: " +
                category
            );
            return false;
        }

        if (subGameObject == null)
        {
            Debug.LogError(
                "[MiniGame] Object subgame belum diisi pada Inspector: " +
                subGame
            );
            return false;
        }

        // Bypass kunci subgame bila offline mode aktif
        bool isOffline = DonimalAPI.Instance != null && DonimalAPI.Instance.offlineMode;
        if (!isOffline && AchievementManager.Instance != null &&
            !AchievementManager.Instance.CanPlaySubGame(category, subGame))
        {
            string requiredSubGame =
                AchievementManager.Instance.GetRequiredPreviousSubGame(category, subGame);

            Debug.LogWarning(
                "[MiniGame] Subgame masih terkunci: " + category + " / " + subGame +
                ". Selesaikan " + requiredSubGame +
                " minimal 1 bintang terlebih dahulu."
            );
            return false;
        }

        if (DonimalAPI.Instance != null && !DonimalAPI.Instance.CanStartActivity())
        {
            Debug.LogWarning("[MiniGame] Diblokir Require Child RFID: kartu anak belum tersimpan.");
            return false;
        }

        selectedCategory = NormalizeText(category);
        selectedSubGame = NormalizeText(subGame);
        currentCorrectAnswer = "";

        bool resumeCariAngka = selectedSubGame == "cari_angka" &&
            cariQuestionController != null &&
            cariQuestionController.HasResumableSession();

        if (!resumeCariAngka)
        {
            DonimalAPI.Instance?.BeginActivity(selectedCategory, selectedSubGame);
        }

        // Statistik pada RewardPanel berlaku untuk satu sesi subgame.
        // Pergantian soal di dalam subgame tidak melewati method ini,
        // sehingga jumlah benar/salah tetap terakumulasi sampai soal habis.
        if (!resumeCariAngka)
        {
            ScoreManager.Instance?.ResetScore();
        }

        // Aktifkan MiniGamePanel dahulu.
        uiManager.ShowMiniGamePanel();

        // Jangan menghentikan soal Cari Angka saat membuka kembali sesi yang dipause.
        if (!resumeCariAngka)
        {
            HideAllSoal();
        }

        // Aktifkan kategori dan subgame terpilih.
        categoryGroup.SetActive(true);
        subGameObject.SetActive(true);

        if (resumeCariAngka)
        {
            cariQuestionController.ResumeQuestion();
        }

        // Berburu Huruf untuk sementara tidak memakai audio.
        if (
            playAudio &&
            playDefaultAudioOnOpen &&
            audioManager != null &&
            selectedSubGame != "berburu_huruf"
        )
        {
            audioManager.PlaySubGameStartAudio(
                selectedCategory,
                selectedSubGame
            );
        }

        Debug.Log("Berhasil masuk MiniGamePanel.");

        Debug.Log(
            "Kategori MiniGame aktif: " +
            selectedCategory
        );

        Debug.Log(
            "Subgame MiniGame aktif: " +
            selectedSubGame
        );

        Debug.Log(
            "Parent kategori aktif: " +
            categoryGroup.activeInHierarchy
        );

        Debug.Log(
            "Object subgame aktif: " +
            subGameObject.activeInHierarchy
        );

        return true;
    }

    // ==================================================
    // SEMBUNYIKAN SEMUA SUBGAME
    // ==================================================

    public void HideAllSoal()
    {
        bunyiQuestionController?.StopQuestion();
        // Hentikan proses Berburu Huruf ketika keluar
        // atau sebelum membuka subgame lain.
        if (
            berburuQuestionController != null
        )
        {
            berburuQuestionController.StopQuestion();
        }

        // Hentikan Melengkapi Huruf ketika keluar
        // atau sebelum membuka subgame lain.
        if (
            lengkapiQuestionController != null
        )
        {
            lengkapiQuestionController.StopQuestion();
        }

        // Huruf
        SetObjectActive(bunyi, false);
        SetObjectActive(berburu, false);
        SetObjectActive(lengkapi, false);

        // Hentikan Cari Angka ketika keluar
        // atau sebelum membuka subgame lain.
        if (
            cariQuestionController != null
        )
        {
            cariQuestionController.StopQuestion();
        }

        // Hentikan Hitung Benda ketika keluar
        // atau sebelum membuka subgame lain.
        if (
            hitungQuestionController != null
        )
        {
            hitungQuestionController.StopQuestion();
        }

        // Hentikan Menyusun Angka ketika keluar
        // atau sebelum membuka subgame lain.
        if (
            susunQuestionController != null
        )
        {
            susunQuestionController.StopQuestion();
        }

        // Angka
        SetObjectActive(hitung, false);
        SetObjectActive(susun, false);
        SetObjectActive(cari, false);

        if (
            deskripsiQuestionController != null
        )
        {
            deskripsiQuestionController.StopQuestion();
        }

        if (
            suaraQuestionController != null
        )
        {
            suaraQuestionController.StopQuestion();
        }

        if (
            bayanganQuestionController != null
        )
        {
            bayanganQuestionController.StopQuestion();
        }

        // Hewan
        SetObjectActive(suara, false);
        SetObjectActive(bayangan, false);
        SetObjectActive(deskripsi, false);

        if (
            tebakQuestionController != null
        )
        {
            tebakQuestionController.StopQuestion();
        }

        // Warna
        SetObjectActive(tebak, false);

        // Parent kategori
        SetObjectActive(hurufGroup, false);
        SetObjectActive(angkaGroup, false);
        SetObjectActive(hewanGroup, false);
        SetObjectActive(warnaGroup, false);
    }

    private void SetObjectActive(
        GameObject targetObject,
        bool active
    )
    {
        if (targetObject != null)
        {
            targetObject.SetActive(active);
        }
    }

    // ==================================================
    // NORMALISASI
    // ==================================================

    private string NormalizeText(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return "";
        }

        return value
            .ToLower()
            .Trim()
            .Replace(" ", "");
    }

    // ==================================================
    // JAWABAN SOAL AKTIF
    // ==================================================

    public void SetCurrentCorrectAnswer(
        string correctAnswer
    )
    {
        currentCorrectAnswer =
            NormalizeText(correctAnswer);

        Debug.Log(
            "Jawaban benar soal aktif diubah menjadi: " +
            currentCorrectAnswer
        );
    }

    public string GetCurrentCorrectAnswer()
    {
        return currentCorrectAnswer;
    }

    public string GetSelectedCategory()
    {
        return selectedCategory;
    }

    public string GetSelectedSubGame()
    {
        return selectedSubGame;
    }

    public bool TryShowNextQuestionInSelectedSubGame()
    {
        // FeedbackCharacterPanel menonaktifkan MiniGamePanel beserta seluruh
        // controller di bawahnya. Aktifkan kembali sebelum controller mencoba
        // StartCoroutine untuk soal berikutnya.
        if (uiManager != null)
        {
            uiManager.ShowMiniGamePanel();
        }

        switch (NormalizeText(selectedSubGame))
        {
            case "tebak_bunyi":
                return bunyiQuestionController != null &&
                       bunyiQuestionController.TryShowNextServerQuestion();
            case "berburu_huruf":
                return berburuQuestionController != null &&
                       berburuQuestionController.TryShowNextServerQuestion();
            case "lengkapi_huruf":
                return lengkapiQuestionController != null &&
                       lengkapiQuestionController.TryShowNextServerQuestion();
            case "hitung_benda":
                return hitungQuestionController != null &&
                       hitungQuestionController.TryShowNextServerQuestion();
            case "menyusun_angka":
                return susunQuestionController != null &&
                       susunQuestionController.TryShowNextServerQuestion();
            case "cari_angka":
                return cariQuestionController != null &&
                       cariQuestionController.TryShowNextServerQuestion();
            case "tebak_suara":
                return suaraQuestionController != null &&
                       suaraQuestionController.TryShowNextServerQuestion();
            case "tebak_bayangan":
                return bayanganQuestionController != null &&
                       bayanganQuestionController.TryShowNextServerQuestion();
            case "tebak_deskripsi":
                return deskripsiQuestionController != null &&
                       deskripsiQuestionController.TryShowNextServerQuestion();
            case "tebak_warna":
                return tebakQuestionController != null &&
                       tebakQuestionController.TryShowNextServerQuestion();
            default:
                return false;
        }
    }
}
