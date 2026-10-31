# Nuclear 360 — Painel de Curadoria (.NET)

## Grupo

Integrantes:
Lucas Shida Rodrigues da Costa RM568194
Matheus Guimarães Rosa RM567912
Pedro Henrique Pinheiro Falchi RM566967

> Turma: 2TDSPS

## A solução

**Nuclear 360** é um app educativo gamificado sobre tecnologia nuclear (desafio AMAZUL). Em um Brasil de 2070 sem tecnologia nuclear, o jogador "reacende" a cidade distrito por distrito (Saúde, Agro, Indústria, Pesquisa, Energia) derrubando mitos com fatos e fontes oficiais.

Uma plataforma contra desinformação **não pode publicar desinformação**. Por isso, todo conteúdo passa por um banco curado: *nenhuma pergunta chega ao jogador sem referência oficial e sem aprovação de um curador*.

## O que foi desenvolvido nesta Sprint

O **Painel de Curadoria**, uma aplicação de console que permite a professores e curadores montar e aprovar as perguntas do quiz. É a funcionalidade do sistema que fica a cargo do .NET e **não repete** a API Java (que cuida do jogo: distritos, missões e correção de respostas).

- **Entidade:** `Pergunta` (enunciado, área, explicação, alternativas, referências, status e trilha de auditoria).
- **Cadastro** de perguntas e de referências oficiais.
- **Consulta:** listagem de todas, busca por texto (ignora acento e maiúscula; procura também na fonte), por área e por status, e tela de detalhes com auditoria.
- **Fluxo de curadoria:** `RASCUNHO → EM_REVISAO → APROVADA` (com devolução ao rascunho).
- **Persistência** em arquivo JSON (`data/perguntas.json`), mantida entre execuções.

### Regras de negócio

1. Pergunta nasce como `RASCUNHO`.
2. **Sem referência não vai para revisão** (e nem é aprovada).
3. **Só o perfil CURADOR aprova ou devolve**, e **nunca a própria pergunta**.
4. Devolver exige motivo; referências só entram em rascunho; mesma URL não se repete.
5. Perfil JOGADOR não cadastra. Enunciado duplicado é recusado.
6. Toda ação fica na **auditoria** (quem, quando, o quê).

### Validações

- Enunciado: 15–300 caracteres · Explicação: 20–600 caracteres.
- 2 a 4 alternativas, sem repetição, **exatamente uma correta**.
- Referência: título, instituição, URL `http(s)` válida e ano entre 1900 e o ano atual.
- Apelido: 3–20 caracteres, sem espaços (mesma regra do app mobile).

### Tratamento de erros

Nenhum erro derruba a aplicação: validações e regras viram mensagens claras (`try/catch` na interface), JSON corrompido é isolado em arquivo `.corrompido-*` e a base reinicia vazia com aviso, e falha de gravação em disco não deixa memória e arquivo divergentes.

## Como executar

Pré-requisito: [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```bash
git clone <URL-DO-REPOSITORIO>
cd nuclear360-curadoria-dotnet

dotnet run --project src/Nuclear360.Curadoria        
dotnet run --project tests/Nuclear360.Curadoria.Testes   
```

Na primeira execução são criadas **3 perguntas de exemplo** (uma em cada status). Para usar outro arquivo de dados: `--dados <caminho>` ou variável `NUCLEAR360_DADOS`.

**Roteiro rápido de demonstração:**
1. Entre como `prof.marcos` · `PROFESSOR` → opção **1** para cadastrar uma pergunta (sem referência).
2. Opção **3** (enviar para revisão) → o sistema **recusa**: falta fonte.
3. Opção **2** adiciona a referência → opção **3** agora funciona.
4. Opção **9**, entre como `curador.amazul` · `CURADOR` → opção **4** aprova.
5. Opção **7** busca por texto/área/status e opção **8** mostra a auditoria. Feche e abra de novo: os dados continuam lá.

## Arquitetura

```
src/Nuclear360.Curadoria/
├── Domain/          Pergunta, Referencia, Alternativa, Usuario, enums, exceções
│                    → atributos, validações e regras de negócio (sem dependência de I/O)
├── Services/        CuradoriaService (casos de uso) e DadosExemplo
├── Repositories/    IPerguntaRepository + JsonPerguntaRepository (persistência)
├── UI/              Aplicacao (menu de console)
└── Program.cs       composição das camadas
tests/Nuclear360.Curadoria.Testes/   42 testes (domínio, regras, busca, persistência e UI)
```

A UI depende do serviço, e o serviço depende da **interface** `IPerguntaRepository`. Assim, o JSON pode ser trocado por **Entity Framework Core + Oracle** na próxima sprint implementando só essa interface.

## Como o .NET se aplica à solução do grupo

No Nuclear 360, o .NET é a camada de **curadoria e governança do conteúdo**: é nele que professores e curadores constroem, revisam e aprovam as perguntas e referências que alimentam o app e a API Java, garantindo que nenhuma informação chegue ao jogador sem fonte oficial. O C# permite modelar essas regras de forma fortemente tipada (status, perfis, validações) e testável; o mesmo domínio escrito aqui pode ser reaproveitado na API ASP.NET Core, no painel web de curadoria e na ingestão das leituras do totem Geiger (IoT) nas próximas sprints, ligando o .NET ao banco relacional do projeto via Entity Framework Core.

## Segurança

Nenhuma credencial é versionada (`.env` e arquivos de configuração local estão no `.gitignore`). O sistema coleta apenas apelido e perfil, sem dados pessoais (LGPD).
