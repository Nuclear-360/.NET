using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Text.Json.Serialization;
using Nuclear360.Curadoria.Domain;

namespace Nuclear360.Curadoria.Repositories;

public static class JsonConfig
{
    public static readonly JsonSerializerOptions Opcoes = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        Encoder = JavaScriptEncoder.UnsafeRelaxedJsonEscaping, // mantém acentos legíveis no arquivo
        Converters = { new JsonStringEnumConverter() }         // grava "EmRevisao" em vez de 2
    };
}

/// <summary>Persistência em arquivo JSON (os dados sobrevivem entre execuções).</summary>
public sealed class JsonPerguntaRepository : IPerguntaRepository
{
    private readonly string _caminho;

    /// <summary>Preenchido quando um arquivo corrompido foi isolado ao carregar.</summary>
    public string? Aviso { get; private set; }

    public JsonPerguntaRepository(string caminho)
    {
        if (string.IsNullOrWhiteSpace(caminho))
            throw new ArgumentException("Caminho do arquivo de dados não informado.", nameof(caminho));
        _caminho = caminho;
    }

    public string Caminho => _caminho;

    public List<Pergunta> Carregar()
    {
        if (!File.Exists(_caminho)) return new List<Pergunta>();

        try
        {
            var json = File.ReadAllText(_caminho, Encoding.UTF8);
            if (string.IsNullOrWhiteSpace(json)) return new List<Pergunta>();

            var lista = JsonSerializer.Deserialize<List<Pergunta>>(json, JsonConfig.Opcoes)
                        ?? throw new JsonException("Conteúdo vazio.");
            return lista;
        }
        catch (JsonException)
        {
            // Arquivo ilegível: guarda uma cópia para análise e segue com base vazia (não derruba o app).
            var backup = $"{_caminho}.corrompido-{DateTime.Now:yyyyMMddHHmmss}";
            try
            {
                File.Move(_caminho, backup);
                Aviso = $"O arquivo de dados estava corrompido e foi movido para '{backup}'. Iniciando com base vazia.";
                return new List<Pergunta>();
            }
            catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
            {
                throw new PersistenciaException($"Arquivo de dados corrompido e não foi possível isolá-lo: {ex.Message}", ex);
            }
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            throw new PersistenciaException($"Não foi possível ler '{_caminho}': {ex.Message}", ex);
        }
    }

    public void Salvar(IReadOnlyList<Pergunta> perguntas)
    {
        try
        {
            var pasta = Path.GetDirectoryName(Path.GetFullPath(_caminho));
            if (!string.IsNullOrEmpty(pasta)) Directory.CreateDirectory(pasta);

            // Escreve em arquivo temporário e troca: evita deixar o JSON pela metade se algo falhar.
            var temporario = _caminho + ".tmp";
            var json = JsonSerializer.Serialize(perguntas, JsonConfig.Opcoes);
            File.WriteAllText(temporario, json, new UTF8Encoding(false));
            File.Move(temporario, _caminho, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or NotSupportedException)
        {
            throw new PersistenciaException($"Não foi possível gravar '{_caminho}': {ex.Message}", ex);
        }
    }
}
