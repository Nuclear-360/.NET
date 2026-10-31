namespace Nuclear360.Curadoria.Domain;

/// <summary>Fonte oficial que sustenta uma pergunta (CNEN, AIEA, AMAZUL...).</summary>
public class Referencia
{
    public const int AnoMinimo = 1900;

    public string Titulo { get; set; } = "";
    public string Instituicao { get; set; } = "";
    public string Url { get; set; } = "";
    public int Ano { get; set; }

    public Referencia() { }

    public Referencia(string titulo, string instituicao, string url, int ano)
    {
        Titulo = titulo;
        Instituicao = instituicao;
        Url = url;
        Ano = ano;
    }

    public List<string> Validar(int anoAtual)
    {
        var erros = new List<string>();

        var titulo = (Titulo ?? "").Trim();
        if (titulo.Length < 3 || titulo.Length > 200)
            erros.Add("Título da referência deve ter entre 3 e 200 caracteres.");

        var inst = (Instituicao ?? "").Trim();
        if (inst.Length < 2 || inst.Length > 100)
            erros.Add("Instituição da referência deve ter entre 2 e 100 caracteres.");

        var okUrl = Uri.TryCreate((Url ?? "").Trim(), UriKind.Absolute, out var uri)
                    && (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps);
        if (!okUrl)
            erros.Add("URL da referência deve ser um endereço http:// ou https:// válido.");

        if (Ano < AnoMinimo || Ano > anoAtual)
            erros.Add($"Ano da referência deve estar entre {AnoMinimo} e {anoAtual}.");

        return erros;
    }

    public string UrlNormalizada() => (Url ?? "").Trim().TrimEnd('/').ToLowerInvariant();
}
