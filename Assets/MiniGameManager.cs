using UnityEngine;
using TMPro;

public class MiniGameManager : MonoBehaviour
{
    [Header("UI Menampilkan Info Soal")]
    public TMP_Text teksSoal;          
    public TMP_Text teksKolomJawaban;  
    public TMP_Text teksSkor;
    public TMP_Text teksTimer;

    [Header("Komponen Pengaman Input Keyboard Fisik")]
    public TMP_InputField inputTersembunyi; 

    // Variabel Mode Huruf (Ejaan)
    private string kataTarget;          
    private string progressJawaban;     
    private int indeksHurufSekarang = 0; 
    private string[] bankKataBuah = { "APPLE", "BANANA", "MELON", "ORANGE", "CHERRY" };

    // Variabel Mode Angka (Pertambahan)
    private int angkaPertama;
    private int angkaKedua;
    private int jawabanBenarMatematika;
    private string inputJawabanAngkaUser = "";

    // Variabel Global Game
    private int skorSekarang = 0;
    private float sisaWaktu = 30f;     
    private bool gameSedangBerjalan = false;
    private string modeGameSekarang = "Huruf"; // "Huruf" atau "Angka"

    void Update()
    {
        if (!gameSedangBerjalan) return;

        if (sisaWaktu > 0)
        {
            sisaWaktu -= Time.deltaTime;
            teksTimer.text = "Waktu: " + Mathf.CeilToInt(sisaWaktu) + "s";
        }
        else
        {
            SelesaiGame();
        }

        // Memastikan input text tersembunyi selalu fokus menerima ketikan keyboard
        if (inputTersembunyi != null && !inputTersembunyi.isFocused)
        {
            inputTersembunyi.ActivateInputField();
        }
    }

    public void MulaiGame(string mode)
    {
        if (string.IsNullOrEmpty(mode)) mode = "Huruf";

        modeGameSekarang = mode;
        skorSekarang = 0;
        sisaWaktu = 30f;
        gameSedangBerjalan = true;
        inputJawabanAngkaUser = "";
        
        if (teksSkor != null) teksSkor.text = "Skor: 0";
        
        gameObject.SetActive(true); 

        if (inputTersembunyi != null)
        {
            inputTersembunyi.text = "";
            inputTersembunyi.gameObject.SetActive(true);
            inputTersembunyi.ActivateInputField(); 
            
            inputTersembunyi.onValueChanged.RemoveAllListeners();
            inputTersembunyi.onValueChanged.AddListener(OnUserMengetik);
        }

        BuatSoalBaru();
    }

void BuatSoalBaru()
    {
        inputJawabanAngkaUser = "";

        if (modeGameSekarang == "Angka")
        {
            // --- SKEMA MODE ANGKA (PERTAMBAHAN BASIC) ---
            angkaPertama = Random.Range(1, 20); // Ambil angka acak 1-19
            angkaKedua = Random.Range(1, 20);   // Ambil angka acak 1-19
            jawabanBenarMatematika = angkaPertama + angkaKedua;

            // FIX: Pertanyaan langsung digabungkan masuk ke dalam BALON BIRU (teksSoal)
            if (teksSoal != null)
            {
                teksSoal.text = "Berapakah hasil dari:\n" + angkaPertama + " + " + angkaKedua + " = ?";
                teksSoal.ForceMeshUpdate();
            }

            // Kolom bawah dibuat bersih untuk estetika pengetikan
            if (teksKolomJawaban != null)
            {
                teksKolomJawaban.text = "Ketik jawabanmu...";
                teksKolomJawaban.ForceMeshUpdate();
            }
        }
        else
        {
            // --- SKEMA MODE HURUF (EJEAN BUAH) ---
            kataTarget = bankKataBuah[Random.Range(0, bankKataBuah.Length)].ToUpper().Trim();
            indeksHurufSekarang = 0;

            if (teksSoal != null)
            {
                teksSoal.text = "Buah " + kataTarget + " terdiri dari huruf apa saja?";
                teksSoal.ForceMeshUpdate();
            }

            UpdateTampilanKolomHuruf();
        }
    }

    void UpdateTampilanKolomHuruf()
    {
        if (teksKolomJawaban == null) return;

        progressJawaban = "";
        for (int i = 0; i < kataTarget.Length; i++)
        {
            if (i < indeksHurufSekarang)
                progressJawaban += kataTarget[i] + " "; 
            else
                progressJawaban += "_ "; 
        }
        
        teksKolomJawaban.text = progressJawaban;
        teksKolomJawaban.ForceMeshUpdate(); 
    }

    void OnUserMengetik(string teksInput)
    {
        if (string.IsNullOrEmpty(teksInput) || !gameSedangBerjalan) return;

        // Ambil karakter terakhir yang diketik user
        string karakterKetik = teksInput.Substring(teksInput.Length - 1, 1).ToUpper().Trim();

        // Reset input field agar selalu siap menerima ketikan berikutnya
        inputTersembunyi.text = "";
        inputTersembunyi.ActivateInputField();

        if (modeGameSekarang == "Angka")
        {
            // Jika mode angka, pastikan yang diketik adalah angka 0-9
            if (System.Text.RegularExpressions.Regex.IsMatch(karakterKetik, "^[0-9]$"))
            {
                CekInputJawabanAngka(karakterKetik);
            }
        }
        else
        {
            // Jika mode huruf, pastikan yang diketik adalah huruf A-Z
            if (System.Text.RegularExpressions.Regex.IsMatch(karakterKetik, "^[A-Z]$"))
            {
                CekInputKeEjaanHuruf(karakterKetik);
            }
        }
    }

    void CekInputKeEjaanHuruf(string hurufInput)
    {
        if (indeksHurufSekarang >= kataTarget.Length) return;

        string hurufTargetSekarang = kataTarget[indeksHurufSekarang].ToString().ToUpper().Trim();

        if (hurufInput == hurufTargetSekarang)
        {
            indeksHurufSekarang++;
            UpdateTampilanKolomHuruf();

            if (indeksHurufSekarang >= kataTarget.Length)
            {
                skorSekarang += 20;
                if (teksSkor != null) teksSkor.text = "Skor: " + skorSekarang;
                BuatSoalBaru(); 
            }
        }
        else
        {
            sisaWaktu -= 2f; // Salah ketik huruf, waktu berkurang 2 detik
        }
    }

void CekInputJawabanAngka(string angkaInput)
    {
        // Gabungkan angka yang baru diketik dengan angka sebelumnya
        inputJawabanAngkaUser += angkaInput;

        // FIX: Saat user mengetik, balon biru (teksSoal) ikut memperbarui jawabannya secara real-time!
        if (teksSoal != null)
        {
            teksSoal.text = "Berapakah hasil dari:\n" + angkaPertama + " + " + angkaKedua + " = " + inputJawabanAngkaUser;
        }

        if (teksKolomJawaban != null)
        {
            teksKolomJawaban.text = "Jawabanmu: " + inputJawabanAngkaUser;
        }

        // Validasi matematika jawaban user
        if (int.TryParse(inputJawabanAngkaUser, out int jawabanUserInt))
        {
            if (jawabanUserInt == jawabanBenarMatematika)
            {
                skorSekarang += 20; 
                if (teksSkor != null) teksSkor.text = "Skor: " + skorSekarang;
                BuatSoalBaru(); // Jawaban benar, ganti soal baru!
            }
            else if (inputJawabanAngkaUser.Length >= jawabanBenarMatematika.ToString().Length)
            {
                sisaWaktu -= 2f; // Salah, potong waktu 2 detik
                inputJawabanAngkaUser = ""; // Reset input angka
                
                if (teksKolomJawaban != null)
                {
                    teksKolomJawaban.text = "Salah! Coba ketik lagi...";
                }
            }
        }
    }

    void SelesaiGame()
    {
        gameSedangBerjalan = false;
        int koinHasil = skorSekarang; 

        ShopManager shopUtama = FindFirstObjectByType<ShopManager>();
        
        if (shopUtama != null)
        {
            shopUtama.totalPoin += koinHasil; 

            if (shopUtama.teksPoin != null)
            {
                shopUtama.teksPoin.text = "poin : " + shopUtama.totalPoin;
                shopUtama.teksPoin.ForceMeshUpdate(true);
                Canvas.ForceUpdateCanvases();
            }

            Debug.Log("GAME SELESAI! Skor total berhasil masuk ke ShopManager: " + shopUtama.totalPoin);
        }
        else
        {
            Debug.LogError("ShopManager tidak ditemukan!");
        }

        if (inputTersembunyi != null) inputTersembunyi.gameObject.SetActive(false);
        gameObject.SetActive(false); 
    }

    public void KeluarGameManual()
    {
        gameSedangBerjalan = false;
        if (inputTersembunyi != null) inputTersembunyi.gameObject.SetActive(false);
        gameObject.SetActive(false);
    }
}