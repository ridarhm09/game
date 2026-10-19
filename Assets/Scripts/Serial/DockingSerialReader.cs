using System;
using System.Collections.Generic;
using System.IO.Ports;
using UnityEngine;

public class DockingSerialReader : MonoBehaviour
{
    [Serializable]
    private class ServerRfidCard
    {
        public string uid;
        public string category;
        public string code;
    }

    public static DockingSerialReader Instance { get; private set; }

    private static readonly Dictionary<string, string> ChildCards =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "75:f3:f6:04", "Ari" },
            { "35:d1:c9:06", "Azka" }
        };

    private static readonly Dictionary<string, string> CharacterCards =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "3a:ca:bb:ba", "kelinci" },
            { "1a:ca:bb:ba", "elang" },
            { "2a:ca:bb:ba", "penyu" },
            { "04:43:4c:b0:36:02:89", "kelinci" }, // UID baru
            { "04:23:bf:bf:36:02:89", "elang" }    // UID baru
        };

    // Kartu khusus navigasi, diprioritaskan atas kategori dari docking.
    private static readonly Dictionary<string, string> NavigationCards =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "d8:58:61:f5", "back" },
            { "a8:fc:40:f5", "3" },
            { "66:71:c4:d1", "2" },
            { "76:6c:f3:d1", "1" },
            { "66:87:ae:d1", "belajar" },
            { "66:42:d7:d1", "warna" },
            { "36:6d:78:d1", "hewan" },
            { "76:aa:54:d1", "angka" },
            { "76:58:5c:d1", "huruf" }
        };

    // Pemetaan kartu angka yang sudah dikonfirmasi, untuk data docking unknown.
    private static readonly Dictionary<string, string> FallbackNumberCards =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "0b:96:76:06", "0" },
            { "73:b7:b6:2e", "1" },
            { "93:69:8e:34", "3" },
            { "73:32:c3:2e", "5" },
            { "e3:81:77:06", "6" },
            { "e9:04:73:06", "9" }
        };


    // Urutan UID A-Z sesuai hasil tap yang dikonfirmasi pengguna.
    private static readonly Dictionary<string, string> FallbackLetterCards =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "f3:9c:d8:41", "a" },
            { "93:8c:89:41", "b" },
            { "c3:e4:99:3f", "c" },
            { "c3:3c:5f:41", "d" },
            { "c3:f7:0e:3f", "e" },
            { "13:dc:8f:3f", "f" },
            { "13:a9:6f:3f", "g" },
            { "53:5b:74:3f", "h" },
            { "b3:9e:97:41", "j" },
            { "d3:2e:ef:40", "k" },
            { "03:e4:ae:40", "l" },
            { "03:fb:a7:40", "m" },
            { "d3:4e:10:3f", "n" },
            { "03:b9:c4:40", "o" },
            { "d6:86:ce:06", "p" },
            { "97:f5:cf:06", "q" },
            { "44:2e:cf:06", "r" },
            { "a6:77:ca:06", "w" },
            { "2a:e5:ca:06", "x" },
            { "4c:b6:ca:06", "y" },
            { "56:22:d3:06", "v" },
            { "b1:fa:cc:06", "u" }
        };

    // Urutan UID kartu hewan sesuai hasil tap pengguna.
    private static readonly Dictionary<string, string> FallbackAnimalCards =
        new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "f3:ee:42:3f", "bebek" },
            { "63:f9:13:3f", "kucing" },
            { "a3:3d:e5:3e", "kambing" },
            { "03:87:f8:3e", "anjing" },
            { "a3:a5:cd:06", "monyet" },
            { "32:fc:74:06", "katak" },
            { "29:5b:cc:06", "sapi" },
            { "07:ce:74:06", "babi" },
            { "ea:bf:74:06", "jangkrik" }
        };

    [Header("Serial Settings")]
    [Tooltip("Port pilihan utama. Kosongkan untuk langsung mencari semua port yang tersedia.")]
    public string portName = "COM8";
    public bool autoDetectPort = true;
    [Min(1f)]
    public float reconnectInterval = 3f;
    public int baudRate = 115200;

    [Header("LED Feedback")]
    public bool enableLedFeedback = true;
    [Tooltip("Perintah satu baris yang harus didukung firmware ESP32 untuk LED hijau.")]
    public string correctLedCommand = "LED|hijau";
    [Tooltip("Perintah satu baris yang harus didukung firmware ESP32 untuk LED merah.")]
    public string wrongLedCommand = "LED|merah";

    [Header("Controllers")]
    public UIManagerDonimal uiManager;
    public ScanRFIDController scanRFIDController;
    public DashboardController dashboardController;
    public CategoryToMiniGameImageController categoryToMiniGameController;
    public MiniGameAnswerController miniGameAnswerController;
    public BelajarPanelController belajarPanelController;

    private SerialPort serialPort;
    private bool isConnected = false;

    private float startReadDelay = 2f;
    private float timer = 0f;
    private float reconnectTimer = 0f;

    [Header("Docking Status")]
    public bool isDockingEnabled = true;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }
        Instance = this;
        isDockingEnabled = PlayerPrefs.GetInt("UseDocking", 1) == 1;
    }

    private void Start()
    {
        ResolveLearningController();
    }

    private void OnEnable()
    {
        if (Instance != this) return;
        // Jangan coba serial koneksi kalau offline mode aktif
        if (DonimalAPI.Instance != null && DonimalAPI.Instance.offlineMode) return;
        if (isDockingEnabled)
        {
            ConnectToDocking();
        }
    }

    private void OnDisable()
    {
        CloseSerial();
    }

    [ContextMenu("Reconnect Docking")]
    public void ReconnectDocking()
    {
        if (!Application.isPlaying || !isActiveAndEnabled || Instance != this) return;
        CloseSerial();
        if (isDockingEnabled)
        {
            ConnectToDocking();
        }
    }

    public void SetDockingEnabled(bool enable)
    {
        if (Instance != this) return;
        isDockingEnabled = enable;
        PlayerPrefs.SetInt("UseDocking", enable ? 1 : 0);
        PlayerPrefs.Save();

        if (enable)
        {
            ConnectToDocking();
        }
        else
        {
            CloseSerial();
        }
        Debug.Log("[Docking] Status docking diubah menjadi: " + (enable ? "ON" : "OFF"));
    }

    private void ResolveLearningController()
    {
        // Referensi scene dapat kosong atau masih menunjuk controller panel lama.
        if (belajarPanelController != null && belajarPanelController.isActiveAndEnabled &&
            belajarPanelController.belajarPanel != null &&
            belajarPanelController.belajarPanel.activeInHierarchy) return;

        foreach (BelajarPanelController candidate in FindObjectsByType<BelajarPanelController>(
            FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (candidate.gameObject.scene != gameObject.scene) continue;
            if (belajarPanelController == null) belajarPanelController = candidate;
            if (!candidate.isActiveAndEnabled || candidate.belajarPanel == null ||
                !candidate.belajarPanel.activeInHierarchy) continue;
            belajarPanelController = candidate;
            return;
        }
    }

    private bool TrySendLearningCard(string category, string value)
    {
        category = category.Trim().ToLowerInvariant();
        value = value.Trim().ToLowerInvariant();
        if (category != "huruf" && category != "angka" && category != "warna" && category != "hewan")
            return false;

        ResolveLearningController();
        if (belajarPanelController == null)
        {
            Debug.LogWarning("[Docking] BelajarPanelController belum ditemukan untuk kartu " + category + ":" + value);
            return false;
        }
        if (!belajarPanelController.isActiveAndEnabled || belajarPanelController.belajarPanel == null ||
            !belajarPanelController.belajarPanel.activeInHierarchy) return false;

        Debug.Log("[Docking -> Belajar] " + category + ":" + value);
        belajarPanelController.ShowLearningCard(category, value);
        return true;
    }

    private void Update()
    {
        if (!isDockingEnabled) return;

        // Jangan reconnect jika offline mode aktif
        if (DonimalAPI.Instance != null && DonimalAPI.Instance.offlineMode) return;

        if (!isConnected || serialPort == null || !serialPort.IsOpen)
        {
            if (autoDetectPort)
            {
                reconnectTimer += Time.deltaTime;
                if (reconnectTimer >= reconnectInterval)
                {
                    reconnectTimer = 0f;
                    ConnectToDocking();
                }
            }
            return;
        }

        timer += Time.deltaTime;

        if (timer < startReadDelay)
        {
            return;
        }

        try
        {
            while (serialPort.BytesToRead > 0)
            {
                string data = serialPort.ReadLine();
                data = data.Trim();

                if (data != "")
                {
                    Debug.Log("Data dari docking: " + data);
                    ProcessDockingData(data);
                }
            }
        }
        catch (TimeoutException)
        {
            // Normal, tidak perlu aksi
        }
        catch (Exception e)
        {
            Debug.LogWarning("Gagal membaca serial: " + e.Message);
        }
    }

    private void ConnectToDocking()
    {
        if (isConnected && serialPort != null && serialPort.IsOpen) return;
        CloseSerial();

        List<string> candidatePorts = new List<string>();
        if (!string.IsNullOrWhiteSpace(portName))
        {
            candidatePorts.Add(portName.Trim());
        }

        if (autoDetectPort)
        {
            foreach (string detectedPort in SerialPort.GetPortNames())
            {
                if (!candidatePorts.Contains(detectedPort))
                {
                    candidatePorts.Add(detectedPort);
                }
            }
        }

        if (candidatePorts.Count == 0)
        {
            Debug.LogWarning("[Docking] Belum ada port serial yang tersedia.");
            return;
        }

        foreach (string candidatePort in candidatePorts)
        {
            try
            {
                serialPort = new SerialPort(candidatePort, baudRate);
                serialPort.ReadTimeout = 50;
                serialPort.NewLine = "\n";
                serialPort.Open();

                isConnected = true;
                timer = 0f;
                reconnectTimer = 0f;
                portName = candidatePort;

                Debug.Log("Docking terhubung otomatis di " + candidatePort);
                return;
            }
            catch (Exception)
            {
                CloseSerial();
            }
        }

        Debug.LogWarning("[Docking] Tidak ada port yang berhasil dibuka. Akan mencoba lagi.");
    }

    private void ProcessDockingData(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return;
        data = data.Trim();
        if (data.StartsWith("{") || data.StartsWith("["))
        {
            if (data.StartsWith("{") && HandleServerAnswerStatus(data)) return;
            ReceiveServerRfidJson(data);
            return;
        }
        data = data.ToLowerInvariant();
        /*
         * Format yang dibaca:
         * tap|1|boneka|kelinci|uid
         * tap|1|huruf|n|uid
         * tap|1|huruf|o|uid
         * tap|1|huruf|k|uid
         * huruf:n
         * huruf:o
         * huruf:k
         * mengirim ke server: {"answer_status":"benar", ...}
         */

        if (data.StartsWith("reader"))
        {
            return;
        }

        if (data.StartsWith("response server"))
        {
            return;
        }

        if (data.Contains("anak") && data.Contains("memainkan materi"))
        {
            return;
        }

        if (HandleServerAnswerStatus(data))
        {
            return;
        }

        if (data.StartsWith("tap|"))
        {
            ProcessTapFormat(data);
            return;
        }

        ProcessSimpleFormat(data);
    }

    private bool HandleServerAnswerStatus(string data)
    {
        if (!data.Contains("answer_status"))
        {
            return false;
        }

        if (data.Contains("\"answer_status\":\"benar\"") || data.Contains("\"answer_status\": \"benar\""))
        {
            Debug.Log("Server membaca jawaban: BENAR");

            if (miniGameAnswerController != null)
            {
                miniGameAnswerController.ShowCorrectFeedbackFromServer();
            }
            else
            {
                Debug.LogWarning("MiniGameAnswerController belum diisi di DockingSerialReader.");
            }

            return true;
        }

        if (data.Contains("\"answer_status\":\"salah\"") || data.Contains("\"answer_status\": \"salah\""))
        {
            Debug.Log("Server membaca jawaban: SALAH");

            if (miniGameAnswerController != null)
            {
                miniGameAnswerController.ShowWrongFeedbackFromServer();
            }
            else
            {
                Debug.LogWarning("MiniGameAnswerController belum diisi di DockingSerialReader.");
            }

            return true;
        }

        return false;
    }

    // Panggil dari transport server pada main thread Unity. Satu object = satu tap.
    public void ReceiveServerRfidJson(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) return;
        if (json.TrimStart().StartsWith("["))
        {
            Debug.LogWarning("[Docking] Array RFID adalah katalog, bukan event scan. Kirim satu object kartu yang baru ditap.");
            return;
        }

        ServerRfidCard card;
        try
        {
            card = JsonUtility.FromJson<ServerRfidCard>(json);
        }
        catch (ArgumentException exception)
        {
            Debug.LogWarning("[Docking] JSON RFID tidak valid: " + exception.Message);
            return;
        }

        if (card == null || string.IsNullOrWhiteSpace(card.uid) ||
            string.IsNullOrWhiteSpace(card.category) || string.IsNullOrWhiteSpace(card.code))
        {
            Debug.LogWarning("[Docking] JSON RFID harus berisi uid, category, dan code.");
            return;
        }

        // Kategori/nilai dari server adalah sumber utama. Jangan timpa dengan tabel UID lokal.
        ProcessCard("server", card.category, card.code, card.uid.Trim(), false);
    }

    private static void NormalizeCard(ref string category, ref string value)
    {
        category = category.Trim().ToLowerInvariant();
        value = value.Trim().ToLowerInvariant();
        string sourceCategory = category;
        if (category == "animal") category = "hewan";
        else if (category == "character") category = "karakter";
        else if (category == "student") category = "anak";

        if (category == "huruf" || category == "angka" || category == "warna" ||
            category == "hewan" || category == "karakter")
        {
            foreach (string prefix in new[] { sourceCategory + " ", sourceCategory + ":", category + " ", category + ":" })
            {
                if (!value.StartsWith(prefix, StringComparison.Ordinal)) continue;
                value = value.Substring(prefix.Length).Trim();
                break;
            }
        }

        if (category == "karakter")
        {
            if (value == "kira") value = "kelinci";
            else if (value == "eyra") value = "elang";
            else if (value == "pasa") value = "penyu";
        }
        value = RfidAnswerUtility.Normalize(category, value);
    }

    private void ProcessTapFormat(string data)
    {
        string[] parts = data.Split('|');

        if (parts.Length < 5)
        {
            Debug.LogWarning("Format tap tidak lengkap: " + data);
            return;
        }

        string readerId = parts[1].Trim();
        string category = parts[2].Trim();
        string value = parts[3].Trim();
        string uid = parts[4].Trim();

        ProcessCard(readerId, category, value, uid, true);
    }

    private void ProcessCard(string readerId, string category, string value, string uid, bool useLocalMapping)
    {
        NormalizeCard(ref category, ref value);

        if (useLocalMapping && HandleNavigationCard(uid)) return;

        if (useLocalMapping && string.Equals(category, "unknown", StringComparison.OrdinalIgnoreCase) &&
            FallbackNumberCards.TryGetValue(uid, out string fallbackNumber))
        {
            category = "angka";
            value = fallbackNumber;
            Debug.Log("[Docking] Pemetaan cadangan UID " + uid + " -> angka:" + value);
        }
        else if (useLocalMapping && string.Equals(category, "unknown", StringComparison.OrdinalIgnoreCase) &&
            FallbackLetterCards.TryGetValue(uid, out string fallbackLetter))
        {
            category = "huruf";
            value = fallbackLetter;
            Debug.Log("[Docking] Pemetaan cadangan UID " + uid + " -> huruf:" + value);
        }
        else if (useLocalMapping && string.Equals(category, "unknown", StringComparison.OrdinalIgnoreCase) &&
            FallbackAnimalCards.TryGetValue(uid, out string fallbackAnimal))
        {
            category = "hewan";
            value = fallbackAnimal;
            Debug.Log("[Docking] Pemetaan cadangan UID " + uid + " -> hewan:" + value);
        }
        else if (useLocalMapping && string.Equals(category, "unknown", StringComparison.OrdinalIgnoreCase) &&
            CharacterCards.TryGetValue(uid, out string character))
        {
            category = "karakter";
            value = character;
            Debug.Log("[Docking] Pemetaan UID karakter " + uid + " -> karakter:" + value);
        }

        Debug.Log("Reader: " + readerId);
        Debug.Log("Kategori RFID: " + category);
        Debug.Log("Nilai RFID: " + value);
        Debug.Log("UID RFID: " + uid);

        if (HandleChildRfid(category, value, uid, useLocalMapping))
        {
            return;
        }

        if (category == "unknown")
        {
            Debug.Log("[Docking] Kategori kartu dari docking masih unknown. " +
                "Reader: " + readerId + " | UID: " + uid + " | Nilai: " + value +
                " | Periksa pemetaan UID ke kategori/nilai pada docking atau server.");
            return;
        }

        // Saat panel Belajar aktif, kartu materi tidak diproses sebagai menu/jawaban game.
        if (TrySendLearningCard(category, value)) return;

        // =========================
        // BONEKA KARAKTER
        // tap|1|boneka|kelinci|uid
        // tap|1|boneka|elang|uid
        // tap|1|boneka|penyu|uid
        // =========================
        if (category == "boneka" || category == "karakter")
        {
            if (value == "kelinci" || value == "elang" || value == "penyu")
            {
                if (scanRFIDController != null)
                {
                    scanRFIDController.CheckRFIDString(value, uid);
                }
                else
                {
                    Debug.LogWarning("ScanRFIDController belum diisi.");
                }

                return;
            }
        }

        // =========================
        // KATEGORI LANGSUNG
        // tap|1|kategori|huruf|uid
        // =========================
        if (category == "kategori")
        {
            if (value == "huruf" || value == "angka" || value == "hewan" || value == "warna")
            {
                if (dashboardController != null)
                {
                    dashboardController.SelectCategory(value);
                }
                else
                {
                    Debug.LogWarning("DashboardController belum diisi.");
                }

                return;
            }
        }

        // =========================
        // SUBGAME LANGSUNG
        // tap|1|subgame|1|uid ATAU tap|1|subgame|HURUF-TEBAK|uid
        // =========================
        if (category == "subgame")
        {
            if (categoryToMiniGameController != null)
            {
                if (int.TryParse(value, out int subGameNumber))
                {
                    categoryToMiniGameController.SelectSubGame(subGameNumber);
                }
                else
                {
                    categoryToMiniGameController.SelectSubGameByString(value);
                }
            }
            else
            {
                Debug.LogWarning("CategoryToMiniGameController belum diisi.");
            }

            return;
        }

        // =========================
        // KARTU HURUF
        // Dashboard: N O P Q R
        // Category: K L M
        // Belajar/Jawaban: A B C
        // =========================
        if (category == "huruf")
        {
            if (HandleDashboardRFID(value))
            {
                return;
            }

            if (HandleCategoryRFID(value))
            {
                return;
            }

            if (belajarPanelController != null)
            {
                belajarPanelController.ShowLearningCard(category, value);
            }

            string answerData = "huruf:" + value;

            Debug.Log("Jawaban dikirim ke MiniGameAnswerController: " + answerData);

            if (miniGameAnswerController != null)
            {
                miniGameAnswerController.SubmitDockingAnswer(answerData);
            }

            return;
        }

        // =========================
        // KARTU ANGKA
        // tap|1|angka|1|uid
        // =========================
        if (category == "angka")
        {
            if (belajarPanelController != null)
            {
                belajarPanelController.ShowLearningCard(category, value);
            }

            string answerData = "angka:" + value;

            Debug.Log("Jawaban dikirim ke MiniGameAnswerController: " + answerData);

            if (miniGameAnswerController != null)
            {
                miniGameAnswerController.SubmitDockingAnswer(answerData);
            }

            return;
        }

        // =========================
        // KARTU WARNA
        // tap|1|warna|merah|uid
        // =========================
        if (category == "warna")
        {
            if (belajarPanelController != null)
            {
                belajarPanelController.ShowLearningCard(category, value);
            }

            string answerData = "warna:" + value;

            Debug.Log("Kartu warna terbaca: " + answerData);
            miniGameAnswerController?.SubmitDockingAnswer(answerData);

            return;
        }

        // =========================
        // KARTU HEWAN
        // tap|1|hewan|kucing|uid
        // =========================
        if (category == "hewan")
        {
            belajarPanelController?.ShowLearningCard(category, value);
            string answerData = "hewan:" + value;

            Debug.Log("Kartu hewan terbaca: " + answerData);
            miniGameAnswerController?.SubmitDockingAnswer(answerData);

            return;
        }

        Debug.LogWarning("Kategori tap belum dikenali: " + category);
    }

    private bool HandleChildRfid(string category, string value, string uid, bool useLocalMapping = true)
    {
        string childName = null;
        bool isKnownChild = !string.IsNullOrWhiteSpace(uid) &&
            ChildCards.TryGetValue(uid.Trim(), out childName);
        bool isChildCard =
            (useLocalMapping && isKnownChild) ||
            string.Equals(category, "anak", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(category, "child", StringComparison.OrdinalIgnoreCase) ||
            ((string.Equals(category, "unknown", StringComparison.OrdinalIgnoreCase)) &&
             (string.Equals(value, "anak", StringComparison.OrdinalIgnoreCase) ||
              string.Equals(value, "child", StringComparison.OrdinalIgnoreCase)));

        if (!isChildCard)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(uid))
        {
            Debug.LogWarning("[Docking] Kartu anak terbaca tanpa UID.");
            return true;
        }

        if (DonimalAPI.Instance == null)
        {
            Debug.LogWarning("[Docking] DonimalAPI belum tersedia untuk menyimpan RFID anak.");
            return true;
        }

        DonimalAPI.Instance.SetChildRfid(uid);
        Debug.Log("[Docking] RFID anak tersimpan otomatis: " + uid +
            (isKnownChild ? " | Nama: " + childName : ""));

        if (uiManager != null && uiManager.splashPanel != null &&
            uiManager.splashPanel.activeInHierarchy)
        {
            if (scanRFIDController != null)
            {
                scanRFIDController.StartScanCharacter();
            }
            else
            {
                uiManager.ShowScanRFIDPanel();
            }

            Debug.Log("[Docking] RFID anak diterima di SplashScreen. Langsung masuk ke ScanRFIDPanel.");
        }

        return true;
    }

    // =========================================================
    // DASHBOARD RFID
    // N → BelajarPanel
    // O → Huruf
    // P → Angka
    // Q → Hewan
    // R → Warna
    // =========================================================
    private bool HandleDashboardRFID(string value)
    {
        value = value.ToLower().Trim();

        bool isDashboardMenu =
            value == "n" ||
            value == "o" ||
            value == "p" ||
            value == "q" ||
            value == "r";

        if (!isDashboardMenu)
        {
            return false;
        }

        if (uiManager == null)
        {
            Debug.LogWarning("UIManager belum diisi di DockingSerialReader.");
            return false;
        }

        if (uiManager.dashboardPanel == null)
        {
            Debug.LogWarning("DashboardPanel belum diisi di UIManagerDonimal.");
            return false;
        }

        if (!uiManager.dashboardPanel.activeSelf)
        {
            Debug.Log("RFID Dashboard terbaca, tapi DashboardPanel sedang tidak aktif. Nilai: " + value);
            return false;
        }

        if (value == "n")
        {
            uiManager.ShowBelajarPanel();
            Debug.Log("RFID Dashboard N: masuk BelajarPanel.");
            return true;
        }

        if (value == "o")
        {
            if (dashboardController != null)
            {
                dashboardController.SelectCategory("huruf");
            }
            else
            {
                Debug.LogWarning("DashboardController belum diisi.");
            }

            Debug.Log("RFID Dashboard O: pilih kategori Huruf.");
            return true;
        }

        if (value == "p")
        {
            if (dashboardController != null)
            {
                dashboardController.SelectCategory("angka");
            }
            else
            {
                Debug.LogWarning("DashboardController belum diisi.");
            }

            Debug.Log("RFID Dashboard P: pilih kategori Angka.");
            return true;
        }

        if (value == "q")
        {
            if (dashboardController != null)
            {
                dashboardController.SelectCategory("hewan");
            }
            else
            {
                Debug.LogWarning("DashboardController belum diisi.");
            }

            Debug.Log("RFID Dashboard Q: pilih kategori Hewan.");
            return true;
        }

        if (value == "r")
        {
            if (dashboardController != null)
            {
                dashboardController.SelectCategory("warna");
            }
            else
            {
                Debug.LogWarning("DashboardController belum diisi.");
            }

            Debug.Log("RFID Dashboard R: pilih kategori Warna.");
            return true;
        }

        return false;
    }

    // =========================================================
    // CATEGORY RFID
    // K → Subgame 1
    // L → Subgame 2
    // M → Subgame 3
    // =========================================================
    private bool HandleCategoryRFID(string value)
    {
        value = value.ToLower().Trim();

        bool isCategoryMenu =
            value == "k" ||
            value == "l" ||
            value == "m";

        if (!isCategoryMenu)
        {
            return false;
        }

        if (uiManager == null)
        {
            Debug.LogWarning("UIManager belum diisi di DockingSerialReader.");
            return false;
        }

        if (uiManager.categoryPanel == null)
        {
            Debug.LogWarning("CategoryPanel belum diisi di UIManagerDonimal.");
            return false;
        }

        if (!uiManager.categoryPanel.activeSelf)
        {
            Debug.Log("RFID Category terbaca, tapi CategoryPanel sedang tidak aktif. Nilai: " + value);
            return false;
        }

        if (value == "k")
        {
            if (categoryToMiniGameController != null)
            {
                categoryToMiniGameController.SelectSubGame(1);
            }
            else
            {
                Debug.LogWarning("CategoryToMiniGameController belum diisi.");
            }

            Debug.Log("RFID Category K: pilih subgame 1.");
            return true;
        }

        if (value == "l")
        {
            if (categoryToMiniGameController != null)
            {
                categoryToMiniGameController.SelectSubGame(2);
            }
            else
            {
                Debug.LogWarning("CategoryToMiniGameController belum diisi.");
            }

            Debug.Log("RFID Category L: pilih subgame 2.");
            return true;
        }

        if (value == "m")
        {
            if (categoryToMiniGameController != null)
            {
                categoryToMiniGameController.SelectSubGame(3);
            }
            else
            {
                Debug.LogWarning("CategoryToMiniGameController belum diisi.");
            }

            Debug.Log("RFID Category M: pilih subgame 3.");
            return true;
        }

        return false;
    }

    private bool HandleNavigationCard(string uid)
    {
        if (!NavigationCards.TryGetValue(uid.Trim(), out string action)) return false;

        if (uiManager == null)
            uiManager = FindFirstObjectByType<UIManagerDonimal>(FindObjectsInactive.Include);

        if (uiManager == null)
        {
            Debug.LogWarning("[Docking] UIManager belum tersedia untuk kartu navigasi.");
            return true;
        }

        bool inDashboard = uiManager.dashboardPanel != null && uiManager.dashboardPanel.activeInHierarchy;
        bool inCategory = uiManager.categoryPanel != null && uiManager.categoryPanel.activeInHierarchy;
        bool inLearning = uiManager.belajarPanel != null && uiManager.belajarPanel.activeInHierarchy;
        bool inGame = uiManager.miniGamePanel != null && uiManager.miniGamePanel.activeInHierarchy;
        bool inReward = uiManager.rewardPanel != null && uiManager.rewardPanel.activeInHierarchy;

        // Jangan melewati alur splash dan pemilihan karakter.
        if (!inDashboard && !inCategory && !inLearning && !inGame && !inReward) return true;

        if (action == "back")
        {
            if (inGame || inReward) uiManager.ShowCategoryPanel();
            else if (inCategory || inLearning) uiManager.ShowDashboardPanel();
        }
        else if (int.TryParse(action, out int subGameNumber))
        {
            // Satu tap hanya memilih subgame pada panel pilihan kategori.
            if (!inCategory) return true;
            if (categoryToMiniGameController == null)
                categoryToMiniGameController = FindFirstObjectByType<CategoryToMiniGameImageController>(FindObjectsInactive.Include);
            if (categoryToMiniGameController != null)
                categoryToMiniGameController.SelectSubGame(subGameNumber);
            else
                Debug.LogWarning("[Docking] CategoryToMiniGameController belum tersedia.");
        }
        else if (action == "belajar")
        {
            if (!inLearning) uiManager.ShowBelajarPanel();
        }
        else
        {
            if (dashboardController == null)
                dashboardController = FindFirstObjectByType<DashboardController>(FindObjectsInactive.Include);
            if (dashboardController != null)
                dashboardController.SelectCategory(action);
            else
                Debug.LogWarning("[Docking] DashboardController belum tersedia.");
        }

        return true;
    }

    private void ProcessSimpleFormat(string data)
    {
        if (HandleActivityHttpResponse(data)) return;
        if (IsDockingStatusMessage(data)) return;

        if (HandleNavigationCard(data)) return;

        int separator = data.IndexOf(':');
        if (separator > 0 &&
            (data.StartsWith("anak:", StringComparison.OrdinalIgnoreCase) ||
             data.StartsWith("child:", StringComparison.OrdinalIgnoreCase)))
        {
            HandleChildRfid(data.Substring(0, separator), "", data.Substring(separator + 1));
            return;
        }

        if (separator > 0 && TrySendLearningCard(
            data.Substring(0, separator), data.Substring(separator + 1))) return;

        if (data == "docking_ready")
        {
            Debug.Log("Docking siap digunakan.");
            return;
        }

        if (data == "unknown")
        {
            Debug.LogWarning("Kartu RFID tidak dikenali.");
            return;
        }

        if (data == "kelinci" || data == "elang" || data == "penyu")
        {
            if (scanRFIDController != null)
            {
                scanRFIDController.CheckRFIDString(data, "");
            }

            return;
        }

        if (data == "huruf" || data == "angka" || data == "hewan" || data == "warna")
        {
            if (dashboardController != null)
            {
                dashboardController.SelectCategory(data);
            }

            return;
        }

        if (data.StartsWith("subgame:"))
        {
            string numberText = data.Replace("subgame:", "");

            if (categoryToMiniGameController != null)
            {
                if (int.TryParse(numberText, out int subGameNumber))
                {
                    categoryToMiniGameController.SelectSubGame(subGameNumber);
                }
                else
                {
                    categoryToMiniGameController.SelectSubGameByString(numberText);
                }
            }

            return;
        }

        if (data.StartsWith("huruf:"))
        {
            string cleanValue = data.Replace("huruf:", "");

            if (HandleDashboardRFID(cleanValue))
            {
                return;
            }

            if (HandleCategoryRFID(cleanValue))
            {
                return;
            }

            if (belajarPanelController != null)
            {
                belajarPanelController.ShowLearningCard("huruf", cleanValue);
            }

            if (miniGameAnswerController != null)
            {
                miniGameAnswerController.SubmitDockingAnswer(data);
            }

            return;
        }

        if (data.StartsWith("angka:"))
        {
            string cleanValue = data.Replace("angka:", "");

            if (belajarPanelController != null)
            {
                belajarPanelController.ShowLearningCard("angka", cleanValue);
            }

            if (miniGameAnswerController != null)
            {
                miniGameAnswerController.SubmitDockingAnswer(data);
            }

            return;
        }

        if (data.StartsWith("warna:"))
        {
            string cleanValue = data.Replace("warna:", "");

            if (belajarPanelController != null)
            {
                belajarPanelController.ShowLearningCard("warna", cleanValue);
            }

            miniGameAnswerController?.SubmitDockingAnswer(data);
            return;
        }

        if (data.StartsWith("hewan:"))
        {
            belajarPanelController?.ShowLearningCard("hewan", data.Substring("hewan:".Length));
            miniGameAnswerController?.SubmitDockingAnswer(data);
            return;
        }

        Debug.LogWarning("Format data docking belum dikenali: " + data);
    }

    private bool HandleActivityHttpResponse(string data)
    {
        const string prefix = "http post activity response:";
        if (string.IsNullOrWhiteSpace(data) ||
            !data.TrimStart().StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            return false;

        string statusText = data.Trim().Substring(prefix.Length).Trim();
        // Ini status pengiriman aktivitas ESP32, bukan kartu atau hasil jawaban.
        // Respons sukses tidak perlu memenuhi Console dengan pesan berulang.
        if (int.TryParse(statusText, out int statusCode) &&
            statusCode >= 200 && statusCode < 300)
            return true;

        if (statusCode == 422)
        {
            Debug.LogWarning("[Docking HTTP] Aktivitas ESP32 ditolak server (HTTP 422). " +
                "Periksa payload POST aktivitas dan isi respons server pada firmware ESP32; " +
                "kode status saja belum menunjukkan field yang ditolak.");
            return true;
        }

        Debug.LogWarning("[Docking HTTP] Pengiriman aktivitas tidak berhasil. Respons: " + statusText);
        return true;
    }

    private bool IsDockingStatusMessage(string data)
    {
        if (string.IsNullOrWhiteSpace(data)) return true;

        string value = data.Trim().ToLowerInvariant();
        return value.StartsWith("ets ") ||
               value.StartsWith("rst:") ||
               value.StartsWith("configsip:") ||
               value.StartsWith("clk_drv:") ||
               value.StartsWith("mode:") ||
               value.StartsWith("load:") ||
               value.StartsWith("entry ") ||
               value.StartsWith("*wm:") ||
               value.StartsWith("status wifi:") ||
               value.StartsWith("ip esp32:") ||
               value.StartsWith("server url ") ||
               value.StartsWith("siap. skenario");
    }

    private void OnApplicationQuit()
    {
        CloseSerial();
    }

    private void OnDestroy()
    {
        CloseSerial();
        if (Instance == this) Instance = null;
    }

    private void CloseSerial()
    {
        isConnected = false;
        SerialPort portToClose = serialPort;
        serialPort = null;
        if (portToClose == null) return;
        try
        {
            portToClose.Dispose();
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Docking] Gagal melepas port serial: " + e.Message);
        }
    }

    // LED tidak mencatat skor/aktivitas, sehingga tetap dikirim pada Susun Angka.
    public void SendAnswerLed(bool isCorrect)
    {
        if (!enableLedFeedback || !isDockingEnabled) return;
        string command = isCorrect ? correctLedCommand : wrongLedCommand;
        if (string.IsNullOrWhiteSpace(command) || command.Contains("\n") || command.Contains("\r"))
        {
            Debug.LogWarning("[Docking LED] Isi perintah LED dengan satu baris di Inspector.");
            return;
        }

        try
        {
            if (!isConnected || serialPort == null || !serialPort.IsOpen)
            {
                Debug.LogWarning("[Docking LED] Sinyal tidak terkirim: serial belum terhubung.");
                return;
            }
            serialPort.WriteLine(command);
            Debug.Log("[Docking LED] Mengirim: " + command);
        }
        catch (Exception e)
        {
            Debug.LogWarning("[Docking LED] Gagal mengirim: " + e.Message);
        }
    }

    public void SendLogToDocking(bool isCorrect)
    {
        // Susun Angka mengirim satu total langsung melalui HTTP dari Unity.
        // Hindari ESP32 ikut mencatat jawaban dan menggandakan aktivitas.
        if (miniGameAnswerController != null &&
            miniGameAnswerController.miniGameController != null &&
            miniGameAnswerController.miniGameController.GetSelectedSubGame() == "menyusun_angka")
            return;

        if (
            miniGameAnswerController != null &&
            !miniGameAnswerController.enableDockingInput
        )
        {
            return;
        }

        if (!isConnected || serialPort == null || !serialPort.IsOpen)
        {
            Debug.LogWarning("Gagal mengirim log ke ESP32. Serial belum terbuka.");
            return;
        }

        try
        {
            string command = isCorrect ? "LOG|benar" : "LOG|salah";
            serialPort.WriteLine(command);
            Debug.Log("Mengirim ke ESP32: " + command);
        }
        catch (Exception e)
        {
            Debug.LogError("Gagal mengirim ke ESP32: " + e.Message);
        }
    }
}
