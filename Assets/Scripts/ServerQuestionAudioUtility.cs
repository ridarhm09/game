using UnityEngine;

/// <summary>
/// Pemutar audio lokal berdasarkan karakter aktif dan ID soal server.
/// Tidak perlu drag AudioSource di Inspector: AudioSource dibuat otomatis.
/// </summary>
public static class ServerQuestionAudioUtility
{
    private static GameObject playerObject;
    private static AudioSource audioSource;

    private static void EnsurePlayer()
    {
        if (audioSource != null) return;

        playerObject = new GameObject("DONIMAL_ServerQuestionAudio");
        Object.DontDestroyOnLoad(playerObject);

        audioSource = playerObject.AddComponent<AudioSource>();
        audioSource.playOnAwake = false;
        audioSource.loop = false;
        audioSource.spatialBlend = 0f;
    }

    public static void Play(GameQuestionData question, string logPrefix = "[QuestionAudio]", string fallbackLabel = null)
    {
        Stop();
        if (question == null || string.IsNullOrWhiteSpace(question.id))
        {
            Debug.LogWarning(logPrefix + " ID soal kosong; audio lokal tidak dapat dicari.");
            return;
        }
        AudioManagerDonimal audioManager = Object.FindFirstObjectByType<AudioManagerDonimal>();
        string character = audioManager != null ? audioManager.GetActiveCharacterName() : "";
        if (character != "kira" && character != "eyra" && character != "pasa")
        {
            Debug.LogWarning(logPrefix + " Audio tidak dapat diputar. ID soal: " + question.id +
                             " | Jawaban: " + question.correct_answer +
                             " | Karakter: belum tersedia. Periksa pilihan karakter dan hubungan AudioManagerDonimal ke ScanRFIDController.");
            return;
        }
        LocalQuestionAudioCatalog catalog = Resources.Load<LocalQuestionAudioCatalog>("LocalQuestionAudioCatalog");
        AudioClip clip = catalog != null ? catalog.GetClip(question.id, character) : null;
        if (clip == null && catalog != null && !string.IsNullOrWhiteSpace(fallbackLabel))
            clip = catalog.GetClipByLabel(fallbackLabel, character);
        if (clip == null)
        {
            string reason = catalog == null ? "Katalog tidak ditemukan" :
                !catalog.ContainsQuestion(question.id) ? "ID soal belum terdaftar di katalog" :
                "Slot audio " + character + " masih kosong di katalog";
            Debug.Log(logPrefix + " Audio lokal belum dipasangkan. ID soal: " + question.id +
                             " | Jawaban: " + question.correct_answer + " | Karakter: " + character +
                             " | Penyebab: " + reason +
                             ". Isi pasangan di Assets/Resources/LocalQuestionAudioCatalog.asset.");
            return;
        }
        EnsurePlayer();
        audioSource.clip = clip;
        audioSource.Play();
        // Catat pasangan yang benar-benar dipakai, bukan perkiraan dari nama file.
        Debug.Log(logPrefix + " [AudioKatalog] ID=" + question.id +
                  " | Jawaban=" + question.correct_answer +
                  " | Karakter=" + character +
                  " | Clip=" + clip.name +
                  " | Gambar=" + question.media_url, catalog);
    }

    public static void Stop()
    {
        if (audioSource != null)
        {
            audioSource.Stop();
            audioSource.clip = null;
        }
    }
}
