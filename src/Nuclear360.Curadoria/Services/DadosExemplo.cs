using Nuclear360.Curadoria.Domain;

namespace Nuclear360.Curadoria.Services;

/// <summary>
/// Carga inicial só para demonstração (primeira execução). Usa o próprio serviço,
/// então o fluxo e as auditorias são os mesmos de uma operação real.
/// ATENÇÃO: as referências abaixo apontam para os portais institucionais; antes de publicar
/// conteúdo, substitua pelos documentos oficiais específicos (CNEN, AIEA, AMAZUL).
/// </summary>
public static class DadosExemplo
{
    public static void Carregar(CuradoriaService servico)
    {
        var professor = Usuario.Criar("prof.marcos", PerfilUsuario.Professor);
        var curador = Usuario.Criar("curador.amazul", PerfilUsuario.Curador);

        var cnen = new Referencia("Portal institucional da CNEN", "CNEN", "https://www.gov.br/cnen", 2024);
        var aiea = new Referencia("Portal institucional da AIEA", "AIEA", "https://www.iaea.org", 2024);

        // 1) APROVADA
        var p1 = servico.Cadastrar(professor,
            "Um paciente que faz radioterapia externa (com feixe de radiação) fica radioativo depois da sessão?",
            AreaDistrito.Saude,
            "Na radioterapia externa o feixe só atinge o paciente enquanto o aparelho está ligado. Ao desligar, não há material radioativo no corpo.",
            new[]
            {
                new Alternativa("Não, o feixe deixa de atingir o paciente quando o aparelho é desligado", true),
                new Alternativa("Sim, fica radioativo por várias semanas", false),
                new Alternativa("Sim, e precisa ficar isolado por meses", false)
            },
            new[] { cnen });
        servico.EnviarParaRevisao(professor, p1.Id);
        servico.Aprovar(curador, p1.Id);

        // 2) EM_REVISAO
        var p2 = servico.Cadastrar(professor,
            "Alimentos tratados por irradiação se tornam radioativos?",
            AreaDistrito.Agro,
            "A irradiação de alimentos expõe o produto a radiação para eliminar microrganismos e pragas, sem torná-lo radioativo.",
            new[]
            {
                new Alternativa("Não, o alimento não fica radioativo", true),
                new Alternativa("Sim, fica radioativo até ser cozido", false),
                new Alternativa("Sim, mas apenas as frutas", false)
            },
            new[] { aiea });
        servico.EnviarParaRevisao(professor, p2.Id);

        // 3) RASCUNHO (sem referência: ainda não pode ser enviada)
        servico.Cadastrar(professor,
            "Para um reator de potência, o urânio precisa ter o mesmo grau de enriquecimento de uma arma?",
            AreaDistrito.Energia,
            "Reatores de potência usam urânio com baixo enriquecimento, muito inferior ao exigido para fins militares.",
            new[]
            {
                new Alternativa("Não, o enriquecimento para reator é bem menor", true),
                new Alternativa("Sim, é exatamente o mesmo", false)
            });
    }
}
