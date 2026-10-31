using Nuclear360.Curadoria.Domain;

namespace Nuclear360.Curadoria.Repositories;

/// <summary>
/// Contrato de persistência. Hoje é um arquivo JSON; na próxima etapa pode ser
/// trocado por Entity Framework Core + Oracle sem mexer em Domain/Services/UI.
/// </summary>
public interface IPerguntaRepository
{
    List<Pergunta> Carregar();
    void Salvar(IReadOnlyList<Pergunta> perguntas);
}
