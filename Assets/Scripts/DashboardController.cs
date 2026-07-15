using UnityEngine;

public class DashboardController : MonoBehaviour
{
    [Header("UI Manager")]
    public UIManagerDonimal uiManager;

    [Header("Category Panel Groups")]
    public GameObject hurufGroup;
    public GameObject angkaGroup;
    public GameObject hewanGroup;
    public GameObject warnaGroup;

    private string selectedCategory = "";

    private void Start()
    {
        HideAllCategoryGroups();
    }

    private void Update()
    {
        // Keyboard simulasi untuk memilih kategori dari Dashboard
        if (Input.GetKeyDown(KeyCode.H))
        {
            SelectCategory("huruf");
        }

        if (Input.GetKeyDown(KeyCode.A))
        {
            SelectCategory("angka");
        }

        if (Input.GetKeyDown(KeyCode.N))
        {
            SelectCategory("hewan");
        }

        if (Input.GetKeyDown(KeyCode.W))
        {
            SelectCategory("warna");
        }
    }

    public void SelectCategory(string category)
    {
        selectedCategory = category.ToLower().Trim();

        HideAllCategoryGroups();

        if (selectedCategory == "huruf")
        {
            if (hurufGroup != null) hurufGroup.SetActive(true);
            Debug.Log("Kategori Huruf dipilih.");
        }
        else if (selectedCategory == "angka")
        {
            if (angkaGroup != null) angkaGroup.SetActive(true);
            Debug.Log("Kategori Angka dipilih.");
        }
        else if (selectedCategory == "hewan")
        {
            if (hewanGroup != null) hewanGroup.SetActive(true);
            Debug.Log("Kategori Hewan dipilih.");
        }
        else if (selectedCategory == "warna")
        {
            if (warnaGroup != null) warnaGroup.SetActive(true);
            Debug.Log("Kategori Warna dipilih.");
        }
        else
        {
            Debug.LogWarning("Kategori tidak dikenali.");
            return;
        }

        if (uiManager != null)
        {
            uiManager.ShowCategoryPanel();
        }
    }

    private void HideAllCategoryGroups()
    {
        if (hurufGroup != null) hurufGroup.SetActive(false);
        if (angkaGroup != null) angkaGroup.SetActive(false);
        if (hewanGroup != null) hewanGroup.SetActive(false);
        if (warnaGroup != null) warnaGroup.SetActive(false);
    }

    public string GetSelectedCategory()
    {
        return selectedCategory;
    }
}