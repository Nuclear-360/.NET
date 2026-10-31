using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace Nuclear360.Curadoria.Domain;

/// <summary>Utilitários de texto (busca sem acento/maiúscula, exibição de enums).</summary>
public static class Texto
{
    /// <summary>"Saúde  Nuclear" → "saude nuclear".</summary>
    public static string Normalizar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor)) return "";

        var decomposto = valor.Trim().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
                sb.Append(c);
        }

        var semAcento = sb.ToString().Normalize(NormalizationForm.FormC).ToLowerInvariant();
        return string.Join(' ', semAcento.Split(' ', StringSplitOptions.RemoveEmptyEntries));
    }

    /// <summary>EmRevisao → EM_REVISAO (mesmo formato usado no banco do projeto).</summary>
    public static string Codigo(Enum valor) =>
        Regex.Replace(valor.ToString(), "(?<!^)([A-Z])", "_$1").ToUpperInvariant();

    public static string Truncar(string? valor, int max)
    {
        valor ??= "";
        return valor.Length <= max ? valor : valor[..(max - 1)] + "…";
    }
}
