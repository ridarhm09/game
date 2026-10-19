using System.Collections;
using UnityEngine;

public class AudioManagerDonimal : MonoBehaviour
{
    [Header("Audio Source")]
    public AudioSource audioSource;

    [Header("Scan RFID Controller")]
    public ScanRFIDController scanRFIDController;

    [Header("Narrator Audio")]
    [Tooltip("Sambutan saat splash screen dibuka.")]
    public AudioClip narSplashWelcome;
    [HideInInspector]
    public AudioClip narSplashLetakkanBoneka;
    [Tooltip("Panduan meletakkan karakter ke docking saat panel scan RFID dibuka.")]
    public AudioClip narRFIDScanBoneka;
    [Tooltip("Pemberitahuan karakter belum terbaca setelah batas waktu scan.")]
    public AudioClip narRFIDScanGagal;

    [Header("Kira Audio Umum")]
    [InspectorName("KIRA_IntroSoal")]
    [Tooltip("Masukkan KIRA_IntroSoal.mp3")]
    public AudioClip kiraIntroSoal;
    [InspectorName("KIRA_IntroDashboard")]
    [Tooltip("Masukkan KIRA_IntroDashboard.mp3")]
    public AudioClip kiraIntroDashboard;
    [InspectorName("KIRA_IntroSubgame")]
    [Tooltip("Masukkan KIRA_IntroSubgame.mp3")]
    public AudioClip kiraIntroSubgame;
    [InspectorName("KIRA_IntroFeedback")]
    [Tooltip("Masukkan KIRA_IntroFeedback.mp3")]
    public AudioClip kiraIntroFeedback;
    [InspectorName("KIRA_IntroJawaban")]
    [Tooltip("Masukkan KIRA_IntroJawaban.mp3")]
    public AudioClip kiraIntroJawaban;

    [Header("Eyra Audio Umum")]
    [InspectorName("EYRA_IntroSoal")]
    [Tooltip("Masukkan EYRA_IntroSoal.mp3")]
    public AudioClip eyraIntroSoal;
    [InspectorName("EYRA_IntroDashboard")]
    [Tooltip("Masukkan EYRA_IntroDashboard.mp3")]
    public AudioClip eyraIntroDashboard;
    [InspectorName("EYRA_IntroSubgame")]
    [Tooltip("Masukkan EYRA_IntroSubgame.mp3")]
    public AudioClip eyraIntroSubgame;
    [InspectorName("EYRA_IntroFeedback")]
    [Tooltip("Masukkan EYRA_IntroFeedback.mp3")]
    public AudioClip eyraIntroFeedback;
    [InspectorName("EYRA_IntroJawaban")]
    [Tooltip("Masukkan EYRA_IntroJawaban.mp3")]
    public AudioClip eyraIntroJawaban;

    [Header("Pasa Audio Umum")]
    [InspectorName("PASA_IntroSoal")]
    [Tooltip("Masukkan PASA_IntroSoal.mp3")]
    public AudioClip pasaIntroSoal;
    [InspectorName("PASA_IntroDashboard")]
    [Tooltip("Masukkan PASA_IntroDashboard.mp3")]
    public AudioClip pasaIntroDashboard;
    [InspectorName("PASA_IntroSubgame")]
    [Tooltip("Masukkan PASA_IntroSubgame.mp3")]
    public AudioClip pasaIntroSubgame;
    [InspectorName("PASA_IntroFeedback")]
    [Tooltip("Masukkan PASA_IntroFeedback.mp3")]
    public AudioClip pasaIntroFeedback;
    [InspectorName("PASA_IntroJawaban")]
    [Tooltip("Masukkan PASA_IntroJawaban.mp3")]
    public AudioClip pasaIntroJawaban;

    private Coroutine audioSequenceCoroutine;
    private bool isPlayingSequence;

    public static IEnumerator WaitUntilFinished(AudioManagerDonimal manager, float fallbackDuration)
    {
        if (manager == null || manager.audioSource == null ||
            (!manager.isPlayingSequence && !manager.audioSource.isPlaying))
        {
            yield return new WaitForSecondsRealtime(Mathf.Max(0f, fallbackDuration));
            yield break;
        }

        while (manager != null && manager.isActiveAndEnabled &&
            (manager.isPlayingSequence ||
             (manager.audioSource != null && manager.audioSource.isActiveAndEnabled &&
              (manager.audioSource.isPlaying || AudioListener.pause))))
        {
            yield return null;
        }
    }

    // ==================================================
    // PEMUTAR AUDIO DASAR
    // ==================================================

    public void PlayAudio(AudioClip clip)
    {
        StopAudio();

        if (audioSource == null)
        {
            Debug.LogWarning(
                "[AudioManager] AudioSource belum dihubungkan."
            );
            return;
        }

        if (clip == null)
        {
            Debug.LogWarning(
                "[AudioManager] AudioClip yang akan diputar masih kosong."
            );
            return;
        }

        PlayClipInternal(clip);
    }

    private void PlayClipInternal(AudioClip clip)
    {
        if (audioSource == null || clip == null)
        {
            return;
        }

        audioSource.Stop();
        audioSource.clip = clip;
        audioSource.loop = false;
        audioSource.Play();

        Debug.Log(
            "[AudioManager] Memutar audio: " + clip.name
        );
    }

    public void StopAudio()
    {
        isPlayingSequence = false;
        if (audioSequenceCoroutine != null)
        {
            StopCoroutine(audioSequenceCoroutine);
            audioSequenceCoroutine = null;
        }

        if (audioSource != null)
        {
            audioSource.Stop();
        }
    }

    private void StartAudioSequence(params AudioClip[] clips)
    {
        StopAudio();
        isPlayingSequence = true;
        audioSequenceCoroutine =
            StartCoroutine(PlayAudioSequence(clips));
    }

    private IEnumerator PlayAudioSequence(AudioClip[] clips)
    {
        if (clips == null || clips.Length == 0)
        {
            isPlayingSequence = false;
            audioSequenceCoroutine = null;
            yield break;
        }

        foreach (AudioClip clip in clips)
        {
            if (clip == null)
            {
                continue;
            }

            PlayClipInternal(clip);
            while (audioSource != null && audioSource.isActiveAndEnabled &&
                (audioSource.isPlaying || AudioListener.pause))
            {
                yield return null;
            }
        }

        isPlayingSequence = false;
        audioSequenceCoroutine = null;
    }

    // ==================================================
    // AUDIO NARATOR DAN UMUM
    // ==================================================

    public void PlaySplashWelcome()
    {
        PlayAudio(narSplashWelcome);
    }

    public void PlayLetakkanBoneka()
    {
        PlayAudio(narSplashLetakkanBoneka);
    }

    public void PlayRFIDScanBoneka()
    {
        PlayAudio(narRFIDScanBoneka);
    }

    public void PlayRFIDScanGagal()
    {
        PlayAudio(narRFIDScanGagal);
    }

    public void PlayCharacterIntroAudio()
    {
        string character = GetActiveCharacter();

        if (character == "kira")
        {
            PlayAudio(kiraIntroFeedback);
        }
        else if (character == "eyra")
        {
            PlayAudio(eyraIntroFeedback);
        }
        else if (character == "pasa")
        {
            PlayAudio(pasaIntroFeedback);
        }
        else
        {
            Debug.LogWarning(
                "[AudioManager] Belum ada karakter aktif untuk audio intro."
            );
        }
    }

    public void PlayBelajarCardAudio(
        string category,
        string value
    )
    {
        string cleanCategory = NormalizeText(category);
        if (cleanCategory == "animal") cleanCategory = "hewan";
        string cleanValue = NormalizeText(RfidAnswerUtility.Normalize(cleanCategory, value));
        string character = GetActiveCharacter();

        AudioClip selectedClip =
            GetBelajarCardClip(
                cleanCategory,
                cleanValue,
                character
            );

        if (selectedClip == null)
        {
            Debug.LogWarning(
                "[AudioManager] Audio belajar belum tersedia untuk: " +
                character + " - " +
                cleanCategory + ":" + cleanValue
            );
            return;
        }

        Debug.Log("[AudioManager] Putar audio belajar: " + character + " - " +
            cleanCategory + ":" + cleanValue + " | Clip: " + selectedClip.name);
        PlayAudio(selectedClip);
    }

    private AudioClip GetBelajarCardClip(string category, string value, string character)
    {
        LocalQuestionAudioCatalog catalog =
            Resources.Load<LocalQuestionAudioCatalog>("LocalQuestionAudioCatalog");
        return catalog != null
            ? catalog.GetClip("belajar:" + category + ":" + value, character)
            : null;
    }

    public void PlayDashboardAudio()
    {
        string character = GetActiveCharacter();

        if (character == "kira") PlayAudio(kiraIntroDashboard);
        else if (character == "eyra") PlayAudio(eyraIntroDashboard);
        else if (character == "pasa") PlayAudio(pasaIntroDashboard);
        else Debug.LogWarning(
            "[AudioManager] Karakter aktif belum dipilih."
        );
    }

    private void PlayCatalogFeedback(string id, string character)
    {
        LocalQuestionAudioCatalog catalog = Resources.Load<LocalQuestionAudioCatalog>("LocalQuestionAudioCatalog");
        AudioClip clip = catalog != null ? catalog.GetClip(id, character) : null;
        if (clip == null)
            Debug.LogWarning("[AudioManager] Audio katalog belum diisi: " + id + " | " + character);
        PlayAudio(clip);
    }

    public void PlayRewardAudio()
    {
        string character = GetActiveCharacter();

        PlayCatalogFeedback("feedback:reward", character);
    }

    public void PlayMenungguKategoriAudio()
    {
        string character = GetActiveCharacter();

        if (character == "kira") PlayAudio(kiraIntroSubgame);
        else if (character == "eyra") PlayAudio(eyraIntroSubgame);
        else if (character == "pasa") PlayAudio(pasaIntroSubgame);
    }

    public void PlayMenungguSubgameAudio()
    {
        string character = GetActiveCharacter();

        if (character == "kira") PlayAudio(kiraIntroSubgame);
        else if (character == "eyra") PlayAudio(eyraIntroSubgame);
        else if (character == "pasa") PlayAudio(pasaIntroSubgame);
    }

    public void PlayIntroJawabanAudio()
    {
        string character = GetActiveCharacter();
        if (character == "kira") PlayAudio(kiraIntroJawaban);
        else if (character == "eyra") PlayAudio(eyraIntroJawaban);
        else if (character == "pasa") PlayAudio(pasaIntroJawaban);
    }
    public void PlayGeneralCorrectAudio()
    {
        string character = GetActiveCharacter();

        PlayCatalogFeedback("feedback:benar", character);
    }

    public void PlayGeneralWrongAudio()
    {
        string character = GetActiveCharacter();

        PlayCatalogFeedback("feedback:salah", character);
    }

    // ==================================================
    // AUDIO SUBGAME UMUM
    // ==================================================

    // Tetap tersedia untuk controller yang sudah memakai API subgame.
    // Semua subgame kini memakai audio umum karakter aktif.
    public void PlaySubGameStartAudio(string category, string subGame)
    {
        string character = GetActiveCharacter();
        if (character == "kira") PlayAudio(kiraIntroSoal);
        else if (character == "eyra") PlayAudio(eyraIntroSoal);
        else if (character == "pasa") PlayAudio(pasaIntroSoal);
    }

    public void PlaySubGameCorrectAudio(string category, string subGame)
    {
        PlayGeneralCorrectAudio();
    }

    public void PlaySubGameWrongAudio(string category, string subGame)
    {
        PlayGeneralWrongAudio();
    }

    public string GetActiveCharacterName()
    {
        return GetActiveCharacter();
    }

    private string GetActiveCharacter()
    {
        if (scanRFIDController == null)
        {
            Debug.LogWarning(
                "[AudioManager] ScanRFIDController belum dihubungkan."
            );
            return "";
        }

        string character = NormalizeText(
            scanRFIDController.GetSelectedCharacter()
        );

        if (character == "kira" || character == "kelinci")
        {
            return "kira";
        }

        if (character == "eyra" || character == "elang")
        {
            return "eyra";
        }

        if (
            character == "pasa" ||
            character == "prabu" ||
            character == "penyu"
        )
        {
            return "pasa";
        }

        Debug.LogWarning(
            "[AudioManager] Nama karakter tidak dikenali: " +
            character
        );

        return "";
    }

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
}
