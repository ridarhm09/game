using System;

// Dipakai untuk kode kartu dan correct_answer server agar perbandingannya sama.
public static class RfidAnswerUtility
{
    public static string Normalize(string category, string value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        category = (category ?? "").Trim().ToLowerInvariant();
        if (category == "animal") category = "hewan";
        string result = value.Trim().ToLowerInvariant();
        string[] prefixes;
        switch (category)
        {
            case "angka": prefixes = new[] { "angka:", "angka ", "ang-" }; break;
            case "huruf": prefixes = new[] { "huruf:", "huruf ", "hrf-" }; break;
            case "hewan": prefixes = new[] { "hewan:", "hewan ", "animal:", "animal ", "hwn-" }; break;
            case "warna": prefixes = new[] { "warna:", "warna ", "wrn-" }; break;
            default: return result;
        }
        // Contoh input berlapis: angka:ANG-2.
        bool removed;
        do
        {
            removed = false;
            foreach (string prefix in prefixes)
            {
                if (!result.StartsWith(prefix, StringComparison.Ordinal)) continue;
                result = result.Substring(prefix.Length).Trim();
                removed = true;
                break;
            }
        } while (removed);
        if (category == "warna" && (result == "orange" || result == "oranye"))
            result = "jingga";
        return category == "hewan" ? result : result.Replace(" ", "");
    }
}
