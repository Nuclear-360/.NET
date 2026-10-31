namespace Nuclear360.Curadoria.Domain;

public class Alternativa
{
    public string Texto { get; set; } = "";
    public bool Correta { get; set; }

    public Alternativa() { }

    public Alternativa(string texto, bool correta)
    {
        Texto = texto;
        Correta = correta;
    }
}
