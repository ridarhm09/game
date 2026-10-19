using System.Collections;
using UnityEngine;

public class MiniGameAnswerController : MonoBehaviour
{
    [Header("UI Manager")]
    public UIManagerDonimal uiManager;

    [Header("Mini Game Controller")]
    public CategoryToMiniGameImageController miniGameController;

    [Header("Panel Check")]
    public GameObject miniGamePanel;

    [Header("Input Settings")]
    [Tooltip("Aktifkan untuk mencoba jawaban memakai keyboard.")]
    public bool enableKeyboardInput = true;

    [Tooltip("Aktifkan untuk menerima jawaban dan mengirim hasil melalui docking/ESP32.")]
    public bool enableDockingInput = true;

    [Header("Answer Feedback Objects")]
    public GameObject benarObject;
    public GameObject salahObject;

    [Header("Score Manager")]
    public ScoreManager scoreManager;

    [Header("Audio Manager")]
    public AudioManagerDonimal audioManager;

    [Header("Berburu Huruf")]
    [Tooltip(
        "Isi dengan GameObject yang memiliki BerburuHurufController."
    )]
    public BerburuQuestionController berburuQuestionController;

    [Header("Melengkapi Huruf")]
    [Tooltip(
        "Isi dengan GameObject yang memiliki LengkapiHurufQuestionController."
    )]
    public LengkapiQuestionController lengkapiQuestionController;

    [Header("Cari Angka")]
    [Tooltip(
        "Isi dengan GameObject yang memiliki CariQuestionController."
    )]
    public CariQuestionController cariQuestionController;

    [Header("Hitung Benda")]
    [Tooltip(
        "Isi dengan GameObject yang memiliki HitungQuestionController."
    )]
    public HitungQuestionController hitungQuestionController;

    [Header("Susun Angka")]
    [Tooltip(
        "Isi dengan GameObject yang memiliki SusunQuestionController."
    )]
    public SusunQuestionController susunQuestionController;

    [Header("Tebak Deskripsi Hewan")]
    public DeskripsiQuestionController deskripsiQuestionController;

    [Header("Tebak Suara Hewan")]
    public SuaraQuestionController suaraQuestionController;

    [Header("Tebak Bayangan Hewan")]
    public BayanganQuestionController bayanganQuestionController;

    [Header("Tebak Warna")]
    public TebakQuestionController tebakQuestionController;

    [Header("Feedback Duration")]
    public float feedbackDuration = 3f;

    [Header("Reward Duration")]
    public float rewardDuration = 5f;

    private bool isCheckingAnswer = false;
    private string keyboardTextBuffer = "";
    private string keyboardInputContext = "";

    private void Start()
    {
        HideAnswerFeedbackObjects();
    }

    private void Update()
    {
        if (!enableKeyboardInput)
        {
            return;
        }

        if (
            miniGamePanel == null ||
            !miniGamePanel.activeInHierarchy
        )
        {
            return;
        }

        if (isCheckingAnswer)
        {
            return;
        }

        HandleKeyboardInput();
    }

    private void HandleKeyboardInput()
    {
        string selectedCategory = NormalizeText(miniGameController.GetSelectedCategory());
        string selectedSubGame = NormalizeText(miniGameController.GetSelectedSubGame());

        bool usesWordInput =
            selectedCategory == "hewan" ||
            selectedCategory == "warna" ||
            selectedSubGame == "tebak_deskripsi" ||
            selectedSubGame == "tebak_suara" ||
            selectedSubGame == "tebak_bayangan" ||
            selectedSubGame == "tebak_warna";

        if (usesWordInput)
        {
            HandleBufferedKeyboardInput(selectedCategory, selectedSubGame);
            return;
        }

        keyboardTextBuffer = "";
        keyboardInputContext = "";

        // Huruf A-Z otomatis menjadi format huruf:x.
        for (
            KeyCode key = KeyCode.A;
            key <= KeyCode.Z;
            key++
        )
        {
            if (KeyboardInputUtility.WasPressed(key))
            {
                char letter =
                    (char)(
                        'a' +
                        (key - KeyCode.A)
                    );

                SubmitKeyboardAnswer(
                    "huruf:" + letter
                );

                return;
            }
        }

        // Angka atas 0-9.
        for (int number = 0; number <= 9; number++)
        {
            KeyCode alphaKey =
                KeyCode.Alpha0 + number;

            KeyCode keypadKey =
                KeyCode.Keypad0 + number;

            if (
                KeyboardInputUtility.WasPressed(alphaKey) ||
                KeyboardInputUtility.WasPressed(keypadKey)
            )
            {
                SubmitKeyboardAnswer(
                    "angka:" + number
                );

                return;
            }
        }
    }

    private void HandleBufferedKeyboardInput(string category, string subGame)
    {
        string context = category + ":" + subGame;
        if (keyboardInputContext != context)
        {
            keyboardInputContext = context;
            keyboardTextBuffer = "";
        }

        if (KeyboardInputUtility.WasPressed(KeyCode.Escape))
        {
            keyboardTextBuffer = "";
            Debug.Log("[AnswerController] Teks keyboard dikosongkan.");
            return;
        }

        if (KeyboardInputUtility.WasPressed(KeyCode.Backspace))
        {
            if (keyboardTextBuffer.Length > 0)
                keyboardTextBuffer = keyboardTextBuffer.Substring(0, keyboardTextBuffer.Length - 1);
            Debug.Log("[AnswerController] Teks keyboard: " + keyboardTextBuffer);
            return;
        }

        if (KeyboardInputUtility.WasPressed(KeyCode.Return) || KeyboardInputUtility.WasPressed(KeyCode.KeypadEnter))
        {
            string submittedText = keyboardTextBuffer.Trim();
            if (string.IsNullOrEmpty(submittedText)) return;

            keyboardTextBuffer = "";
            string prefix = category == "warna" || subGame == "tebak_warna"
                ? "warna:"
                : "hewan:";
            SubmitKeyboardAnswer(prefix + submittedText);
            return;
        }

        bool changed = false;

        for (KeyCode key = KeyCode.A; key <= KeyCode.Z; key++)
        {
            if (KeyboardInputUtility.WasPressed(key))
            {
                keyboardTextBuffer += (char)('a' + (key - KeyCode.A));
                changed = true;
            }
        }

        if (KeyboardInputUtility.WasPressed(KeyCode.Space) && keyboardTextBuffer.Length > 0 &&
            !keyboardTextBuffer.EndsWith(" "))
        {
            keyboardTextBuffer += " ";
            changed = true;
        }

        if (changed)
            Debug.Log("[AnswerController] Teks keyboard: " + keyboardTextBuffer + " (tekan Enter untuk menjawab)");
    }

    private void SubmitKeyboardAnswer(
        string answerData
    )
    {
        Debug.Log(
            "[AnswerController] Jawaban keyboard: " +
            answerData
        );

        CheckAnswer(answerData);
    }

    public void SubmitDockingAnswer(string answerData)
    {
        if (!enableDockingInput)
        {
            Debug.Log(
                "[AnswerController] Input docking diabaikan karena " +
                "Enable Docking Input sedang nonaktif."
            );
            return;
        }

        Debug.Log(
            "[AnswerController] Jawaban docking: " + answerData
        );

        CheckAnswer(answerData);
    }

    // Dipanggil oleh keyboard maupun RFID.
    public void CheckAnswer(string answerValue)
    {
        if (isCheckingAnswer)
        {
            Debug.Log(
                "[AnswerController] Jawaban diabaikan karena " +
                "feedback sedang berlangsung."
            );
            return;
        }

        if (string.IsNullOrWhiteSpace(answerValue))
        {
            Debug.LogWarning(
                "[AnswerController] Nilai jawaban kosong."
            );
            return;
        }

        if (miniGamePanel == null)
        {
            Debug.LogWarning(
                "[AnswerController] MiniGamePanel belum diisi."
            );
            return;
        }

        if (!miniGamePanel.activeInHierarchy)
        {
            Debug.Log(
                "[AnswerController] MiniGamePanel tidak aktif. " +
                "Input jawaban diabaikan."
            );
            return;
        }

        if (miniGameController == null)
        {
            Debug.LogWarning(
                "[AnswerController] MiniGameController belum dihubungkan."
            );
            return;
        }

        string input = NormalizeAnswerInput(answerValue);
        string selectedSubGame =
            NormalizeText(
                miniGameController.GetSelectedSubGame()
            );

        // Berburu Huruf memakai mekanisme pencarian beberapa huruf.
        // Input tidak dibandingkan dengan satu jawaban tetap.
        if (selectedSubGame == "berburu_huruf")
        {
            if (berburuQuestionController == null)
            {
                Debug.LogError(
                    "[AnswerController] " +
                    "BerburuHurufController belum diisi."
                );
                return;
            }

            Debug.Log(
                "[AnswerController] Input diteruskan ke " +
                "Berburu Huruf: " + input
            );

            berburuQuestionController.CheckLetter(input);
            return;
        }

        // Melengkapi Huruf mempunyai visual khusus:
        // jawaban benar mengganti kata tidak lengkap menjadi kata lengkap.
        if (selectedSubGame == "lengkapi_huruf")
        {
            if (lengkapiQuestionController == null)
            {
                Debug.LogError(
                    "[AnswerController] " +
                    "LengkapiHurufQuestionController belum diisi."
                );
                return;
            }

            Debug.Log(
                "[AnswerController] Input diteruskan ke " +
                "Melengkapi Huruf: " + input
            );

            lengkapiQuestionController.CheckLetter(input);
            return;
        }

        // Cari Angka memakai animasi flip khusus.
        if (selectedSubGame == "cari_angka")
        {
            if (cariQuestionController == null)
            {
                Debug.LogError(
                    "[AnswerController] " +
                    "CariQuestionController belum diisi."
                );
                return;
            }

            Debug.Log(
                "[AnswerController] Input diteruskan ke " +
                "Cari Angka: " + input
            );

            cariQuestionController.CheckNumber(input);
            return;
        }

        // Hitung Benda:
        // soal tetap terlihat, jawaban angka pop-up jika benar.
        if (selectedSubGame == "hitung_benda")
        {
            if (hitungQuestionController == null)
            {
                Debug.LogError(
                    "[AnswerController] " +
                    "HitungQuestionController belum diisi."
                );
                return;
            }

            Debug.Log(
                "[AnswerController] Input diteruskan ke " +
                "Hitung Benda: " + input
            );

            hitungQuestionController.CheckNumber(input);
            return;
        }

        // Menyusun Angka terdiri dari dua angka.
        // Angka muncul satu per satu dengan pop-up.
        if (selectedSubGame == "menyusun_angka")
        {
            if (susunQuestionController == null)
            {
                Debug.LogError(
                    "[AnswerController] " +
                    "SusunQuestionController belum diisi."
                );
                return;
            }

            Debug.Log(
                "[AnswerController] Input diteruskan ke " +
                "Susun Angka: " + input
            );

            susunQuestionController.CheckNumber(input);
            return;
        }

        if (selectedSubGame == "tebak_deskripsi")
        {
            if (deskripsiQuestionController == null)
            {
                Debug.LogError(
                    "[AnswerController] " +
                    "DeskripsiQuestionController belum diisi."
                );
                return;
            }

            Debug.Log(
                "[AnswerController] Input diteruskan ke " +
                "Tebak Deskripsi: " + input
            );

            deskripsiQuestionController.CheckAnimal(input);
            return;
        }

        if (selectedSubGame == "tebak_suara")
        {
            if (suaraQuestionController == null)
            {
                Debug.LogError(
                    "[AnswerController] " +
                    "SuaraQuestionController belum diisi."
                );
                return;
            }

            Debug.Log(
                "[AnswerController] Input diteruskan ke " +
                "Tebak Suara: " + input
            );

            suaraQuestionController.CheckAnimal(input);
            return;
        }

        if (selectedSubGame == "tebak_bayangan")
        {
            if (bayanganQuestionController == null)
            {
                bayanganQuestionController = FindFirstObjectByType<BayanganQuestionController>(
                    FindObjectsInactive.Include);
            }

            if (bayanganQuestionController == null)
            {
                Debug.LogError("[AnswerController] BayanganQuestionController belum ditemukan.");
                return;
            }

            Debug.Log("[AnswerController] Input diteruskan ke Tebak Bayangan: " + input);
            bayanganQuestionController.CheckAnimal(input);
            return;
        }

        if (selectedSubGame == "tebak_warna")
        {
            if (tebakQuestionController == null)
            {
                Debug.LogError(
                    "[AnswerController] " +
                    "TebakQuestionController belum diisi."
                );

                return;
            }

            Debug.Log(
                "[AnswerController] Input diteruskan ke " +
                "Tebak Warna: " + input
            );

            tebakQuestionController.CheckColor(input);
            return;
        }

        string expectedAnswer =
            NormalizeText(
                miniGameController
                    .GetCurrentCorrectAnswer()
            );

        // Subgame lama yang belum memakai QuestionController
        // tetap mempunyai jawaban sementara.
        if (string.IsNullOrEmpty(expectedAnswer))
        {
            expectedAnswer =
                GetTemporaryStaticAnswer(
                    selectedSubGame
                );
        }

        // Tebak Bunyi tidak boleh kembali diam-diam ke huruf A.
        if (
            selectedSubGame == "tebak_bunyi" &&
            string.IsNullOrEmpty(
                miniGameController
                    .GetCurrentCorrectAnswer()
            )
        )
        {
            Debug.LogError(
                "[AnswerController] Soal Tebak Bunyi belum memiliki " +
                "jawaban aktif. Periksa BunyiQuestionController."
            );
            return;
        }

        if (string.IsNullOrEmpty(expectedAnswer))
        {
            Debug.LogWarning(
                "[AnswerController] Jawaban benar untuk subgame " +
                selectedSubGame +
                " belum ditentukan."
            );
            return;
        }

        bool isCorrect =
            RfidAnswerUtility.Normalize(miniGameController.GetSelectedCategory(), input) ==
            RfidAnswerUtility.Normalize(miniGameController.GetSelectedCategory(),
                !string.IsNullOrWhiteSpace(miniGameController.GetCurrentCorrectAnswer())
                    ? miniGameController.GetCurrentCorrectAnswer() : expectedAnswer);

        Debug.Log(
            "=================================================="
        );
        Debug.Log(
            "[CEK JAWABAN] Subgame aktif: " +
            selectedSubGame
        );
        Debug.Log(
            "[CEK JAWABAN] Jawaban masuk: " +
            input
        );
        Debug.Log(
            "[CEK JAWABAN] Jawaban seharusnya: " +
            expectedAnswer
        );
        Debug.Log(
            "[CEK JAWABAN] Hasil: " +
            (isCorrect ? "BENAR" : "SALAH")
        );
        Debug.Log(
            "=================================================="
        );

        if (isCorrect)
        {
            StartCoroutine(
                ShowCorrectFeedback()
            );
        }
        else
        {
            StartCoroutine(
                ShowWrongFeedback()
            );
        }
    }

    private string GetTemporaryStaticAnswer(
        string selectedSubGame
    )
    {
        return "";
    }

    public void CompleteTebakWarna()
    {
        if (isCheckingAnswer)
        {
            return;
        }

        StartCoroutine(
            ShowCorrectFeedback()
        );
    }

    public void ShowTebakWarnaWrongFeedback()
    {
        if (isCheckingAnswer)
        {
            return;
        }

        StartCoroutine(
            ShowTebakWarnaWrongRoutine()
        );
    }

    private IEnumerator ShowTebakWarnaWrongRoutine()
    {
        DockingSerialReader.Instance?.SendAnswerLed(false);
        isCheckingAnswer = true;
        HideAnswerFeedbackObjects();

        if (uiManager != null)
        {
            uiManager
                .ShowFeedbackCharacterPanel();
        }

        if (salahObject != null)
        {
            salahObject.SetActive(true);
        }

        if (scoreManager != null)
        {
            scoreManager.AddWrongAnswer();
            scoreManager.UpdateAllUI();
        }

        PlayWrongAudio();

        yield return AudioManagerDonimal.WaitUntilFinished(audioManager, feedbackDuration);

        HideAnswerFeedbackObjects();

        // Satu soal hanya memiliki satu kesempatan; lanjut setelah salah.
        if (TryContinueCurrentSubGame())
        {
        }
        else
        {
            yield return StartCoroutine(ShowRewardThenDashboard());
        }

        isCheckingAnswer = false;
    }

    public void CompleteSuaraHewan()
    {
        if (isCheckingAnswer)
        {
            return;
        }

        StartCoroutine(
            ShowCorrectFeedback()
        );
    }

    public void ShowSuaraHewanWrongFeedback()
    {
        if (isCheckingAnswer)
        {
            return;
        }

        StartCoroutine(
            ShowSuaraHewanWrongRoutine()
        );
    }

    private IEnumerator ShowSuaraHewanWrongRoutine()
    {
        DockingSerialReader.Instance?.SendAnswerLed(false);
        isCheckingAnswer = true;
        HideAnswerFeedbackObjects();

        if (uiManager != null)
        {
            uiManager.ShowFeedbackCharacterPanel();
        }

        if (salahObject != null)
        {
            salahObject.SetActive(true);
        }

        if (scoreManager != null)
        {
            scoreManager.AddWrongAnswer();
            scoreManager.UpdateAllUI();
        }

        PlayWrongAudio();

        yield return AudioManagerDonimal.WaitUntilFinished(audioManager, feedbackDuration);

        HideAnswerFeedbackObjects();

        if (TryContinueCurrentSubGame())
        {
        }
        else
        {
            yield return StartCoroutine(ShowRewardThenDashboard());
        }

        isCheckingAnswer = false;
    }

    public void CompleteDeskripsiHewan()
    {
        if (isCheckingAnswer)
        {
            return;
        }

        StartCoroutine(
            ShowCorrectFeedback()
        );
    }

    public void ShowDeskripsiHewanWrongFeedback()
    {
        if (isCheckingAnswer)
        {
            return;
        }

        StartCoroutine(
            ShowDeskripsiHewanWrongRoutine()
        );
    }

    private IEnumerator ShowDeskripsiHewanWrongRoutine()
    {
        DockingSerialReader.Instance?.SendAnswerLed(false);
        isCheckingAnswer = true;
        HideAnswerFeedbackObjects();

        if (uiManager != null)
        {
            uiManager.ShowFeedbackCharacterPanel();
        }

        if (salahObject != null)
        {
            salahObject.SetActive(true);
        }

        if (scoreManager != null)
        {
            scoreManager.AddWrongAnswer();
            scoreManager.UpdateAllUI();
        }

        PlayWrongAudio();

        yield return AudioManagerDonimal.WaitUntilFinished(audioManager, feedbackDuration);

        HideAnswerFeedbackObjects();

        if (TryContinueCurrentSubGame())
        {
        }
        else
        {
            yield return StartCoroutine(ShowRewardThenDashboard());
        }

        isCheckingAnswer = false;
    }

    public void CompleteSusunAngka()
    {
        if (isCheckingAnswer)
        {
            return;
        }

        StartCoroutine(
            ShowCorrectFeedback()
        );
    }

    public void ShowSusunAngkaWrongFeedback()
    {
        if (isCheckingAnswer)
        {
            return;
        }

        StartCoroutine(
            ShowSusunAngkaWrongRoutine()
        );
    }

    private IEnumerator ShowSusunAngkaWrongRoutine()
    {
        DockingSerialReader.Instance?.SendAnswerLed(false);
        isCheckingAnswer = true;
        HideAnswerFeedbackObjects();

        if (uiManager != null)
        {
            uiManager
                .ShowFeedbackCharacterPanel();
        }

        if (salahObject != null)
        {
            salahObject.SetActive(true);
        }

        if (scoreManager != null)
        {
            scoreManager.AddWrongAnswer();
            scoreManager.UpdateAllUI();
        }

        PlayWrongAudio();

        yield return AudioManagerDonimal.WaitUntilFinished(audioManager, feedbackDuration);

        HideAnswerFeedbackObjects();

        // Satu soal hanya memiliki satu kesempatan; lanjut setelah salah.
        if (TryContinueCurrentSubGame())
        {
        }
        else
        {
            yield return StartCoroutine(ShowRewardThenDashboard());
        }

        isCheckingAnswer = false;
    }

    public void CompleteHitungBenda()
    {
        if (isCheckingAnswer)
        {
            return;
        }

        StartCoroutine(
            ShowCorrectFeedback()
        );
    }

    public void ShowHitungBendaWrongFeedback()
    {
        if (isCheckingAnswer)
        {
            return;
        }

        StartCoroutine(
            ShowHitungBendaWrongRoutine()
        );
    }

    private IEnumerator ShowHitungBendaWrongRoutine()
    {
        DockingSerialReader.Instance?.SendAnswerLed(false);
        isCheckingAnswer = true;
        HideAnswerFeedbackObjects();

        if (uiManager != null)
        {
            uiManager.ShowFeedbackCharacterPanel();
        }

        if (salahObject != null)
        {
            salahObject.SetActive(true);
        }

        if (scoreManager != null)
        {
            scoreManager.AddWrongAnswer();
            scoreManager.UpdateAllUI();
        }

        PlayWrongAudio();

        yield return AudioManagerDonimal.WaitUntilFinished(audioManager, feedbackDuration);

        HideAnswerFeedbackObjects();

        // Satu soal hanya memiliki satu kesempatan; lanjut setelah salah.
        if (TryContinueCurrentSubGame())
        {
        }
        else
        {
            yield return StartCoroutine(ShowRewardThenDashboard());
        }

        isCheckingAnswer = false;
    }

    public void CompleteCariAngka()
    {
        if (isCheckingAnswer)
        {
            return;
        }

        StartCoroutine(
            ShowCorrectFeedback()
        );
    }

    public void ShowCariAngkaWrongFeedback()
    {
        if (isCheckingAnswer)
        {
            return;
        }

        StartCoroutine(
            ShowCariAngkaWrongRoutine()
        );
    }

    private IEnumerator ShowCariAngkaWrongRoutine()
    {
        DockingSerialReader.Instance?.SendAnswerLed(false);
        isCheckingAnswer = true;
        HideAnswerFeedbackObjects();

        if (uiManager != null)
        {
            uiManager.ShowFeedbackCharacterPanel();
        }

        if (salahObject != null)
        {
            salahObject.SetActive(true);
        }

        if (scoreManager != null)
        {
            scoreManager.AddWrongAnswer();
            scoreManager.UpdateAllUI();
        }

        PlayWrongAudio();

        yield return AudioManagerDonimal.WaitUntilFinished(audioManager, feedbackDuration);

        HideAnswerFeedbackObjects();

        // Satu soal hanya memiliki satu kesempatan; lanjut setelah salah.
        if (TryContinueCurrentSubGame())
        {
        }
        else
        {
            yield return StartCoroutine(ShowRewardThenDashboard());
        }

        isCheckingAnswer = false;
    }

    public void CompleteLengkapiHuruf()
    {
        if (isCheckingAnswer)
        {
            return;
        }

        StartCoroutine(
            ShowCorrectFeedback()
        );
    }

    public void ShowLengkapiHurufWrongFeedback()
    {
        if (isCheckingAnswer)
        {
            return;
        }

        StartCoroutine(
            ShowLengkapiHurufWrongRoutine()
        );
    }

    private IEnumerator ShowLengkapiHurufWrongRoutine()
    {
        DockingSerialReader.Instance?.SendAnswerLed(false);
        isCheckingAnswer = true;
        HideAnswerFeedbackObjects();

        if (uiManager != null)
        {
            uiManager.ShowFeedbackCharacterPanel();
        }

        if (salahObject != null)
        {
            salahObject.SetActive(true);
        }

        if (scoreManager != null)
        {
            scoreManager.AddWrongAnswer();
            scoreManager.UpdateAllUI();
        }

        PlayWrongAudio();

        yield return AudioManagerDonimal.WaitUntilFinished(audioManager, feedbackDuration);

        HideAnswerFeedbackObjects();

        // Satu soal hanya memiliki satu kesempatan; lanjut setelah salah.
        if (TryContinueCurrentSubGame())
        {
        }
        else
        {
            yield return StartCoroutine(ShowRewardThenDashboard());
        }

        isCheckingAnswer = false;
    }

    public void CompleteBerburuHuruf()
    {
        if (isCheckingAnswer)
        {
            Debug.Log(
                "[AnswerController] Reward Berburu Huruf diabaikan " +
                "karena feedback sedang berlangsung."
            );
            return;
        }

        StartCoroutine(
            ShowBerburuHurufReward()
        );
    }

    private IEnumerator ShowBerburuHurufReward()
    {
        DockingSerialReader.Instance?.SendAnswerLed(true);
        isCheckingAnswer = true;
        HideAnswerFeedbackObjects();

        // Tampilkan feedback benar menggunakan panel yang sudah ada.
        if (uiManager != null)
        {
            uiManager.ShowFeedbackCharacterPanel();
        }
        else
        {
            Debug.LogWarning(
                "[AnswerController] UIManager belum diisi."
            );
        }

        if (benarObject != null)
        {
            benarObject.SetActive(true);
        }

        // Tambahkan poin hanya satu kali setelah semua huruf ditemukan.
        if (scoreManager != null)
        {
            scoreManager.AddCorrectAnswer();
            scoreManager.UpdateAllUI();
        }
        else
        {
            Debug.LogWarning(
                "[AnswerController] ScoreManager belum diisi."
            );
        }

        Debug.Log(
            "[AnswerController] Berburu Huruf selesai. " +
            "Menampilkan feedback benar."
        );

        yield return new WaitForSeconds(
            feedbackDuration
        );

        HideAnswerFeedbackObjects();

        if (TryContinueCurrentSubGame())
        {

            isCheckingAnswer = false;
            yield break;
        }

        RecordSubGameAchievement();

        // Tampilkan RewardPanel tanpa audio.
        if (scoreManager != null)
        {
            scoreManager.UpdateAllUI();
        }

        if (uiManager != null)
        {
            uiManager.ShowRewardPanel();
        }

        Debug.Log(
            "[AnswerController] Reward Berburu Huruf tampil selama " +
            rewardDuration +
            " detik."
        );

        yield return new WaitForSeconds(
            rewardDuration
        );

        if (scoreManager != null)
        {
            scoreManager.UpdateAllUI();
        }

        if (uiManager != null)
        {
            uiManager.ShowDashboardPanel();
        }

        isCheckingAnswer = false;
    }

    public void ShowCorrectFeedbackFromServer()
    {
        if (isCheckingAnswer)
        {
            return;
        }

        StartCoroutine(
            ShowCorrectFeedback()
        );
    }

    public void ShowWrongFeedbackFromServer()
    {
        if (isCheckingAnswer)
        {
            return;
        }

        StartCoroutine(
            ShowWrongFeedback()
        );
    }

    private IEnumerator ShowCorrectFeedback()
    {
        DockingSerialReader.Instance?.SendAnswerLed(true);
        isCheckingAnswer = true;
        HideAnswerFeedbackObjects();

        if (uiManager != null)
        {
            uiManager
                .ShowFeedbackCharacterPanel();
        }
        else
        {
            Debug.LogWarning(
                "[AnswerController] UIManager belum diisi."
            );
        }

        if (benarObject != null)
        {
            benarObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning(
                "[AnswerController] Benar Object belum diisi."
            );
        }

        if (scoreManager != null)
        {
            scoreManager.AddCorrectAnswer();
            scoreManager.UpdateAllUI();
        }
        else
        {
            Debug.LogWarning(
                "[AnswerController] ScoreManager belum diisi."
            );
        }

        PlayCorrectAudio();

        yield return AudioManagerDonimal.WaitUntilFinished(audioManager, feedbackDuration);

        HideAnswerFeedbackObjects();

        if (TryContinueCurrentSubGame())
        {

            isCheckingAnswer = false;
            yield break;
        }

        yield return StartCoroutine(
            ShowRewardThenDashboard()
        );

        isCheckingAnswer = false;
    }

    private bool TryContinueCurrentSubGame()
    {
        if (miniGameController == null)
        {
            Debug.LogWarning("[AnswerController] MiniGameController belum diisi; soal berikutnya tidak dapat dimulai.");
            return false;
        }

        if (uiManager != null)
        {
            uiManager.ShowMiniGamePanel();
        }

        bool continued = miniGameController.TryShowNextQuestionInSelectedSubGame();
        Debug.Log(
            continued
                ? "[AnswerController] Audio feedback selesai. Lanjut ke soal berikutnya."
                : "[AnswerController] Tidak ada soal berikutnya. Menampilkan reward."
        );
        return continued;
    }

    private IEnumerator ShowWrongFeedback()
    {
        DockingSerialReader.Instance?.SendAnswerLed(false);
        isCheckingAnswer = true;
        HideAnswerFeedbackObjects();

        if (uiManager != null)
        {
            uiManager
                .ShowFeedbackCharacterPanel();
        }
        else
        {
            Debug.LogWarning(
                "[AnswerController] UIManager belum diisi."
            );
        }

        if (salahObject != null)
        {
            salahObject.SetActive(true);
        }
        else
        {
            Debug.LogWarning(
                "[AnswerController] Salah Object belum diisi."
            );
        }

        if (scoreManager != null)
        {
            scoreManager.AddWrongAnswer();
            scoreManager.UpdateAllUI();
        }
        else
        {
            Debug.LogWarning(
                "[AnswerController] ScoreManager belum diisi."
            );
        }

        PlayWrongAudio();

        yield return AudioManagerDonimal.WaitUntilFinished(audioManager, feedbackDuration);

        HideAnswerFeedbackObjects();

        if (TryContinueCurrentSubGame())
        {

            isCheckingAnswer = false;
            yield break;
        }

        yield return StartCoroutine(
            ShowRewardThenDashboard()
        );

        isCheckingAnswer = false;
    }

    private void PlayCorrectAudio()
    {
        if (audioManager == null)
        {
            return;
        }

        if (miniGameController == null)
        {
            audioManager
                .PlayGeneralCorrectAudio();
            return;
        }

        string selectedCategory =
            miniGameController
                .GetSelectedCategory();

        string selectedSubGame =
            miniGameController
                .GetSelectedSubGame();

        if (
            !string.IsNullOrEmpty(
                selectedCategory
            ) &&
            !string.IsNullOrEmpty(
                selectedSubGame
            )
        )
        {
            audioManager
                .PlaySubGameCorrectAudio(
                    selectedCategory,
                    selectedSubGame
                );
        }
        else
        {
            audioManager
                .PlayGeneralCorrectAudio();
        }
    }

    private void PlayWrongAudio()
    {
        if (audioManager == null)
        {
            return;
        }

        if (miniGameController == null)
        {
            audioManager
                .PlayGeneralWrongAudio();
            return;
        }

        string selectedCategory =
            miniGameController
                .GetSelectedCategory();

        string selectedSubGame =
            miniGameController
                .GetSelectedSubGame();

        if (
            !string.IsNullOrEmpty(
                selectedCategory
            ) &&
            !string.IsNullOrEmpty(
                selectedSubGame
            )
        )
        {
            audioManager
                .PlaySubGameWrongAudio(
                    selectedCategory,
                    selectedSubGame
                );
        }
        else
        {
            audioManager
                .PlayGeneralWrongAudio();
        }
    }

    private IEnumerator ShowRewardThenDashboard()
    {
        if (miniGameController != null && scoreManager != null)
        {
            DonimalAPI.Instance?.SubmitActivity(scoreManager.GetPoint(), scoreManager.GetCorrectCount());
        }
        RecordSubGameAchievement();

        if (scoreManager != null)
        {
            scoreManager.UpdateAllUI();
        }

        if (uiManager != null)
        {
            uiManager.ShowRewardPanel();
        }

        if (audioManager != null)
        {
            audioManager.PlayRewardAudio();
        }

        Debug.Log(
            "[AnswerController] RewardPanel tampil selama " +
            rewardDuration +
            " detik."
        );

        yield return AudioManagerDonimal.WaitUntilFinished(audioManager, rewardDuration);

        if (scoreManager != null)
        {
            scoreManager.UpdateAllUI();
        }

        if (uiManager != null)
        {
            uiManager.ShowDashboardPanel();
        }
    }

    private void RecordSubGameAchievement()
    {
        if (AchievementManager.Instance == null ||
            miniGameController == null ||
            scoreManager == null)
        {
            return;
        }

        AchievementManager.Instance.CompleteSubGame(
            miniGameController.GetSelectedCategory(),
            miniGameController.GetSelectedSubGame(),
            scoreManager.GetCorrectCount(),
            scoreManager.GetWrongCount(),
            scoreManager.GetPoint()
        );
    }

    private void HideAnswerFeedbackObjects()
    {
        // Feedback jawaban berada di panel feedback, bukan teks jumlah di RewardPanel.
        if (uiManager != null && uiManager.feedbackCharacterPanel != null)
        {
            Transform feedback = uiManager.feedbackCharacterPanel.transform.Find("AnswerFeedback");
            if (feedback != null)
            {
                benarObject = feedback.Find("benar")?.gameObject;
                salahObject = feedback.Find("salah")?.gameObject;
                feedback.gameObject.SetActive(true);
            }
        }
        if (benarObject != null)
        {
            benarObject.SetActive(false);
        }

        if (salahObject != null)
        {
            salahObject.SetActive(false);
        }
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

    private string NormalizeAnswerInput(string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        return value.ToLowerInvariant().Trim();
    }
}
