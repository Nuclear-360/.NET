using System.Text;
using Nuclear360.Curadoria.Domain;
using Nuclear360.Curadoria.Repositories;
using Nuclear360.Curadoria.Services;
using Nuclear360.Curadoria.UI;

Console.OutputEncoding = Encoding.UTF8;
Console.InputEncoding = Encoding.UTF8;

// Arquivo de dados: --dados <caminho> ou NUCLEAR360_DADOS; padrão ./data/perguntas.json
var caminho = ObterCaminho(args);
var primeiraExecucao = !File.Exists(caminho);

try
{
    var repositorio = new JsonPerguntaRepository(caminho);
    var servico = new CuradoriaService(repositorio);

    if (repositorio.Aviso is not null)
        Console.WriteLine($"⚠ {repositorio.Aviso}\n");

    if (primeiraExecucao)
    {
        DadosExemplo.Carregar(servico);
        Console.WriteLine($"Primeira execução: 3 perguntas de exemplo criadas em '{caminho}'.\n");
    }

    new Aplicacao(servico, Console.In, Console.Out).Executar();
    return 0;
}
catch (PersistenciaException ex)
{
    Console.Error.WriteLine($"Erro de armazenamento: {ex.Message}");
    return 1;
}
catch (Exception ex)
{
    // Última barreira: nunca mostrar stack trace ao usuário.
    Console.Error.WriteLine($"Erro inesperado ao iniciar o painel: {ex.Message}");
    return 1;
}

static string ObterCaminho(string[] args)
{
    var i = Array.IndexOf(args, "--dados");
    if (i >= 0 && i + 1 < args.Length) return args[i + 1];

    var env = Environment.GetEnvironmentVariable("NUCLEAR360_DADOS");
    return string.IsNullOrWhiteSpace(env) ? Path.Combine("data", "perguntas.json") : env;
}
