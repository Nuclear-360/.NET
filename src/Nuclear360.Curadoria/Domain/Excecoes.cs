namespace Nuclear360.Curadoria.Domain;

/// <summary>Base para erros esperados do domínio (dados inválidos ou regra de negócio violada).</summary>
public abstract class DominioException : Exception
{
    protected DominioException(string mensagem) : base(mensagem) { }
}

/// <summary>Um ou mais atributos inválidos.</summary>
public sealed class ValidacaoException : DominioException
{
    public IReadOnlyList<string> Erros { get; }

    public ValidacaoException(IEnumerable<string> erros)
        : this(erros.ToList(), 0) { }

    public ValidacaoException(string erro) : this(new List<string> { erro }, 0) { }

    private ValidacaoException(List<string> erros, int _)
        : base("Dados inválidos: " + string.Join(" ", erros))
    {
        Erros = erros;
    }
}

/// <summary>Operação proibida pelas regras de negócio (perfil, status, referência obrigatória...).</summary>
public sealed class RegraNegocioException : DominioException
{
    public RegraNegocioException(string mensagem) : base(mensagem) { }
}

/// <summary>Falha ao ler/gravar o arquivo de dados.</summary>
public sealed class PersistenciaException : Exception
{
    public PersistenciaException(string mensagem, Exception? inner = null) : base(mensagem, inner) { }
}
