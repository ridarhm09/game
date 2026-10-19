using UnityEngine;

public class CharacterVisualController : MonoBehaviour
{
    [Header("Scan RFID Controller")]
    public ScanRFIDController scanRFIDController;

    [Header("Kira Backgrounds")]
    public GameObject[] kiraBackgrounds;

    [Header("Eyra Backgrounds")]
    public GameObject[] eyraBackgrounds;

    [Header("Pasa Backgrounds")]
    public GameObject[] pasaBackgrounds;

    [Header("Kira Characters")]
    public GameObject[] kiraCharacters;

    [Header("Eyra Characters")]
    public GameObject[] eyraCharacters;

    [Header("Pasa Characters")]
    public GameObject[] pasaCharacters;

    private string lastCharacter = "";

    private void Start()
    {
        HideAllVisuals();
    }

    private void Update()
    {
        if (scanRFIDController == null)
        {
            return;
        }

        string selectedCharacter = scanRFIDController.GetSelectedCharacter();

        if (selectedCharacter != lastCharacter)
        {
            lastCharacter = selectedCharacter;
            UpdateVisualByCharacter(selectedCharacter);
        }
    }

    public void UpdateVisualByCharacter(string selectedCharacter)
    {
        HideAllVisuals();

        if (selectedCharacter == "kira")
        {
            SetActiveArray(kiraBackgrounds, true);
            SetActiveArray(kiraCharacters, true);
            Debug.Log("Visual aktif: Kira + background darat");
        }
        else if (selectedCharacter == "eyra")
        {
            SetActiveArray(eyraBackgrounds, true);
            SetActiveArray(eyraCharacters, true);
            Debug.Log("Visual aktif: Eyra + background udara");
        }
        else if (selectedCharacter == "pasa")
        {
            SetActiveArray(pasaBackgrounds, true);
            SetActiveArray(pasaCharacters, true);
            Debug.Log("Visual aktif: Pasa + background air");
        }
        else
        {
            Debug.Log("Belum ada karakter yang dipilih.");
        }
    }

    private void HideAllVisuals()
    {
        SetActiveArray(kiraBackgrounds, false);
        SetActiveArray(eyraBackgrounds, false);
        SetActiveArray(pasaBackgrounds, false);

        SetActiveArray(kiraCharacters, false);
        SetActiveArray(eyraCharacters, false);
        SetActiveArray(pasaCharacters, false);
    }

    private void SetActiveArray(GameObject[] objects, bool active)
    {
        if (objects == null)
        {
            return;
        }

        foreach (GameObject obj in objects)
        {
            if (obj != null)
            {
                obj.SetActive(active);
            }
        }
    }
}