using System.Text.Json;
using Nuclear360.Curadoria.Domain;
using Nuclear360.Curadoria.Repositories;

namespace Nuclear360.Curadoria.Services;

/// <summary>
/// Casos de uso da curadoria: cadastro, fluxo de aprovação e consulta.
/// Toda alteração é aplicada numa cópia, gravada e só então assumida em memória,
/// então uma falha de disco nunca deixa memória e arquivo divergentes.
/// </summary>
public sealed class CuradoriaService
{
    private readonly IPerguntaRepository _repositorio;
    private readonly Func<DateTime> _agora;
    private List<Pergunta> _perguntas;

    public CuradoriaService(IPerguntaRepository repositorio, Func<DateTime>? relogio = null)
    {
        _repositorio = repositorio;
        _agora = relogio ?? (() => DateTime.UtcNow);
        _perguntas = _repositorio.Carregar();
    }

    // ------------------------------------------------------------ Cadastro
    public Pergunta Cadastrar(
        Usuario autor,
        string enunciado,
        AreaDistrito area,
        string explicacao,
        IEnumerable<Alternativa> alternativas,
        IEnumerable<Referencia>? referencias = null)
    {
        if (!autor.PodeCadastrar)
            throw new RegraNegocioException("Perfil JOGADOR não pode criar ou alterar perguntas.");

        var agora = _agora();
        var nova = new Pergunta
        {
            Id = ProximoId(),
            Enunciado = (enunciado ?? "").Trim(),
            Area = area,
            Explicacao = (explicacao ?? "").Trim(),
            Alternativas = (alternativas ?? Enumerable.Empty<Alternativa>())
                .Select(a => new Alternativa((a.Texto ?? "").Trim(), a.Correta)).ToList(),
            Status = StatusPergunta.Rascunho,
            CriadoPor = autor.Apelido,
            CriadoEm = agora
        };

        var erros = nova.Validar();
        if (erros.Count > 0) throw new ValidacaoException(erros);

        var chave = Texto.Normalizar(nova.Enunciado);
        if (_perguntas.Any(p => Texto.Normalizar(p.Enunciado) == chave))
            throw new RegraNegocioException("Já existe uma pergunta com este enunciado.");

        nova.Registrar(autor, agora, "Criada como rascunho");

        foreach (var r in referencias ?? Enumerable.Empty<Referencia>())
            nova.AdicionarReferencia(autor, new Referencia(r.Titulo, r.Instituicao, r.Url, r.Ano), agora);

        var novaLista = new List<Pergunta>(_perguntas) { nova };
        _repositorio.Salvar(novaLista);
        _perguntas = novaLista;
        return nova;
    }

    // ------------------------------------------------------------ Fluxo de curadoria
    public Pergunta AdicionarReferencia(Usuario usuario, int id, Referencia referencia) =>
        Alterar(id, p => p.AdicionarReferencia(usuario, referencia, _agora()));

    public Pergunta EnviarParaRevisao(Usuario usuario, int id) =>
        Alterar(id, p => p.EnviarParaRevisao(usuario, _agora()));

    public Pergunta Aprovar(Usuario usuario, int id) =>
        Alterar(id, p => p.Aprovar(usuario, _agora()));

    public Pergunta Devolver(Usuario usuario, int id, string? motivo) =>
        Alterar(id, p => p.Devolver(usuario, motivo, _agora()));

    // ------------------------------------------------------------ Consulta
    public IReadOnlyList<Pergunta> ListarTodas() => _perguntas.OrderBy(p => p.Id).ToList();

    public Pergunta? ObterPorId(int id) => _perguntas.FirstOrDefault(p => p.Id == id);

    /// <summary>Busca por qualquer combinação de texto (sem acento/maiúscula), área e status.</summary>
    public IReadOnlyList<Pergunta> Buscar(string? texto = null, AreaDistrito? area = null, StatusPergunta? status = null)
    {
        var termo = Texto.Normalizar(texto);

        return _perguntas
            .Where(p => area is null || p.Area == area)
            .Where(p => status is null || p.Status == status)
            .Where(p => termo.Length == 0 || p.TextoBusca().Contains(termo))
            .OrderBy(p => p.Id)
            .ToList();
    }

    // ------------------------------------------------------------ Internos
    private int ProximoId() => _perguntas.Count == 0 ? 1 : _perguntas.Max(p => p.Id) + 1;

    private Pergunta Alterar(int id, Action<Pergunta> acao)
    {
        var original = ObterPorId(id)
            ?? throw new RegraNegocioException($"Pergunta {id} não encontrada.");

        var copia = JsonSerializer.Deserialize<Pergunta>(
            JsonSerializer.Serialize(original, JsonConfig.Opcoes), JsonConfig.Opcoes)!;

        acao(copia); // pode lançar ValidacaoException/RegraNegocioException sem tocar no estado real

        var novaLista = _perguntas.Select(p => p.Id == id ? copia : p).ToList();
        _repositorio.Salvar(novaLista);
        _perguntas = novaLista;
        return copia;
    }
}
