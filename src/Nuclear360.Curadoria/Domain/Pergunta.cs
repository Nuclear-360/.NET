namespace Nuclear360.Curadoria.Domain;

/// <summary>
/// Entidade central da curadoria: uma pergunta do quiz do Nuclear 360.
/// Regra do projeto: nenhuma pergunta chega ao jogador sem referência e sem status APROVADA.
/// </summary>
public class Pergunta
{
    public const int EnunciadoMin = 15;
    public const int EnunciadoMax = 300;
    public const int ExplicacaoMin = 20;
    public const int ExplicacaoMax = 600;
    public const int AlternativasMin = 2;
    public const int AlternativasMax = 4;
    public const int AlternativaTextoMax = 120;

    // ---- Dados persistidos (propriedades públicas com set para o System.Text.Json) ----
    public int Id { get; set; }
    public string Enunciado { get; set; } = "";
    public AreaDistrito Area { get; set; }
    public string Explicacao { get; set; } = "";
    public List<Alternativa> Alternativas { get; set; } = new();
    public List<Referencia> Referencias { get; set; } = new();
    public StatusPergunta Status { get; set; } = StatusPergunta.Rascunho;
    public string CriadoPor { get; set; } = "";
    public DateTime CriadoEm { get; set; }
    public string? RevisadoPor { get; set; }
    public DateTime? AprovadaEm { get; set; }
    public List<EventoAuditoria> Auditoria { get; set; } = new();

    // ------------------------------------------------------------ Validação de atributos
    public List<string> Validar()
    {
        var erros = new List<string>();

        var enunciado = (Enunciado ?? "").Trim();
        if (enunciado.Length < EnunciadoMin || enunciado.Length > EnunciadoMax)
            erros.Add($"Enunciado deve ter entre {EnunciadoMin} e {EnunciadoMax} caracteres.");

        if (!Enum.IsDefined(Area))
            erros.Add("Área inválida.");

        var explicacao = (Explicacao ?? "").Trim();
        if (explicacao.Length < ExplicacaoMin || explicacao.Length > ExplicacaoMax)
            erros.Add($"Explicação deve ter entre {ExplicacaoMin} e {ExplicacaoMax} caracteres.");

        var alts = Alternativas ?? new List<Alternativa>();
        if (alts.Count < AlternativasMin || alts.Count > AlternativasMax)
        {
            erros.Add($"A pergunta deve ter entre {AlternativasMin} e {AlternativasMax} alternativas.");
        }
        else
        {
            if (alts.Any(a => string.IsNullOrWhiteSpace(a.Texto) || a.Texto.Trim().Length > AlternativaTextoMax))
                erros.Add($"Cada alternativa deve ter de 1 a {AlternativaTextoMax} caracteres.");

            var normalizadas = alts.Select(a => Texto.Normalizar(a.Texto)).ToList();
            if (normalizadas.Distinct().Count() != normalizadas.Count)
                erros.Add("As alternativas não podem ser repetidas.");

            if (alts.Count(a => a.Correta) != 1)
                erros.Add("A pergunta deve ter exatamente uma alternativa correta.");
        }

        return erros;
    }

    // ------------------------------------------------------------ Regras de negócio (fluxo de curadoria)

    /// <summary>Só rascunhos aceitam novas referências. Evita duplicar a mesma URL.</summary>
    public void AdicionarReferencia(Usuario usuario, Referencia referencia, DateTime agora)
    {
        ExigirCadastro(usuario);

        if (Status != StatusPergunta.Rascunho)
            throw new RegraNegocioException(
                $"Só é possível adicionar referências a perguntas em RASCUNHO (esta está {Texto.Codigo(Status)}).");

        var erros = referencia.Validar(agora.Year);
        if (erros.Count > 0) throw new ValidacaoException(erros);

        if (Referencias.Any(r => r.UrlNormalizada() == referencia.UrlNormalizada()))
            throw new RegraNegocioException("Esta referência (mesma URL) já foi adicionada à pergunta.");

        Referencias.Add(referencia);
        Registrar(usuario, agora, $"Referência adicionada: {referencia.Instituicao.Trim()} — {referencia.Titulo.Trim()}");
    }

    /// <summary>RASCUNHO → EM_REVISAO. Exige ao menos 1 referência: pergunta sem fonte não segue adiante.</summary>
    public void EnviarParaRevisao(Usuario usuario, DateTime agora)
    {
        ExigirCadastro(usuario);

        if (Status != StatusPergunta.Rascunho)
            throw new RegraNegocioException(
                $"Só é possível enviar para revisão uma pergunta em RASCUNHO (esta está {Texto.Codigo(Status)}).");

        if (Referencias.Count == 0)
            throw new RegraNegocioException(
                "Pergunta sem referência oficial não pode ser enviada para revisão.");

        var erros = Validar();
        if (erros.Count > 0) throw new ValidacaoException(erros);

        Status = StatusPergunta.EmRevisao;
        Registrar(usuario, agora, "Enviada para revisão");
    }

    /// <summary>
    /// EM_REVISAO → APROVADA. Só CURADOR aprova, com ao menos 1 referência,
    /// e nunca a própria pergunta (revisão por outra pessoa).
    /// </summary>
    public void Aprovar(Usuario usuario, DateTime agora)
    {
        if (!usuario.PodeAprovar)
            throw new RegraNegocioException("Apenas usuários com perfil CURADOR podem aprovar perguntas.");

        if (Status != StatusPergunta.EmRevisao)
            throw new RegraNegocioException(
                $"Só é possível aprovar uma pergunta EM_REVISAO (esta está {Texto.Codigo(Status)}).");

        if (string.Equals(CriadoPor, usuario.Apelido, StringComparison.OrdinalIgnoreCase))
            throw new RegraNegocioException("O curador não pode aprovar uma pergunta criada por ele mesmo.");

        if (Referencias.Count == 0)
            throw new RegraNegocioException("Pergunta sem referência oficial não pode ser aprovada.");

        Status = StatusPergunta.Aprovada;
        RevisadoPor = usuario.Apelido;
        AprovadaEm = agora;
        Registrar(usuario, agora, "Aprovada");
    }

    /// <summary>EM_REVISAO → RASCUNHO, com motivo obrigatório (volta para o autor corrigir).</summary>
    public void Devolver(Usuario usuario, string? motivo, DateTime agora)
    {
        if (!usuario.PodeAprovar)
            throw new RegraNegocioException("Apenas usuários com perfil CURADOR podem devolver perguntas.");

        if (Status != StatusPergunta.EmRevisao)
            throw new RegraNegocioException(
                $"Só é possível devolver uma pergunta EM_REVISAO (esta está {Texto.Codigo(Status)}).");

        var m = (motivo ?? "").Trim();
        if (m.Length < 5)
            throw new ValidacaoException("Informe o motivo da devolução (mínimo de 5 caracteres).");

        Status = StatusPergunta.Rascunho;
        RevisadoPor = usuario.Apelido;
        Registrar(usuario, agora, $"Devolvida ao rascunho: {m}");
    }

    // ------------------------------------------------------------ Apoio
    public void Registrar(Usuario usuario, DateTime agora, string acao) =>
        Auditoria.Add(new EventoAuditoria(agora, usuario.Apelido, acao));

    /// <summary>Texto usado na busca (enunciado, explicação e fontes), já normalizado.</summary>
    public string TextoBusca() => Texto.Normalizar(
        string.Join(' ',
            new[] { Enunciado, Explicacao }
                .Concat(Referencias.Select(r => $"{r.Titulo} {r.Instituicao}"))));

    private static void ExigirCadastro(Usuario usuario)
    {
        if (!usuario.PodeCadastrar)
            throw new RegraNegocioException("Perfil JOGADOR não pode criar ou alterar perguntas.");
    }
}
