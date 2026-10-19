using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

// Perubahan melalui objek Unity mempertahankan isian Inspector yang belum disimpan.
[InitializeOnLoad]
public static class BelajarAudioCatalogSetup
{
    static BelajarAudioCatalogSetup()
    {
        EditorApplication.delayCall += EnsureCategory;
        EditorApplication.playModeStateChanged += state =>
        {
            if (state == PlayModeStateChange.EnteredEditMode) EnsureCategory();
        };
    }

    private static void EnsureCategory()
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode) return;
        var catalog = AssetDatabase.LoadAssetAtPath<LocalQuestionAudioCatalog>(
            "Assets/Resources/LocalQuestionAudioCatalog.asset");
        if (catalog == null) return;
        if (catalog.categories != null && catalog.categories.Exists(c => c != null && c.name == "Belajar")) return;

        Undo.RecordObject(catalog, "Tambahkan kategori Belajar");
        if (catalog.categories == null) catalog.categories = new List<LocalQuestionAudioCatalog.Category>();
        var category = new LocalQuestionAudioCatalog.Category { name = "Belajar" };
        var letters = new List<string>();
        for (char letter = 'A'; letter <= 'Z'; letter++) letters.Add(letter.ToString());
        AddGroup(category, "Huruf", letters);
        AddGroup(category, "Angka", new[] { "0", "1", "2", "3", "4", "5", "6", "7", "8", "9" });
        AddGroup(category, "Warna", new[] { "Biru", "Hijau", "Jingga", "Kuning", "Merah", "Ungu" });
        AddGroup(category, "Hewan", new[] { "Anjing", "Ayam", "Babi", "Bebek", "Jangkrik", "Kambing", "Katak", "Kucing", "Monyet", "Sapi" });
        catalog.categories.Add(category);
        EditorUtility.SetDirty(catalog);
        AssetDatabase.SaveAssetIfDirty(catalog);
        Debug.Log("[Belajar] Kategori Belajar ditambahkan ke katalog: Huruf, Angka, Warna, Hewan.");
    }

    private static void AddGroup(LocalQuestionAudioCatalog.Category category, string name, IEnumerable<string> values)
    {
        var group = new LocalQuestionAudioCatalog.Subgame { name = name };
        foreach (string value in values)
        {
            group.questions.Add(new LocalQuestionAudioCatalog.Entry
            {
                label = "Belajar " + value,
                questionId = "belajar:" + name.ToLowerInvariant() + ":" + value.ToLowerInvariant(),
                kira = LoadClip("Kira", "KIRA", value),
                eyra = LoadClip("Eyra", "EYRA", value),
                pasa = LoadClip("Pasa 1", "PASA", value)
            });
        }
        category.subgames.Add(group);
    }

    private static AudioClip LoadClip(string folder, string role, string value)
    {
        string path = "Assets/Resources/QuestionAudio/" + folder + "/belajar/" + role + "_Belajar" + value + ".mp3";
        AudioClip clip = AssetDatabase.LoadAssetAtPath<AudioClip>(path);
        if (clip == null) Debug.LogWarning("[Belajar] Audio belum tersedia: " + path);
        return clip;
    }
}
