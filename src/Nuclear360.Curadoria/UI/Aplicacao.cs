using Nuclear360.Curadoria.Domain;
using Nuclear360.Curadoria.Services;

namespace Nuclear360.Curadoria.UI;

/// <summary>Interface de console do Painel de Curadoria do Nuclear 360.</summary>
public sealed class Aplicacao
{
    private sealed class EntradaEncerradaException : Exception { }

    private readonly CuradoriaService _servico;
    private readonly TextReader _entrada;
    private readonly TextWriter _saida;
    private Usuario _usuario = new("anonimo", PerfilUsuario.Jogador);

    public Aplicacao(CuradoriaService servico, TextReader entrada, TextWriter saida)
    {
        _servico = servico;
        _entrada = entrada;
        _saida = saida;
    }

    public void Executar()
    {
        try
        {
            _saida.WriteLine("==============================================");
            _saida.WriteLine("  NUCLEAR 360 — Painel de Curadoria (.NET)");
            _saida.WriteLine("  Nenhuma pergunta chega ao jogador sem fonte.");
            _saida.WriteLine("==============================================");

            _usuario = Identificar();

            while (true)
            {
                MostrarMenu();
                var opcao = Ler("Opção");

                if (opcao == "0")
                {
                    _saida.WriteLine("Até logo!");
                    return;
                }

                Proteger(() => Despachar(opcao));
            }
        }
        catch (EntradaEncerradaException)
        {
            _saida.WriteLine();
            _saida.WriteLine("Entrada encerrada. Até logo!");
        }
    }

    // ------------------------------------------------------------ Menu
    private void MostrarMenu()
    {
        _saida.WriteLine();
        _saida.WriteLine($"--- Menu  (usuário: {_usuario.Apelido} · {Texto.Codigo(_usuario.Perfil)}) ---");
        _saida.WriteLine(" 1) Cadastrar pergunta");
        _saida.WriteLine(" 2) Adicionar referência a uma pergunta");
        _saida.WriteLine(" 3) Enviar pergunta para revisão");
        _saida.WriteLine(" 4) Aprovar pergunta           [CURADOR]");
        _saida.WriteLine(" 5) Devolver pergunta ao rascunho [CURADOR]");
        _saida.WriteLine(" 6) Listar todas as perguntas");
        _saida.WriteLine(" 7) Buscar (texto, área e/ou status)");
        _saida.WriteLine(" 8) Ver detalhes e auditoria de uma pergunta");
        _saida.WriteLine(" 9) Trocar de usuário");
        _saida.WriteLine(" 0) Sair");
    }

    private void Despachar(string opcao)
    {
        switch (opcao)
        {
            case "1": Cadastrar(); break;
            case "2": AdicionarReferencia(); break;
            case "3": EnviarParaRevisao(); break;
            case "4": Aprovar(); break;
            case "5": Devolver(); break;
            case "6": Listar(_servico.ListarTodas(), "Todas as perguntas"); break;
            case "7": Buscar(); break;
            case "8": Detalhes(); break;
            case "9": _usuario = Identificar(); break;
            default: _saida.WriteLine("Opção inválida. Digite um número do menu."); break;
        }
    }

    /// <summary>Qualquer erro vira mensagem; o aplicativo não é interrompido.</summary>
    private void Proteger(Action acao)
    {
        try
        {
            acao();
        }
        catch (EntradaEncerradaException)
        {
            throw;
        }
        catch (ValidacaoException ex)
        {
            _saida.WriteLine("Não foi possível concluir — dados inválidos:");
            foreach (var erro in ex.Erros) _saida.WriteLine($"  ✗ {erro}");
        }
        catch (DominioException ex)
        {
            _saida.WriteLine($"✗ {ex.Message}");
        }
        catch (PersistenciaException ex)
        {
            _saida.WriteLine($"✗ Erro de armazenamento: {ex.Message}");
        }
        catch (Exception ex)
        {
            _saida.WriteLine($"✗ Erro inesperado: {ex.Message}. A aplicação continua em execução.");
        }
    }

    // ------------------------------------------------------------ Ações
    private Usuario Identificar()
    {
        while (true)
        {
            try
            {
                _saida.WriteLine();
                var apelido = Ler("Apelido (3 a 20 caracteres, sem espaços)");
                var perfil = EscolherEnum<PerfilUsuario>("Perfil");
                return Usuario.Criar(apelido, perfil);
            }
            catch (ValidacaoException ex)
            {
                foreach (var erro in ex.Erros) _saida.WriteLine($"  ✗ {erro}");
            }
        }
    }

    private void Cadastrar()
    {
        if (!_usuario.PodeCadastrar)
            throw new RegraNegocioException("Perfil JOGADOR não pode criar perguntas. Use a opção 9 para trocar de usuário.");

        _saida.WriteLine("\n== Nova pergunta ==");
        var enunciado = Ler($"Enunciado ({Pergunta.EnunciadoMin}-{Pergunta.EnunciadoMax} caracteres)");
        var area = EscolherEnum<AreaDistrito>("Área");
        var explicacao = Ler($"Explicação exibida após a resposta ({Pergunta.ExplicacaoMin}-{Pergunta.ExplicacaoMax} caracteres)");

        var alternativas = new List<Alternativa>();
        _saida.WriteLine($"Alternativas ({Pergunta.AlternativasMin}-{Pergunta.AlternativasMax}). Linha vazia encerra.");
        while (alternativas.Count < Pergunta.AlternativasMax)
        {
            var texto = Ler($"  Alternativa {alternativas.Count + 1}");
            if (texto.Length == 0)
            {
                if (alternativas.Count >= Pergunta.AlternativasMin) break;
                _saida.WriteLine($"  Informe pelo menos {Pergunta.AlternativasMin} alternativas.");
                continue;
            }
            alternativas.Add(new Alternativa(texto, false));
        }

        var correta = LerInteiro("Número da alternativa correta", 1, alternativas.Count);
        alternativas[correta - 1].Correta = true;

        var referencias = new List<Referencia>();
        while (Confirmar(referencias.Count == 0
                   ? "Adicionar uma referência oficial agora?"
                   : "Adicionar outra referência?"))
        {
            referencias.Add(LerReferencia());
        }

        var criada = _servico.Cadastrar(_usuario, enunciado, area, explicacao, alternativas, referencias);
        _saida.WriteLine($"✓ Pergunta #{criada.Id} cadastrada como {Texto.Codigo(criada.Status)} " +
                         $"com {criada.Referencias.Count} referência(s).");
        if (criada.Referencias.Count == 0)
            _saida.WriteLine("  Dica: ela só poderá ser enviada para revisão depois de receber uma referência (opção 2).");
    }

    private void AdicionarReferencia()
    {
        var id = LerId();
        var referencia = LerReferencia();
        var p = _servico.AdicionarReferencia(_usuario, id, referencia);
        _saida.WriteLine($"✓ Referência adicionada. A pergunta #{p.Id} agora tem {p.Referencias.Count}.");
    }

    private void EnviarParaRevisao()
    {
        var p = _servico.EnviarParaRevisao(_usuario, LerId());
        _saida.WriteLine($"✓ Pergunta #{p.Id} enviada para revisão ({Texto.Codigo(p.Status)}).");
    }

    private void Aprovar()
    {
        var p = _servico.Aprovar(_usuario, LerId());
        _saida.WriteLine($"✓ Pergunta #{p.Id} APROVADA por {p.RevisadoPor}. Agora pode ser publicada no app.");
    }

    private void Devolver()
    {
        var id = LerId();
        var motivo = Ler("Motivo da devolução");
        var p = _servico.Devolver(_usuario, id, motivo);
        _saida.WriteLine($"✓ Pergunta #{p.Id} devolvida ao rascunho.");
    }

    private void Buscar()
    {
        _saida.WriteLine("\n== Busca (deixe em branco para ignorar o critério) ==");
        var texto = Ler("Texto (enunciado, explicação ou fonte)");
        var area = EscolherEnumOpcional<AreaDistrito>("Área");
        var status = EscolherEnumOpcional<StatusPergunta>("Status");

        Listar(_servico.Buscar(texto, area, status), "Resultado da busca");
    }

    private void Detalhes()
    {
        var p = _servico.ObterPorId(LerId())
                ?? throw new RegraNegocioException("Pergunta não encontrada.");

        _saida.WriteLine();
        _saida.WriteLine($"Pergunta #{p.Id} — {Texto.Codigo(p.Status)} — {Texto.Codigo(p.Area)}");
        _saida.WriteLine($"  {p.Enunciado}");
        for (var i = 0; i < p.Alternativas.Count; i++)
            _saida.WriteLine($"   {(p.Alternativas[i].Correta ? "✔" : " ")} {i + 1}) {p.Alternativas[i].Texto}");
        _saida.WriteLine($"  Explicação: {p.Explicacao}");

        _saida.WriteLine("  Referências:");
        if (p.Referencias.Count == 0) _saida.WriteLine("   (nenhuma)");
        foreach (var r in p.Referencias)
            _saida.WriteLine($"   • {r.Instituicao}, {r.Titulo} ({r.Ano}) — {r.Url}");

        _saida.WriteLine($"  Criada por {p.CriadoPor} em {Data(p.CriadoEm)}" +
                         (p.RevisadoPor is null ? "" : $" · Revisor: {p.RevisadoPor}") +
                         (p.AprovadaEm is null ? "" : $" · Aprovada em {Data(p.AprovadaEm.Value)}"));

        _saida.WriteLine("  Auditoria:");
        foreach (var e in p.Auditoria)
            _saida.WriteLine($"   {Data(e.Quando)}  {e.Usuario,-16} {e.Acao}");
    }

    private void Listar(IReadOnlyList<Pergunta> perguntas, string titulo)
    {
        _saida.WriteLine($"\n== {titulo}: {perguntas.Count} pergunta(s) ==");
        if (perguntas.Count == 0)
        {
            _saida.WriteLine("Nenhuma pergunta encontrada.");
            return;
        }

        _saida.WriteLine($"{"ID",-4} {"ÁREA",-9} {"STATUS",-11} {"REFS",-4} ENUNCIADO");
        foreach (var p in perguntas)
        {
            _saida.WriteLine($"{p.Id,-4} {Texto.Codigo(p.Area),-9} {Texto.Codigo(p.Status),-11} " +
                             $"{p.Referencias.Count,-4} {Texto.Truncar(p.Enunciado, 60)}");
        }
    }

    // ------------------------------------------------------------ Leitura de dados
    private string Ler(string rotulo)
    {
        _saida.Write($"{rotulo}: ");
        var linha = _entrada.ReadLine();
        if (linha is null) throw new EntradaEncerradaException();
        return linha.Trim();
    }

    private int LerInteiro(string rotulo, int min, int max)
    {
        while (true)
        {
            var texto = Ler($"{rotulo} ({min}-{max})");
            if (int.TryParse(texto, out var valor) && valor >= min && valor <= max) return valor;
            _saida.WriteLine($"  Valor inválido. Digite um número entre {min} e {max}.");
        }
    }

    private int LerId()
    {
        var texto = Ler("ID da pergunta");
        if (!int.TryParse(texto, out var id) || id <= 0)
            throw new ValidacaoException("O ID deve ser um número inteiro positivo.");
        return id;
    }

    private bool Confirmar(string pergunta)
    {
        while (true)
        {
            var r = Ler($"{pergunta} (s/n)").ToLowerInvariant();
            if (r is "s" or "sim") return true;
            if (r is "n" or "nao" or "não") return false;
            _saida.WriteLine("  Responda com 's' ou 'n'.");
        }
    }

    private Referencia LerReferencia()
    {
        _saida.WriteLine("-- Referência oficial --");
        var titulo = Ler("Título do documento");
        var instituicao = Ler("Instituição (ex.: CNEN, AIEA, AMAZUL)");
        var url = Ler("URL");
        var anoTexto = Ler("Ano de publicação");
        if (!int.TryParse(anoTexto, out var ano))
            throw new ValidacaoException("Ano da referência deve ser um número.");
        return new Referencia(titulo, instituicao, url, ano);
    }

    private T EscolherEnum<T>(string rotulo) where T : struct, Enum
    {
        var valores = Enum.GetValues<T>();
        while (true)
        {
            _saida.WriteLine($"{rotulo}:");
            for (var i = 0; i < valores.Length; i++)
                _saida.WriteLine($"  {i + 1}) {Texto.Codigo(valores[i])}");

            var texto = Ler("Escolha");
            if (int.TryParse(texto, out var n) && n >= 1 && n <= valores.Length) return valores[n - 1];
            _saida.WriteLine("  Opção inválida.");
        }
    }

    private T? EscolherEnumOpcional<T>(string rotulo) where T : struct, Enum
    {
        var valores = Enum.GetValues<T>();
        while (true)
        {
            _saida.WriteLine($"{rotulo} (0 = qualquer):");
            for (var i = 0; i < valores.Length; i++)
                _saida.WriteLine($"  {i + 1}) {Texto.Codigo(valores[i])}");

            var texto = Ler("Escolha");
            if (texto is "" or "0") return null;
            if (int.TryParse(texto, out var n) && n >= 1 && n <= valores.Length) return valores[n - 1];
            _saida.WriteLine("  Opção inválida.");
        }
    }

    private static string Data(DateTime d) => d.ToLocalTime().ToString("dd/MM/yyyy HH:mm");
}
