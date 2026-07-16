using UnityEngine;

public class CategoryToMiniGameImageController : MonoBehaviour
{
    [Header("UI Manager")]
    public UIManagerDonimal uiManager;

    [Header("Dashboard Controller")]
    public DashboardController dashboardController;

    [Header("Soal Huruf")]
    public GameObject soalHuruf1;
    public GameObject soalHuruf2;
    public GameObject soalHuruf3;

    private string selectedCategory = "";
    private string selectedSubGame = "";

    private void Start()
    {
        HideAllSoal();
    }

    private void Update()
    {
        // Simulasi pilih subgame pakai keyboard
        if (Input.GetKeyDown(KeyCode.Alpha1))
        {
            SelectSubGame(1);
        }

        if (Input.GetKeyDown(KeyCode.Alpha2))
        {
            SelectSubGame(2);
        }

        if (Input.GetKeyDown(KeyCode.Alpha3))
        {
            SelectSubGame(3);
        }
    }

    public void SelectSubGame(int subGameNumber)
    {
        if (dashboardController == null)
        {
            Debug.LogWarning("DashboardController belum dihubungkan.");
            return;
        }

        selectedCategory = dashboardController.GetSelectedCategory();

        if (selectedCategory == "")
        {
            Debug.LogWarning("Kategori belum dipilih.");
            return;
        }

        HideAllSoal();

        if (selectedCategory == "huruf")
        {
            SelectHurufSubGame(subGameNumber);
        }
        else
        {
            Debug.LogWarning("Untuk sekarang baru kategori huruf yang dibuat.");
            return;
        }

        if (uiManager != null)
        {
            uiManager.ShowMiniGamePanel();
        }
    }

    private void SelectHurufSubGame(int number)
    {
        if (number == 1)
        {
            selectedSubGame = "tebak_bunyi";

            if (soalHuruf1 != null)
            {
                soalHuruf1.SetActive(true);
            }

            Debug.Log("Masuk ke Soal Huruf 1 / Tebak Bunyi");
        }
        else if (number == 2)
        {
            selectedSubGame = "lengkapi_huruf";

            if (soalHuruf2 != null)
            {
                soalHuruf2.SetActive(true);
            }

            Debug.Log("Masuk ke Soal Huruf 2 / Lengkapi Huruf");
        }
        else if (number == 3)
        {
            selectedSubGame = "berburu_huruf";

            if (soalHuruf3 != null)
            {
                soalHuruf3.SetActive(true);
            }

            Debug.Log("Masuk ke Soal Huruf 3 / Berburu Huruf");
        }
        else
        {
            Debug.LogWarning("Sub game huruf tidak tersedia.");
        }
    }

    private void HideAllSoal()
    {
        if (soalHuruf1 != null) soalHuruf1.SetActive(false);
        if (soalHuruf2 != null) soalHuruf2.SetActive(false);
        if (soalHuruf3 != null) soalHuruf3.SetActive(false);
    }

    public string GetSelectedCategory()
    {
        return selectedCategory;
    }

    public string GetSelectedSubGame()
    {
        return selectedSubGame;
    }
}