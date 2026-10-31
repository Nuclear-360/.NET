using System.Text;
using Nuclear360.Curadoria.Domain;
using Nuclear360.Curadoria.Repositories;
using Nuclear360.Curadoria.Services;
using Nuclear360.Curadoria.UI;

// Runner de testes mínimo (sem pacotes externos). Saída: código 0 se tudo passar, 1 se algo falhar.
var t = new Runner();

// ----------------------------------------------------------------- helpers
var prof = Usuario.Criar("prof.marcos", PerfilUsuario.Professor);
var curador = Usuario.Criar("curador.amazul", PerfilUsuario.Curador);
var outroCurador = Usuario.Criar("curador.dois", PerfilUsuario.Curador);
var jogador = Usuario.Criar("ana.16", PerfilUsuario.Jogador);

string NovoArquivo() => Path.Combine(Path.GetTempPath(), "nuclear360-" + Guid.NewGuid().ToString("N"), "perguntas.json");
Referencia Ref(string url = "https://www.gov.br/cnen") => new("Portal da CNEN", "CNEN", url, 2024);
List<Alternativa> Alts() => new() { new("Não, não fica radioativo", true), new("Sim, fica radioativo", false) };
string Enun(string s = "Alimentos irradiados ficam radioativos depois do tratamento?") => s;
const string Expl = "A irradiação não torna o alimento radioativo, pois não há contato com material radioativo.";

CuradoriaService Servico(string? arquivo = null) => new(new JsonPerguntaRepository(arquivo ?? NovoArquivo()));
Pergunta Cria(CuradoriaService s, string? enunciado = null, bool comRef = true) =>
    s.Cadastrar(prof, enunciado ?? Enun(), AreaDistrito.Agro, Expl, Alts(), comRef ? new[] { Ref() } : null);

// ----------------------------------------------------------------- 1) Validação de atributos
t.Teste("Cadastro válido nasce como RASCUNHO com auditoria", () =>
{
    var s = Servico();
    var p = Cria(s);
    t.Igual(StatusPergunta.Rascunho, p.Status);
    t.Igual(1, p.Id);
    t.Verdade(p.Auditoria.Count >= 1, "deveria registrar auditoria");
});
t.Teste("Enunciado curto demais é rejeitado", () =>
    t.Lanca<ValidacaoException>(() => Servico().Cadastrar(prof, "Curto?", AreaDistrito.Saude, Expl, Alts())));
t.Teste("Enunciado longo demais é rejeitado", () =>
    t.Lanca<ValidacaoException>(() => Servico().Cadastrar(prof, new string('a', 301), AreaDistrito.Saude, Expl, Alts())));
t.Teste("Explicação curta é rejeitada", () =>
    t.Lanca<ValidacaoException>(() => Servico().Cadastrar(prof, Enun(), AreaDistrito.Saude, "curta", Alts())));
t.Teste("Área inexistente é rejeitada", () =>
    t.Lanca<ValidacaoException>(() => Servico().Cadastrar(prof, Enun(), (AreaDistrito)99, Expl, Alts())));
t.Teste("Menos de 2 alternativas é rejeitado", () =>
    t.Lanca<ValidacaoException>(() => Servico().Cadastrar(prof, Enun(), AreaDistrito.Agro, Expl, new[] { new Alternativa("Só uma", true) })));
t.Teste("Mais de 4 alternativas é rejeitado", () =>
    t.Lanca<ValidacaoException>(() => Servico().Cadastrar(prof, Enun(), AreaDistrito.Agro, Expl,
        Enumerable.Range(1, 5).Select(i => new Alternativa("Opção " + i, i == 1)))));
t.Teste("Sem alternativa correta é rejeitado", () =>
    t.Lanca<ValidacaoException>(() => Servico().Cadastrar(prof, Enun(), AreaDistrito.Agro, Expl,
        new[] { new Alternativa("A", false), new Alternativa("B", false) })));
t.Teste("Duas alternativas corretas é rejeitado", () =>
    t.Lanca<ValidacaoException>(() => Servico().Cadastrar(prof, Enun(), AreaDistrito.Agro, Expl,
        new[] { new Alternativa("A", true), new Alternativa("B", true) })));
t.Teste("Alternativas repetidas (ignorando acento/caixa) são rejeitadas", () =>
    t.Lanca<ValidacaoException>(() => Servico().Cadastrar(prof, Enun(), AreaDistrito.Agro, Expl,
        new[] { new Alternativa("Não", true), new Alternativa("NAO", false) })));
t.Teste("Vários erros são reportados juntos", () =>
{
    var ex = t.Lanca<ValidacaoException>(() => Servico().Cadastrar(prof, "x", AreaDistrito.Agro, "y", Alts()));
    t.Verdade(ex.Erros.Count >= 2, "esperava 2+ erros, veio " + ex.Erros.Count);
});
t.Teste("Referência com URL inválida é rejeitada", () =>
{
    var s = Servico(); var p = Cria(s, comRef: false);
    t.Lanca<ValidacaoException>(() => s.AdicionarReferencia(prof, p.Id, new Referencia("Doc CNEN", "CNEN", "isso-nao-e-url", 2024)));
    t.Lanca<ValidacaoException>(() => s.AdicionarReferencia(prof, p.Id, new Referencia("Doc CNEN", "CNEN", "ftp://servidor/doc", 2024)));
});
t.Teste("Referência com ano futuro ou anterior a 1900 é rejeitada", () =>
{
    var s = Servico(); var p = Cria(s, comRef: false);
    t.Lanca<ValidacaoException>(() => s.AdicionarReferencia(prof, p.Id, new Referencia("Doc CNEN", "CNEN", "https://a.gov.br", DateTime.Now.Year + 1)));
    t.Lanca<ValidacaoException>(() => s.AdicionarReferencia(prof, p.Id, new Referencia("Doc CNEN", "CNEN", "https://a.gov.br", 1800)));
});
t.Teste("Apelido inválido (curto, longo, com espaço)", () =>
{
    t.Lanca<ValidacaoException>(() => Usuario.Criar("ab", PerfilUsuario.Professor));
    t.Lanca<ValidacaoException>(() => Usuario.Criar(new string('a', 21), PerfilUsuario.Professor));
    t.Lanca<ValidacaoException>(() => Usuario.Criar("com espaco", PerfilUsuario.Professor));
    t.Lanca<ValidacaoException>(() => Usuario.Criar(null, PerfilUsuario.Professor));
});

// ----------------------------------------------------------------- 2) Regras de negócio
t.Teste("Pergunta sem referência NÃO pode ir para revisão", () =>
{
    var s = Servico(); var p = Cria(s, comRef: false);
    var ex = t.Lanca<RegraNegocioException>(() => s.EnviarParaRevisao(prof, p.Id));
    t.Verdade(ex.Message.Contains("referência"), "mensagem deve citar referência");
    t.Igual(StatusPergunta.Rascunho, s.ObterPorId(p.Id)!.Status);
});
t.Teste("Fluxo feliz: RASCUNHO → EM_REVISAO → APROVADA", () =>
{
    var s = Servico(); var p = Cria(s);
    s.EnviarParaRevisao(prof, p.Id);
    t.Igual(StatusPergunta.EmRevisao, s.ObterPorId(p.Id)!.Status);
    s.Aprovar(curador, p.Id);
    var final = s.ObterPorId(p.Id)!;
    t.Igual(StatusPergunta.Aprovada, final.Status);
    t.Igual("curador.amazul", final.RevisadoPor);
    t.Verdade(final.AprovadaEm.HasValue, "AprovadaEm deveria estar preenchida");
    t.Verdade(final.Auditoria.Any(a => a.Acao == "Aprovada" && a.Usuario == "curador.amazul"), "auditoria da aprovação");
});
t.Teste("Só CURADOR aprova (professor e admin não)", () =>
{
    var s = Servico(); var p = Cria(s); s.EnviarParaRevisao(prof, p.Id);
    t.Lanca<RegraNegocioException>(() => s.Aprovar(prof, p.Id));
    t.Lanca<RegraNegocioException>(() => s.Aprovar(Usuario.Criar("admin.geral", PerfilUsuario.Admin), p.Id));
    t.Lanca<RegraNegocioException>(() => s.Aprovar(jogador, p.Id));
    t.Igual(StatusPergunta.EmRevisao, s.ObterPorId(p.Id)!.Status);
});
t.Teste("Curador não aprova a própria pergunta", () =>
{
    var s = Servico();
    var p = s.Cadastrar(curador, Enun(), AreaDistrito.Agro, Expl, Alts(), new[] { Ref() });
    s.EnviarParaRevisao(curador, p.Id);
    t.Lanca<RegraNegocioException>(() => s.Aprovar(curador, p.Id));
    s.Aprovar(outroCurador, p.Id); // outro curador consegue
    t.Igual(StatusPergunta.Aprovada, s.ObterPorId(p.Id)!.Status);
});
t.Teste("Não aprova pergunta que ainda é RASCUNHO nem reaprova APROVADA", () =>
{
    var s = Servico(); var p = Cria(s);
    t.Lanca<RegraNegocioException>(() => s.Aprovar(curador, p.Id));
    s.EnviarParaRevisao(prof, p.Id); s.Aprovar(curador, p.Id);
    t.Lanca<RegraNegocioException>(() => s.Aprovar(outroCurador, p.Id));
});
t.Teste("Devolver exige curador, motivo e volta para RASCUNHO", () =>
{
    var s = Servico(); var p = Cria(s); s.EnviarParaRevisao(prof, p.Id);
    t.Lanca<RegraNegocioException>(() => s.Devolver(prof, p.Id, "motivo qualquer"));
    t.Lanca<ValidacaoException>(() => s.Devolver(curador, p.Id, "  "));
    s.Devolver(curador, p.Id, "Referência não sustenta a explicação");
    var d = s.ObterPorId(p.Id)!;
    t.Igual(StatusPergunta.Rascunho, d.Status);
    t.Verdade(d.Auditoria.Any(a => a.Acao.Contains("Referência não sustenta")), "motivo na auditoria");
});
t.Teste("Referências só entram em RASCUNHO; URL repetida é recusada", () =>
{
    var s = Servico(); var p = Cria(s, comRef: false);
    s.AdicionarReferencia(prof, p.Id, Ref("https://www.iaea.org"));
    t.Lanca<RegraNegocioException>(() => s.AdicionarReferencia(prof, p.Id, Ref("https://www.iaea.org/"))); // mesma URL
    s.EnviarParaRevisao(prof, p.Id);
    t.Lanca<RegraNegocioException>(() => s.AdicionarReferencia(prof, p.Id, Ref("https://outra.gov.br")));
});
t.Teste("JOGADOR não cadastra nem altera", () =>
{
    var s = Servico();
    t.Lanca<RegraNegocioException>(() => s.Cadastrar(jogador, Enun(), AreaDistrito.Agro, Expl, Alts()));
    var p = Cria(s, comRef: false);
    t.Lanca<RegraNegocioException>(() => s.AdicionarReferencia(jogador, p.Id, Ref()));
    t.Lanca<RegraNegocioException>(() => s.EnviarParaRevisao(jogador, p.Id));
});
t.Teste("Enunciado duplicado é recusado (ignorando acento/caixa)", () =>
{
    var s = Servico(); Cria(s);
    t.Lanca<RegraNegocioException>(() => Cria(s, "ALIMENTOS IRRADIADOS FICAM RADIOATIVOS DEPOIS DO TRATAMENTO?"));
});
t.Teste("ID inexistente gera erro de regra, não exceção genérica", () =>
{
    var s = Servico();
    t.Lanca<RegraNegocioException>(() => s.Aprovar(curador, 999));
    t.Lanca<RegraNegocioException>(() => s.EnviarParaRevisao(prof, 0));
});
t.Teste("Falha de regra não altera o estado da pergunta", () =>
{
    var s = Servico(); var p = Cria(s, comRef: false);
    var antes = s.ObterPorId(p.Id)!.Auditoria.Count;
    t.Lanca<RegraNegocioException>(() => s.EnviarParaRevisao(prof, p.Id));
    t.Igual(antes, s.ObterPorId(p.Id)!.Auditoria.Count);
});

// ----------------------------------------------------------------- 3) Consulta
t.Teste("Listagem retorna todas ordenadas por ID", () =>
{
    var s = Servico();
    Cria(s, "Primeira pergunta sobre radiação natural no dia a dia?");
    Cria(s, "Segunda pergunta sobre radiação natural no dia a dia?");
    var ids = s.ListarTodas().Select(p => p.Id).ToList();
    t.Igual("1,2", string.Join(',', ids));
});
t.Teste("Busca por texto ignora acento e maiúscula", () =>
{
    var s = Servico();
    s.Cadastrar(prof, "A radioterapia deixa o paciente radioativo para sempre?", AreaDistrito.Saude, Expl, Alts(), new[] { Ref() });
    Cria(s);
    t.Igual(1, s.Buscar("RADIOTERAPIA").Count);
    t.Igual(1, s.Buscar("radioterapía").Count);
    t.Igual(1, s.Buscar("irradiados").Count);
    t.Igual(0, s.Buscar("submarino").Count);
});
t.Teste("Busca também encontra pela fonte (instituição)", () =>
{
    var s = Servico(); Cria(s);
    t.Igual(1, s.Buscar("cnen").Count);
});
t.Teste("Busca por área, por status e combinada", () =>
{
    var s = Servico();
    var a = Cria(s);
    s.Cadastrar(prof, "Qual o principal uso da gamagrafia na indústria?", AreaDistrito.Industria, Expl, Alts());
    s.EnviarParaRevisao(prof, a.Id);
    t.Igual(1, s.Buscar(area: AreaDistrito.Agro).Count);
    t.Igual(1, s.Buscar(status: StatusPergunta.EmRevisao).Count);
    t.Igual(1, s.Buscar(status: StatusPergunta.Rascunho).Count);
    t.Igual(0, s.Buscar(area: AreaDistrito.Agro, status: StatusPergunta.Rascunho).Count);
    t.Igual(2, s.Buscar().Count);
});

// ----------------------------------------------------------------- 4) Persistência
t.Teste("Dados persistem entre execuções (novo serviço, mesmo arquivo)", () =>
{
    var arquivo = NovoArquivo();
    var s1 = Servico(arquivo);
    var p = Cria(s1); s1.EnviarParaRevisao(prof, p.Id); s1.Aprovar(curador, p.Id);

    var s2 = Servico(arquivo);
    var lida = s2.ObterPorId(p.Id)!;
    t.Igual(StatusPergunta.Aprovada, lida.Status);
    t.Igual(1, lida.Referencias.Count);
    t.Igual(2, lida.Alternativas.Count);
    t.Verdade(lida.Alternativas.Count(a => a.Correta) == 1, "alternativa correta preservada");
    t.Verdade(lida.Auditoria.Count >= 3, "auditoria preservada");
    t.Igual(Expl, lida.Explicacao);
});
t.Teste("IDs continuam a sequência após reabrir", () =>
{
    var arquivo = NovoArquivo();
    Cria(Servico(arquivo));
    var p2 = Cria(Servico(arquivo), "Outra pergunta totalmente diferente sobre a datação por carbono-14?");
    t.Igual(2, p2.Id);
});
t.Teste("Arquivo JSON gravado é legível, com enums em texto e acentos", () =>
{
    var arquivo = NovoArquivo();
    Cria(Servico(arquivo));
    var json = File.ReadAllText(arquivo);
    t.Verdade(json.Contains("\"Rascunho\""), "enum como texto");
    t.Verdade(json.Contains("irradiação"), "acentos preservados");
    t.Verdade(!File.Exists(arquivo + ".tmp"), "não deve sobrar arquivo temporário");
});
t.Teste("Arquivo inexistente ou vazio inicia base vazia", () =>
{
    t.Igual(0, Servico().ListarTodas().Count);
    var arquivo = NovoArquivo();
    Directory.CreateDirectory(Path.GetDirectoryName(arquivo)!);
    File.WriteAllText(arquivo, "");
    t.Igual(0, Servico(arquivo).ListarTodas().Count);
});
t.Teste("JSON corrompido não derruba: é isolado e a base reinicia vazia", () =>
{
    var arquivo = NovoArquivo();
    Directory.CreateDirectory(Path.GetDirectoryName(arquivo)!);
    File.WriteAllText(arquivo, "{ isso não é json válido ][");
    var repo = new JsonPerguntaRepository(arquivo);
    var s = new CuradoriaService(repo);
    t.Igual(0, s.ListarTodas().Count);
    t.Verdade(repo.Aviso is not null, "deveria avisar sobre a corrupção");
    t.Verdade(Directory.GetFiles(Path.GetDirectoryName(arquivo)!, "*.corrompido-*").Length == 1, "cópia do arquivo ruim");
    Cria(s); // e continua gravando normalmente
    t.Igual(1, Servico(arquivo).ListarTodas().Count);
});
t.Teste("Falha de gravação vira PersistenciaException e não corrompe a memória", () =>
{
    var s = new CuradoriaService(new RepositorioQueFalha());
    t.Lanca<PersistenciaException>(() => Cria(s));
    t.Igual(0, s.ListarTodas().Count);
});
t.Teste("Falha de gravação numa alteração mantém o estado anterior", () =>
{
    var repo = new RepositorioQueFalha();
    var s = new CuradoriaService(repo);
    repo.Falhar = false;
    var p = Cria(s);
    repo.Falhar = true;
    t.Lanca<PersistenciaException>(() => s.EnviarParaRevisao(prof, p.Id));
    t.Igual(StatusPergunta.Rascunho, s.ObterPorId(p.Id)!.Status);
});
t.Teste("Dados de exemplo cobrem os 3 status e respeitam as regras", () =>
{
    var s = Servico();
    DadosExemplo.Carregar(s);
    t.Igual(3, s.ListarTodas().Count);
    t.Igual(1, s.Buscar(status: StatusPergunta.Aprovada).Count);
    t.Igual(1, s.Buscar(status: StatusPergunta.EmRevisao).Count);
    t.Igual(1, s.Buscar(status: StatusPergunta.Rascunho).Count);
    t.Verdade(s.ListarTodas().Where(p => p.Status != StatusPergunta.Rascunho).All(p => p.Referencias.Count > 0),
        "toda pergunta fora do rascunho tem referência");
});

// ----------------------------------------------------------------- 5) Interface de console (ponta a ponta)
string Sessao(string[] linhas, CuradoriaService? servico = null)
{
    var saida = new StringWriter();
    var entrada = new StringReader(string.Join("\n", linhas) + "\n");
    new Aplicacao(servico ?? Servico(), entrada, saida).Executar();
    return saida.ToString();
}

t.Teste("UI: cadastro completo, envio, aprovação, listagem e busca", () =>
{
    var s = Servico();
    // professor cadastra com 1 referência e envia para revisão
    var s1 = Sessao(new[]
    {
        "prof.marcos", "2",                                   // login (2 = PROFESSOR)
        "1",                                                  // cadastrar
        "Pesticidas são a única forma de controlar pragas na lavoura?",
        "2",                                                  // AGRO
        "Insetos estéreis, produzidos com radiação, são uma alternativa de controle.",
        "Não, existem técnicas nucleares", "Sim, é a única", "",   // alternativas
        "1",                                                  // correta
        "s", "Técnica do inseto estéril", "AIEA", "https://www.iaea.org/sit", "2023", "n",
        "3", "1",                                             // enviar para revisão #1
        "6",                                                  // listar
        "7", "inseto", "0", "0",                              // busca por texto
        "0"
    }, s);
    t.Verdade(s1.Contains("Pergunta #1 cadastrada como RASCUNHO"), "cadastro\n" + s1);
    t.Verdade(s1.Contains("enviada para revisão"), "envio");
    t.Verdade(s1.Contains("EM_REVISAO"), "listagem mostra status");
    t.Verdade(s1.Contains("Resultado da busca: 1 pergunta"), "busca");
    t.Igual(StatusPergunta.EmRevisao, s.ObterPorId(1)!.Status);

    // curador aprova
    var s2 = Sessao(new[] { "curador.amazul", "3", "4", "1", "8", "1", "0" }, s);
    t.Verdade(s2.Contains("APROVADA por curador.amazul"), "aprovação\n" + s2);
    t.Verdade(s2.Contains("Auditoria:"), "detalhes mostram auditoria");
    t.Igual(StatusPergunta.Aprovada, s.ObterPorId(1)!.Status);
});
t.Teste("UI: erros são mostrados e o programa continua (não quebra)", () =>
{
    var s = Servico(); var p = Cria(s, comRef: false);
    var saida = Sessao(new[]
    {
        "ab", "2",                // apelido inválido → pede de novo
        "prof.marcos", "2",
        "abc",                    // opção inválida
        "3", "abc",               // ID não numérico
        "3", "999",               // ID inexistente
        "3", p.Id.ToString(),     // sem referência
        "4", p.Id.ToString(),     // professor tentando aprovar
        "2", p.Id.ToString(), "Doc", "CNEN", "nao-e-url", "ano",   // ano não numérico
        "2", p.Id.ToString(), "Doc CNEN", "CNEN", "nao-e-url", "2024", // URL inválida
        "0"
    }, s);
    t.Verdade(saida.Contains("Apelido deve ter entre"), "apelido inválido\n" + saida);
    t.Verdade(saida.Contains("Opção inválida"), "menu inválido");
    t.Verdade(saida.Contains("O ID deve ser um número"), "id não numérico");
    t.Verdade(saida.Contains("Pergunta 999 não encontrada"), "id inexistente");
    t.Verdade(saida.Contains("sem referência oficial"), "sem referência");
    t.Verdade(saida.Contains("Apenas usuários com perfil CURADOR"), "perfil");
    t.Verdade(saida.Contains("Ano da referência deve ser um número"), "ano");
    t.Verdade(saida.Contains("URL da referência deve ser"), "url");
    t.Verdade(saida.TrimEnd().EndsWith("Até logo!"), "terminou normalmente com 0");
    t.Verdade(!saida.Contains("Exception") && !saida.Contains("   at "), "sem stack trace");
});
t.Teste("UI: jogador não consegue cadastrar; trocar de usuário funciona", () =>
{
    var s = Servico();
    var saida = Sessao(new[] { "ana.16", "1", "1", "9", "prof.marcos", "2", "6", "0" }, s);
    t.Verdade(saida.Contains("Perfil JOGADOR não pode criar"), "bloqueio\n" + saida);
    t.Verdade(saida.Contains("usuário: prof.marcos · PROFESSOR"), "troca de usuário");
    t.Verdade(saida.Contains("Nenhuma pergunta encontrada"), "lista vazia tratada");
});
t.Teste("UI: fim da entrada (Ctrl+D / pipe vazio) encerra com elegância", () =>
{
    var saida = Sessao(new[] { "prof.marcos", "2" });
    t.Verdade(saida.Contains("Entrada encerrada"), saida);
});
t.Teste("UI: cadastro inválido mostra todos os erros e nada é salvo", () =>
{
    var s = Servico();
    var saida = Sessao(new[]
    {
        "prof.marcos", "2", "1", "curto", "1", "curta", "A", "B", "", "1", "n", "0"
    }, s);
    t.Verdade(saida.Contains("✗ Enunciado deve ter entre"), saida);
    t.Verdade(saida.Contains("✗ Explicação deve ter entre"), saida);
    t.Igual(0, s.ListarTodas().Count);
});

return t.Resumo();

// ----------------------------------------------------------------- infraestrutura de teste
sealed class RepositorioQueFalha : IPerguntaRepository
{
    public bool Falhar { get; set; } = true;
    public List<Pergunta> Carregar() => new();
    public void Salvar(IReadOnlyList<Pergunta> perguntas)
    {
        if (Falhar) throw new PersistenciaException("disco cheio (simulado)");
    }
}

sealed class Runner
{
    private int _ok, _falhas;

    public void Teste(string nome, Action corpo)
    {
        try
        {
            corpo();
            _ok++;
            Console.WriteLine($"  PASSOU  {nome}");
        }
        catch (Exception ex)
        {
            _falhas++;
            Console.WriteLine($"  FALHOU  {nome}\n          {ex.GetType().Name}: {ex.Message.Replace("\n", "\n          ")}");
        }
    }

    public void Verdade(bool condicao, string mensagem = "condição falsa")
    {
        if (!condicao) throw new Exception(mensagem);
    }

    public void Igual<T>(T esperado, T atual)
    {
        if (!EqualityComparer<T>.Default.Equals(esperado, atual))
            throw new Exception($"esperado <{esperado}> mas veio <{atual}>");
    }

    public TEx Lanca<TEx>(Action acao) where TEx : Exception
    {
        try { acao(); }
        catch (TEx ex) { return ex; }
        catch (Exception ex) { throw new Exception($"esperava {typeof(TEx).Name}, veio {ex.GetType().Name}: {ex.Message}"); }
        throw new Exception($"esperava {typeof(TEx).Name}, mas nenhuma exceção foi lançada");
    }

    public int Resumo()
    {
        Console.WriteLine($"\nResultado: {_ok} passaram, {_falhas} falharam, {_ok + _falhas} no total.");
        return _falhas == 0 ? 0 : 1;
    }
}
