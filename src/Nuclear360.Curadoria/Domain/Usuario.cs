namespace Nuclear360.Curadoria.Domain;

/// <summary>Quem está operando o painel. Só guarda apelido e perfil (minimização de dados / LGPD).</summary>
public sealed record Usuario(string Apelido, PerfilUsuario Perfil)
{
    public const int ApelidoMin = 3;
    public const int ApelidoMax = 20;

    /// <summary>Cria validando o apelido (3 a 20 caracteres, sem espaços — mesma regra do app).</summary>
    public static Usuario Criar(string? apelido, PerfilUsuario perfil)
    {
        var a = (apelido ?? "").Trim();

        if (a.Length < ApelidoMin || a.Length > ApelidoMax)
            throw new ValidacaoException($"Apelido deve ter entre {ApelidoMin} e {ApelidoMax} caracteres.");
        if (a.Any(char.IsWhiteSpace))
            throw new ValidacaoException("Apelido não pode conter espaços.");
        if (!Enum.IsDefined(perfil))
            throw new ValidacaoException("Perfil inválido.");

        return new Usuario(a, perfil);
    }

    /// <summary>Professores, curadores e admins podem montar perguntas; jogador só joga.</summary>
    public bool PodeCadastrar => Perfil is PerfilUsuario.Professor or PerfilUsuario.Curador or PerfilUsuario.Admin;

    /// <summary>Aprovação é exclusiva do perfil CURADOR.</summary>
    public bool PodeAprovar => Perfil == PerfilUsuario.Curador;
}
