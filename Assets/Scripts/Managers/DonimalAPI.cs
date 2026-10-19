using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using UnityEngine;
using UnityEngine.Networking;

[System.Serializable]
public class GameOptionData
{
    // Field utama yang dipakai DONIMAL untuk Berburu Huruf dan Susun Angka.
    public string[] sequence;

    // Opsional. Aman dibiarkan jika backend tidak mengirim field ini.
    public string word;
    public string[] numbers;
}

[System.Serializable]
public class GameQuestionData
{
    public string id;
    public string material_id;
    public string subgame_id;

    public string question_text;
    public string question_type;

    // FINAL: semua jawaban disimpan sebagai string.
    public string correct_answer;

    // FINAL: JSONB dari PostgreSQL dikirim backend sebagai object JSON.
    public GameOptionData option_data;

    // Gambar soal dari server.
    public string media_url;

    // Opsional untuk subgame yang mempunyai gambar soal dan jawaban terpisah.
    // Beberapa endpoint menggunakan nama pendek, lainnya menggunakan *_media_url.
    public string incomplete;
    public string complete;
    public string incomplete_media_url;
    public string complete_media_url;

    // Alias jawaban untuk kompatibilitas endpoint lama.
    public string answer;

    // Audio soal dari server.
    public string audio_url;

    public int score_point;
    public string status;
}

[System.Serializable]
public class LearningMaterialData
{
    public string material_name;
    public string media_url;
}

[System.Serializable]
public class GameQuestionResponseWrapper
{
    public List<GameQuestionData> questions;
}

[Serializable]
public class GameActivityRequest
{
    public string device_id;
    public string child_rfid;
    public string category;
    public string game_type;
    public string answer_status;
    public int score;
    public int response_time;
}

[Serializable]
public class ChildProgressStartResponse
{
    public string child_id;
    public string child_name;
    public int level_huruf;
    public int score_huruf;
    public int level_angka;
    public int score_angka;
    public int level_hewan;
    public int score_hewan;
    public int level_warna;
    public int score_warna;
}

[Serializable]
public class CategoryProgressFinishRequest
{
    public string child_id;
    public string category;
    public int score_gained;
    public string game_status;
}

[Serializable]
public class CategoryProgressFinishResponse
{
    public string status;
    public string category;
    public int updated_level;
    public int updated_score;
    public bool is_level_up;
    public string message;
}

public class DonimalAPI : MonoBehaviour
{
    public static DonimalAPI Instance { get; private set; }

    [Header("Offline Mode")]
    [Tooltip("Aktifkan untuk menjalankan game sepenuhnya offline. Semua soal, gambar, audio, dan skor diambil/disimpan secara lokal. RFID anak tidak diperlukan.")]
    public bool offlineMode = false;

    [Header("Server Configuration")]
    [Tooltip("Alamat API hosting, contoh https://api.donimal.my.id. Dipakai jika Use Local Server dan UDP Discovery mati.")]
    public string serverBaseUrl = "https://api.donimal.my.id";
    [Tooltip("Cari Edge Hub melalui IP jaringan komputer ini dan UDP Broadcast. URL terisi setelah server ditemukan.")]
    public bool useLocalServer = false;
    [Range(1, 65535)]
    public int localServerPort = 8000;
    [Tooltip("Terisi otomatis saat Play. Tidak perlu mengetik IP lokal.")]
    [SerializeField] private string localServerUrl;

    [Header("Pengiriman Poin Game")]
    public string activityPath = "/api/game/activities";
    [Tooltip("Endpoint GET progress anak setelah kartu dibaca.")]
    public string childStartPath = "/api/game/start";
    [Tooltip("Aktifkan jika backend menyediakan endpoint GET progress anak.")]
    public bool loadChildProgress = true;
    [Tooltip("Endpoint POST hasil permainan per kategori.")]
    public string categoryFinishPath = "/api/game/finish";
    [Tooltip("Aktifkan hanya jika backend masih membutuhkan endpoint /game/activities selain endpoint Finish.")]
    public bool sendLegacyActivity = false;
    [Tooltip("Jika aktif, permainan membutuhkan kartu anak. Keyboard tetap dapat digunakan untuk menjawab.")]
    public bool requireChildRfid = false;
    public string deviceId;
    [Tooltip("RFID kartu anak yang terdaftar di server, bukan kartu materi/boneka.")]
    public string childRfid;
    private GameActivityRequest pendingActivity;
    private float activityStartedAt;
    private ChildProgressStartResponse currentChildProgress;

    public event Action<ChildProgressStartResponse> ChildProgressLoaded;
    public event Action<CategoryProgressFinishResponse> CategoryProgressUpdated;

    public ChildProgressStartResponse CurrentChildProgress => currentChildProgress;

    public void CancelActivity()
    {
        pendingActivity = null;
        activityStartedAt = 0f;
    }

    public int GetCategoryLevel(string category)
    {
        if (currentChildProgress == null || string.IsNullOrWhiteSpace(category))
            return 0;

        switch (category.Trim().ToLowerInvariant())
        {
            case "huruf":
                return currentChildProgress.level_huruf;
            case "angka":
                return currentChildProgress.level_angka;
            case "hewan":
                return currentChildProgress.level_hewan;
            case "warna":
                return currentChildProgress.level_warna;
            default:
                return 0;
        }
    }

    public void SetChildRfid(string uid)
    {
        childRfid = uid == null ? "" : uid.Trim();
        currentChildProgress = null;
        // Mode offline: simpan RFID tapi tidak fetch progress dari server.
        if (offlineMode) return;
        if (loadChildProgress && !string.IsNullOrWhiteSpace(childRfid))
        {
            StartCoroutine(GetChildProgress(childRfid));
        }
    }

    /// <summary>
    /// Normalisasi UID RFID anak ke format yang diterima backend:
    /// hilangkan titik dua/spasi pemisah dan ubah ke UPPERCASE.
    /// Contoh: "75:f3:f6:04" → "75F3F604"
    /// </summary>
    private static string NormalizeChildId(string uid)
    {
        if (string.IsNullOrWhiteSpace(uid)) return uid ?? "";
        return uid.Trim().Replace(":", "").Replace("-", "").Replace(" ", "").ToUpperInvariant();
    }

    public bool HasChildRfid()
    {
        return !string.IsNullOrWhiteSpace(childRfid);
    }

    public bool CanStartActivity()
    {
        // Mode offline: tidak perlu RFID anak sama sekali.
        if (offlineMode) return true;
        return !requireChildRfid || HasChildRfid();
    }

    public void BeginActivity(string category, string gameType)
    {
        pendingActivity = null;
        if (requireChildRfid && string.IsNullOrWhiteSpace(childRfid))
        {
            Debug.LogWarning("[Activity] RFID kartu anak wajib dibaca sebelum bermain.");
            return;
        }
        if (string.IsNullOrWhiteSpace(deviceId) && requireChildRfid)
        {
            Debug.LogWarning("[Activity] Device Id belum diisi. Poin tidak akan dikirim.");
            return;
        }
        pendingActivity = new GameActivityRequest
        {
            device_id = deviceId.Trim(),
            child_rfid = childRfid.Trim(),
            category = category == null ? "" : category.Trim().ToLowerInvariant(),
            game_type = gameType == null ? "" : gameType.Trim().ToLowerInvariant()
        };
        activityStartedAt = Time.realtimeSinceStartup;
    }

    public void SubmitActivity(int totalScore, int correctCount)
    {
        if (offlineMode)
        {
            // Mode offline: tidak kirim skor ke server.
            pendingActivity = null;
            Debug.Log("[Activity] Offline mode aktif. Skor tidak dikirim ke server.");
            return;
        }
        if (pendingActivity == null) return;
        GameActivityRequest activity = pendingActivity;
        pendingActivity = null; // Satu pengiriman per subgame; tidak retry tanpa idempotensi backend.
        activity.score = Mathf.Max(0, totalScore);
        // Ringkasan subgame: jangan gunakan status jawaban terakhir yang bisa menghapus total poin.
        activity.answer_status = correctCount > 0 ? "benar" : "salah";
        activity.response_time = Mathf.Max(0, Mathf.RoundToInt(Time.realtimeSinceStartup - activityStartedAt));
        if (sendLegacyActivity)
        {
            StartCoroutine(PostActivity(activity));
        }
        StartCoroutine(PostCategoryProgress(
            activity.category,
            activity.score,
            correctCount > 0 ? "won" : "lost"));
    }

    private IEnumerator GetChildProgress(string childId)
    {
        if (!isServerFound || string.IsNullOrWhiteSpace(serverBaseUrl) || string.IsNullOrWhiteSpace(childStartPath))
            yield break;

        string url = serverBaseUrl.TrimEnd('/') + "/" + childStartPath.TrimStart('/') +
            "?child_id=" + UnityWebRequest.EscapeURL(NormalizeChildId(childId));
        Debug.Log("[Progress] GET progress anak | child_id=" + childId + " → normalized=" + NormalizeChildId(childId) + " | URL: " + url);
        using (var request = UnityWebRequest.Get(url))
        {
            request.timeout = 15;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                string responseBody = request.downloadHandler != null ? request.downloadHandler.text : "";
                Debug.LogError("[Progress] Gagal mengambil progress anak | HTTP " + request.responseCode +
                    " | " + request.error + " | URL: " + url +
                    (string.IsNullOrWhiteSpace(responseBody) ? "" : " | Response: " + responseBody));
                yield break;
            }

            currentChildProgress = JsonUtility.FromJson<ChildProgressStartResponse>(request.downloadHandler.text);
            ChildProgressLoaded?.Invoke(currentChildProgress);
            Debug.Log("[Progress] Progress anak dimuat: " + currentChildProgress.child_name);
        }
    }

    private IEnumerator PostCategoryProgress(string category, int scoreGained, string gameStatus)
    {
        if (!isServerFound || string.IsNullOrWhiteSpace(serverBaseUrl) || string.IsNullOrWhiteSpace(childRfid) ||
            string.IsNullOrWhiteSpace(categoryFinishPath))
            yield break;

        var payload = new CategoryProgressFinishRequest
        {
            child_id = NormalizeChildId(childRfid.Trim()),
            category = category,
            score_gained = Mathf.Max(0, scoreGained),
            game_status = gameStatus
        };
        string url = serverBaseUrl.TrimEnd('/') + "/" + categoryFinishPath.TrimStart('/');
        using (var request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(payload)));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 15;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[Progress] Gagal mengirim progress " + category + " | HTTP " + request.responseCode + " | " + request.error);
                yield break;
            }

            var response = JsonUtility.FromJson<CategoryProgressFinishResponse>(request.downloadHandler.text);
            CategoryProgressUpdated?.Invoke(response);
            Debug.Log("[Progress] " + response.message + " | level=" + response.updated_level + " | score=" + response.updated_score);
        }
    }

    private IEnumerator PostActivity(GameActivityRequest activity)
    {
        if (!isServerFound || string.IsNullOrWhiteSpace(serverBaseUrl))
        {
            Debug.LogError("[Activity] Poin belum tersimpan: server belum tersedia.");
            yield break;
        }
        string url = serverBaseUrl.TrimEnd('/') + "/" + activityPath.TrimStart('/');
        using (var request = new UnityWebRequest(url, "POST"))
        {
            request.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(JsonUtility.ToJson(activity)));
            request.downloadHandler = new DownloadHandlerBuffer();
            request.SetRequestHeader("Content-Type", "application/json");
            request.timeout = 15;
            yield return request.SendWebRequest();
            if (request.result != UnityWebRequest.Result.Success)
            {
                Debug.LogError("[Activity] Pengiriman poin gagal | HTTP " + request.responseCode +
                    " | " + request.error + " | " + request.downloadHandler.text);
                yield break;
            }
            Debug.Log("[Activity] Server menerima poin " + activity.category + "/" + activity.game_type +
                ": " + activity.score + " | HTTP " + request.responseCode);
        }
    }

    [Header("Auto Discovery")]
    public bool enableUDPDiscovery = false;
    public int udpPort = 8888;

    public bool isServerFound { get; private set; } = false;

    private readonly Dictionary<string, Texture2D> textureCache =
        new Dictionary<string, Texture2D>();

    private readonly Dictionary<string, AudioClip> audioCache =
        new Dictionary<string, AudioClip>();

    private UdpClient udpClient;
    private Thread udpListenerThread;

    public event Action OnServerFound;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
            return;
        }

        Instance = this;
        DontDestroyOnLoad(gameObject);

        if (offlineMode)
        {
            // Mode offline penuh: tidak ada koneksi server, RFID anak tidak wajib.
            isServerFound = false;
            requireChildRfid = false;
            Debug.Log("[DonimalAPI] OFFLINE MODE aktif. Soal, gambar, dan audio akan diambil dari katalog lokal. Score tidak dikirim ke server.");
            return;
        }

        if (useLocalServer || enableUDPDiscovery)
        {
            localServerUrl = "";
            StartUDPDiscovery();
            if (useLocalServer) StartCoroutine(FindServerOnThisComputer());
        }
        else
        {
            if (TryNormalizeServerUrl(serverBaseUrl, out string normalizedUrl))
            {
                serverBaseUrl = normalizedUrl;
                isServerFound = true;
                Debug.Log("[DonimalAPI] Server aktif: " + serverBaseUrl);
            }
            else
            {
                Debug.LogError("[DonimalAPI] Alamat server tidak valid. Gunakan URL HTTP/HTTPS, contoh https://api.donimal.my.id atau http://192.168.1.6:8000.");
            }
        }
    }

    private IEnumerator FindServerOnThisComputer()
    {
        while (!isServerFound)
        {
            var addresses = new HashSet<string>();
            addresses.Add(IPAddress.Loopback.ToString());
            try
            {
                foreach (NetworkInterface adapter in NetworkInterface.GetAllNetworkInterfaces())
                {
                    if (adapter.OperationalStatus != OperationalStatus.Up) continue;
                    foreach (UnicastIPAddressInformation entry in adapter.GetIPProperties().UnicastAddresses)
                    {
                        if (entry.Address.AddressFamily == AddressFamily.InterNetwork &&
                            !IPAddress.IsLoopback(entry.Address))
                            addresses.Add(entry.Address.ToString());
                    }
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[DonimalAPI] Tidak dapat membaca IP adapter: " + e.Message);
            }

            foreach (string address in addresses)
            {
                if (isServerFound) yield break;
                string candidate = "http://" + address + ":" + Mathf.Clamp(localServerPort, 1, 65535);
                using (UnityWebRequest request = UnityWebRequest.Get(candidate + "/api/system/status"))
                {
                    request.timeout = 2;
                    UnityWebRequestAsyncOperation operation;
                    try { operation = request.SendWebRequest(); }
                    catch (Exception) { continue; }
                    yield return operation;

                    if (isServerFound) yield break;
                    string body = request.downloadHandler.text;
                    if (request.result == UnityWebRequest.Result.Success &&
                        !string.IsNullOrWhiteSpace(body))
                    {
                        AcceptDiscoveredServer(candidate);
                        yield break;
                    }

                    if (request.result == UnityWebRequest.Result.Success)
                    {
                        Debug.LogWarning("[DonimalAPI] Endpoint status " + candidate +
                            " merespons HTTP tetapi body kosong; discovery dilanjutkan.");
                    }
                }
            }
            yield return new WaitForSecondsRealtime(2f);
        }
    }

    private void AcceptDiscoveredServer(string url)
    {
        if (this == null || Instance != this || isServerFound) return;
        serverBaseUrl = url;
        localServerUrl = url;
        isServerFound = true;
        StopUDPDiscovery();
        Debug.Log("[DonimalAPI] SERVER DITEMUKAN: " + url);
        OnServerFound?.Invoke();
    }

    private static bool TryNormalizeServerUrl(string value, out string normalizedUrl)
    {
        normalizedUrl = "";
        if (string.IsNullOrWhiteSpace(value)) return false;

        value = value.Trim();
        if (!value.Contains("://"))
        {
            // IP/localhost tanpa protokol memakai HTTP; domain memakai HTTPS.
            if (!Uri.TryCreate("http://" + value, UriKind.Absolute, out Uri candidate))
                return false;
            bool local = candidate.IsLoopback || IPAddress.TryParse(candidate.Host, out _);
            value = (local ? "http://" : "https://") + value;
        }

        if (!Uri.TryCreate(value, UriKind.Absolute, out Uri uri) ||
            (uri.Scheme != Uri.UriSchemeHttp && uri.Scheme != Uri.UriSchemeHttps) ||
            string.IsNullOrEmpty(uri.Host) || !string.IsNullOrEmpty(uri.UserInfo) ||
            !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            return false;

        normalizedUrl = uri.AbsoluteUri.TrimEnd('/');
        return true;
    }

    private void Update()
    {
        UnityMainThreadDispatcher.Update();
    }

    private void OnDestroy()
    {
        StopUDPDiscovery();
        if (Instance == this)
        {
            Instance = null;
        }
    }

    private void StartUDPDiscovery()
    {
        isServerFound = false;

        try
        {
            udpClient = new UdpClient();
            udpClient.Client.SetSocketOption(
                SocketOptionLevel.Socket,
                SocketOptionName.ReuseAddress,
                true
            );

            udpClient.Client.Bind(
                new IPEndPoint(IPAddress.Any, udpPort)
            );

            udpListenerThread = new Thread(ListenForUDP);
            udpListenerThread.IsBackground = true;
            udpListenerThread.Start();

            Debug.Log(
                "[DonimalAPI] Listening UDP Broadcast port " +
                udpPort
            );
        }
        catch (Exception e)
        {
            Debug.LogError(
                "[DonimalAPI] Gagal membuka UDP: " +
                e.Message
            );
        }
    }

    private void ListenForUDP()
    {
        IPEndPoint remoteEndPoint =
            new IPEndPoint(IPAddress.Any, 0);

        while (!isServerFound)
        {
            try
            {
                byte[] data =
                    udpClient.Receive(ref remoteEndPoint);

                string message =
                    Encoding.UTF8.GetString(data).Trim();

                string ip;
                // Format otomatis: IP diambil dari pengirim paket, server hanya mengirim port.
                if (message.StartsWith("DONIMAL_PORT:", StringComparison.Ordinal))
                {
                    if (!int.TryParse(message.Substring("DONIMAL_PORT:".Length), out int port) ||
                        port < 1 || port > 65535)
                        continue;
                    ip = "http://" + remoteEndPoint.Address + ":" + port;
                }
                else if (message.StartsWith("DONIMAL_SERVER:", StringComparison.Ordinal))
                {
                    // Tetap mendukung broadcast backend lama.
                    ip = message.Substring("DONIMAL_SERVER:".Length).Trim();
                }
                else
                {
                    continue;
                }

                // Broadcast lama berisi IP saja; format baru boleh URL atau IP:port.
                if (IPAddress.TryParse(ip, out IPAddress address))
                {
                    string host = address.AddressFamily == AddressFamily.InterNetworkV6
                        ? "[" + address + "]" : address.ToString();
                    ip = "http://" + host + ":" + Math.Max(1, Math.Min(65535, localServerPort));
                }

                if (!TryNormalizeServerUrl(ip, out string discoveredUrl))
                {
                    continue;
                }

                UnityMainThreadDispatcher.RunOnMainThread(() =>
                {
                    AcceptDiscoveredServer(discoveredUrl);
                });
                break;
            }
            catch
            {
                break;
            }
        }
    }

    private void StopUDPDiscovery()
    {
        try
        {
            udpClient?.Close();
        }
        catch
        {
        }

        udpClient = null;

        if (
            udpListenerThread != null &&
            udpListenerThread.IsAlive
        )
        {
            try
            {
                udpListenerThread.Interrupt();
            }
            catch
            {
            }
        }

        udpListenerThread = null;
    }

    private static class UnityMainThreadDispatcher
    {
        private static readonly Queue<Action> actionQueue =
            new Queue<Action>();

        public static void RunOnMainThread(Action action)
        {
            if (action == null)
            {
                return;
            }

            lock (actionQueue)
            {
                actionQueue.Enqueue(action);
            }
        }

        public static void Update()
        {
            lock (actionQueue)
            {
                while (actionQueue.Count > 0)
                {
                    actionQueue.Dequeue()?.Invoke();
                }
            }
        }
    }

    private string BuildUrl(string url)
    {
        if (string.IsNullOrWhiteSpace(url))
        {
            return "";
        }

        if (
            url.StartsWith(
                "http://",
                StringComparison.OrdinalIgnoreCase
            ) ||
            url.StartsWith(
                "https://",
                StringComparison.OrdinalIgnoreCase
            )
        )
        {
            return url;
        }

        return
            serverBaseUrl.TrimEnd('/') +
            "/" +
            url.TrimStart('/');
    }

    // ==================================================
    // GAMBAR SOAL
    // ==================================================

    public void GetTexture(
        string mediaUrl,
        Action<Texture2D> onSuccess,
        Action<string> onError
    )
    {
        if (offlineMode)
        {
            onError?.Invoke("Offline mode aktif. Gambar diambil dari katalog lokal.");
            return;
        }

        if (!isServerFound)
        {
            onError?.Invoke(
                "Server belum ditemukan."
            );
            return;
        }

        if (string.IsNullOrWhiteSpace(mediaUrl))
        {
            onError?.Invoke(
                "media_url kosong."
            );
            return;
        }

        string fullUrl =
            BuildUrl(mediaUrl);

        if (
            textureCache.TryGetValue(
                fullUrl,
                out Texture2D cachedTexture
            ) &&
            cachedTexture != null
        )
        {
            onSuccess?.Invoke(cachedTexture);
            return;
        }

        StartCoroutine(
            DownloadTextureCoroutine(
                fullUrl,
                onSuccess,
                onError
            )
        );
    }

    private IEnumerator DownloadTextureCoroutine(
        string url,
        Action<Texture2D> onSuccess,
        Action<string> onError
    )
    {
        Debug.Log(
            "[DonimalAPI] Download gambar: " +
            url
        );

        using (
            UnityWebRequest request =
                UnityWebRequestTexture.GetTexture(url)
        )
        {
            UnityWebRequestAsyncOperation operation;

            try
            {
                operation = request.SendWebRequest();
            }
            catch (Exception e)
            {
                onError?.Invoke("Request tidak dapat dimulai: " + e.Message);
                yield break;
            }

            yield return operation;

            if (
                request.result !=
                UnityWebRequest.Result.Success
            )
            {
                onError?.Invoke(request.error);
                yield break;
            }

            Texture2D texture =
                DownloadHandlerTexture.GetContent(
                    request
                );

            if (texture == null)
            {
                onError?.Invoke(
                    "Texture hasil download null."
                );
                yield break;
            }

            textureCache[url] = texture;
            onSuccess?.Invoke(texture);
        }
    }

    // ==================================================
    // AUDIO SOAL
    // ==================================================

    public void GetAudio(
        string audioUrl,
        Action<AudioClip> onSuccess,
        Action<string> onError
    )
    {
        if (offlineMode)
        {
            onError?.Invoke("Offline mode aktif. Audio diambil dari katalog lokal.");
            return;
        }

        if (!isServerFound)
        {
            onError?.Invoke(
                "Server belum ditemukan."
            );
            return;
        }

        if (string.IsNullOrWhiteSpace(audioUrl))
        {
            onError?.Invoke(
                "audio_url kosong."
            );
            return;
        }

        string fullUrl =
            BuildUrl(audioUrl);

        if (
            audioCache.TryGetValue(
                fullUrl,
                out AudioClip cachedClip
            ) &&
            cachedClip != null
        )
        {
            onSuccess?.Invoke(cachedClip);
            return;
        }

        StartCoroutine(
            DownloadAudioCoroutine(
                fullUrl,
                onSuccess,
                onError
            )
        );
    }

    private IEnumerator DownloadAudioCoroutine(
        string url,
        Action<AudioClip> onSuccess,
        Action<string> onError
    )
    {
        Debug.Log(
            "[DonimalAPI] Download audio: " +
            url
        );

        using (
            UnityWebRequest request =
                UnityWebRequestMultimedia.GetAudioClip(
                    url,
                    AudioType.MPEG
                )
        )
        {
            UnityWebRequestAsyncOperation operation;

            try
            {
                operation = request.SendWebRequest();
            }
            catch (Exception e)
            {
                onError?.Invoke("Request tidak dapat dimulai: " + e.Message);
                yield break;
            }

            yield return operation;

            if (
                request.result !=
                UnityWebRequest.Result.Success
            )
            {
                Debug.LogError(
                    "[DonimalAPI] Audio gagal: " +
                    request.error
                );

                onError?.Invoke(request.error);
                yield break;
            }

            AudioClip clip =
                DownloadHandlerAudioClip.GetContent(
                    request
                );

            if (clip == null)
            {
                onError?.Invoke(
                    "AudioClip hasil download null."
                );
                yield break;
            }

            audioCache[url] = clip;
            onSuccess?.Invoke(clip);
        }
    }

    // ==================================================
    // RANDOM SOAL
    // ==================================================

    public void GetRandomQuestions(
        string mode,
        int limit,
        Action<List<GameQuestionData>> onSuccess,
        Action<string> onError
    )
    {
        if (offlineMode)
        {
            onError?.Invoke("Offline mode aktif. Gunakan soal lokal.");
            return;
        }

        if (!isServerFound)
        {
            if (useLocalServer || enableUDPDiscovery)
            {
                StartCoroutine(WaitForServerAndGetQuestions(mode, limit, onSuccess, onError));
                return;
            }
            onError?.Invoke(
                "Server belum ditemukan."
            );
            return;
        }

        // Kontrak endpoint membatasi query limit pada rentang 1..50.
        // Clamp di satu tempat agar nilai Inspector/controller tidak memicu HTTP 422.
        int safeLimit = Mathf.Clamp(limit, 1, 50);

        string url =
            serverBaseUrl.TrimEnd('/') +
            "/api/game/questions/random" +
            "?mode=" +
            UnityWebRequest.EscapeURL(mode) +
            "&limit=" +
            safeLimit;

        StartCoroutine(
            GetQuestionsCoroutine(
                url,
                onSuccess,
                onError
            )
        );
    }

    private IEnumerator WaitForServerAndGetQuestions(
        string mode,
        int limit,
        Action<List<GameQuestionData>> onSuccess,
        Action<string> onError
    )
    {
        float deadline = Time.realtimeSinceStartup + 15f;
        while (!isServerFound && Time.realtimeSinceStartup < deadline)
            yield return null;

        if (!isServerFound)
        {
            onError?.Invoke("Edge Hub belum ditemukan. " +
                (useLocalServer ? "Pemeriksaan HTTP pada IP komputer ini di port " + localServerPort + " belum berhasil. " : "") +
                "Broadcast UDP port " + udpPort + " juga belum diterima. " +
                "Pastikan Server Utama/API berjalan, bukan hanya jendela Edge Hub terbuka. " +
                "Pencarian tetap berlangsung; ulangi buka game setelah log SERVER DITEMUKAN muncul.");
            yield break;
        }

        GetRandomQuestions(mode, limit, onSuccess, onError);
    }

    private IEnumerator GetQuestionsCoroutine(
        string url,
        Action<List<GameQuestionData>> onSuccess,
        Action<string> onError
    )
    {
        Debug.Log(
            "[DonimalAPI] GET soal: " +
            url
        );

        using (
            UnityWebRequest request =
                UnityWebRequest.Get(url)
        )
        {
            request.timeout = 15;
            UnityWebRequestAsyncOperation operation;

            try
            {
                operation = request.SendWebRequest();
            }
            catch (Exception e)
            {
                onError?.Invoke("Request tidak dapat dimulai: " + e.Message);
                yield break;
            }

            yield return operation;

            if (
                request.result !=
                UnityWebRequest.Result.Success
            )
            {
                string responseBody =
                    request.downloadHandler != null
                        ? request.downloadHandler.text
                        : "";

                onError?.Invoke(
                    request.error +
                    " | HTTP " + request.responseCode +
                    " | URL: " + url +
                    (request.responseCode == 0
                        ? " | Tidak menerima respons HTTP. Periksa koneksi internet perangkat, " +
                          "DNS, VPN/proxy, firewall untuk Unity, dan ketersediaan server."
                        : "") +
                    (string.IsNullOrWhiteSpace(responseBody)
                        ? ""
                        : " | Response: " + responseBody)
                );
                yield break;
            }

            string json =
                request.downloadHandler.text;

            GameQuestionResponseWrapper wrapper = null;

            try
            {
                if (
                    json.TrimStart().StartsWith("[")
                )
                {
                    string wrappedJson =
                        "{\"questions\":" +
                        json +
                        "}";

                    wrapper =
                        JsonUtility.FromJson<
                            GameQuestionResponseWrapper
                        >(wrappedJson);
                }
                else
                {
                    wrapper =
                        JsonUtility.FromJson<
                            GameQuestionResponseWrapper
                        >(json);
                }
            }
            catch (Exception e)
            {
                onError?.Invoke(
                    "JSON Error: " +
                    e.Message
                );
                yield break;
            }

            if (
                wrapper == null ||
                wrapper.questions == null
            )
            {
                onError?.Invoke(
                    "Format JSON soal tidak sesuai."
                );
                yield break;
            }

            Debug.Log(
                "[DonimalAPI] Soal diterima: " +
                wrapper.questions.Count
            );

            onSuccess?.Invoke(
                wrapper.questions
            );
        }
    }

    // ==================================================
    // LEARNING MATERIAL
    // ==================================================

    public void GetLearningMaterial(
        string value,
        Action<LearningMaterialData> onSuccess,
        Action<string> onError
    )
    {
        if (offlineMode)
        {
            onError?.Invoke("Offline mode aktif. Gambar jawaban diambil dari katalog lokal.");
            return;
        }

        if (!isServerFound)
        {
            onError?.Invoke(
                "Server belum ditemukan."
            );
            return;
        }

        string url =
            serverBaseUrl.TrimEnd('/') +
            "/api/learning_materials/by_value/" +
            UnityWebRequest.EscapeURL(value);

        StartCoroutine(
            GetLearningMaterialCoroutine(
                url,
                onSuccess,
                onError
            )
        );
    }

    private IEnumerator GetLearningMaterialCoroutine(
        string url,
        Action<LearningMaterialData> onSuccess,
        Action<string> onError
    )
    {
        using (
            UnityWebRequest request =
                UnityWebRequest.Get(url)
        )
        {
            UnityWebRequestAsyncOperation operation;

            try
            {
                operation = request.SendWebRequest();
            }
            catch (Exception e)
            {
                onError?.Invoke("Request tidak dapat dimulai: " + e.Message);
                yield break;
            }

            yield return operation;

            if (
                request.result !=
                UnityWebRequest.Result.Success
            )
            {
                onError?.Invoke("HTTP " + request.responseCode + " | " + url + " | " + request.error);
                yield break;
            }

            string json =
                request.downloadHandler.text;

            try
            {
                LearningMaterialData data =
                    JsonUtility.FromJson<
                        LearningMaterialData
                    >(json);

                if (data == null || string.IsNullOrWhiteSpace(data.media_url))
                {
                    onError?.Invoke("Respons materi tidak berisi media_url | " + url);
                    yield break;
                }
                onSuccess?.Invoke(data);
            }
            catch (Exception e)
            {
                onError?.Invoke(e.Message);
            }
        }
    }

    public void ClearTextureCache()
    {
        textureCache.Clear();
    }

    public void ClearAudioCache()
    {
        audioCache.Clear();
    }

    public void ClearAllCache()
    {
        textureCache.Clear();
        audioCache.Clear();
    }
}
