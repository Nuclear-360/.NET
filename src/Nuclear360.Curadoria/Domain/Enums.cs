namespace Nuclear360.Curadoria.Domain;

/// <summary>Áreas/distritos do mapa do Nuclear 360 (mesmos valores do modelo de dados do projeto).</summary>
public enum AreaDistrito
{
    Saude = 1,
    Agro = 2,
    Industria = 3,
    Pesquisa = 4,
    Energia = 5
}

/// <summary>Fluxo de curadoria: RASCUNHO → EM_REVISAO → APROVADA.</summary>
public enum StatusPergunta
{
    Rascunho = 1,
    EmRevisao = 2,
    Aprovada = 3
}

public enum PerfilUsuario
{
    Jogador = 1,
    Professor = 2,
    Curador = 3,
    Admin = 4
}
