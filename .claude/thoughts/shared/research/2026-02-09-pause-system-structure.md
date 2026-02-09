---
date: 2026-02-09T00:00:00-03:00
researcher: claude
git_commit: dc376b0e9fda1ae88fd93a3371c6878b7bff7371
branch: main
repository: cs2admin
topic: "Estrutura do projeto CS2Admin - Sistema de Pause e Votacao"
tags: [research, codebase, pause, vote, match, timer, disconnect]
status: complete
last_updated: 2026-02-09
last_updated_by: claude
---

# Research: Estrutura do Projeto CS2Admin - Sistema de Pause e Votacao

**Date**: 2026-02-09
**Researcher**: claude
**Git Commit**: dc376b0e9fda1ae88fd93a3371c6878b7bff7371
**Branch**: main
**Repository**: cs2admin

## Pergunta de Pesquisa

Entender como esta estruturado o projeto CS2Admin, focando no sistema de pause e votacao, para habilitar o recurso de tempo de pause apos votacao aprovada. Cada lado (CT/TR) tem 3 pauses de 1 minuto, admin pause e ilimitado, e desconexao gera pause de 2 minutos.

## Resumo

O CS2Admin e um plugin CounterStrikeSharp (C#/.NET) para administracao de servidores CS2. O sistema de pause **atual** e simples: executa `mp_pause_match` / `mp_unpause_match` sem controle de tempo, contagem de pauses por time, ou comportamento diferenciado por tipo de pause. O sistema de votacao usa a UI nativa do Panorama (F1/F2) e suporta votekick, votepause, voterestart e votemap. **Nenhuma das funcionalidades solicitadas existe atualmente** - sera necessario implementar:

1. Contagem de pauses por time (3 por lado)
2. Timer de 1 minuto para pauses de jogador (com display na tela)
3. Pause ilimitado para admin
4. Pause de 2 minutos por desconexao

---

## Achados Detalhados

### 1. Arquitetura do Projeto

```
cs2admin/
├── CS2Admin.cs                    # Plugin principal (BasePlugin)
├── CS2AdminServiceCollection.cs   # Injecao de dependencias
├── Config/
│   └── PluginConfig.cs            # Configuracoes do plugin
├── Commands/
│   ├── AdminCommands.cs           # Comandos de admin (css_pause, css_unpause)
│   ├── VoteCommands.cs            # Comandos de votacao (css_votepause)
│   ├── ChatCommandHandler.cs      # Roteador de comandos de chat (!pause, !votepause)
│   └── AdminManagementCommands.cs # Gerenciamento de admins
├── Services/
│   ├── MatchService.cs            # Logica de pause/unpause, warmup, knife
│   ├── VoteService.cs             # Orquestracao de votos, filtro por time
│   ├── PanoramaVote.cs            # Integracao com UI de votacao do CS2
│   ├── PlayerService.cs           # Acoes de jogador (kick, slay)
│   ├── BanService.cs              # Sistema de bans
│   ├── MuteService.cs             # Sistema de mutes
│   └── AdminService.cs            # Gerenciamento de admins
├── Models/
│   ├── Vote.cs                    # Modelo de voto
│   └── VoteType.cs                # Enum: Kick, Pause, Restart, ChangeMap
├── Handlers/
│   └── PlayerConnectionHandler.cs # Eventos de conexao/desconexao
├── Database/
│   ├── DatabaseService.cs         # Camada de acesso a dados
│   └── DatabaseMigrations.cs      # Migracoes do banco
└── Utils/
    ├── PlayerFinder.cs            # Busca de jogadores
    └── TimeParser.cs              # Parse de duracoes
```

### 2. Sistema de Pause Atual

**Arquivo principal**: `Services/MatchService.cs`

#### Estado Atual (linhas 13, 20)
```csharp
private bool _isPaused;          // Unico campo de estado
public bool IsPaused => _isPaused;
```

#### PauseMatch (linhas 108-119)
```csharp
public void PauseMatch(CCSPlayerController? admin = null)
{
    if (_isPaused) return;
    Server.ExecuteCommand("mp_pause_match");
    _isPaused = true;
    // Log opcional
}
```

#### UnpauseMatch (linhas 121-132)
```csharp
public void UnpauseMatch(CCSPlayerController? admin = null)
{
    if (!_isPaused) return;
    Server.ExecuteCommand("mp_unpause_match");
    _isPaused = false;
    // Log opcional
}
```

**O que NAO existe atualmente:**
- Sem contagem de pauses por time
- Sem timer/countdown durante pause
- Sem diferenciacao entre pause de admin, jogador ou desconexao
- Sem display de tempo na tela durante pause
- Sem auto-unpause apos timeout

### 3. Como o Pause e Invocado

Existem **3 caminhos** para pausar o jogo:

#### Caminho 1: Admin direto (`!pause` ou `css_pause`)
- `ChatCommandHandler.cs:641-657` - `HandlePause()` - requer `@css/generic`
- `AdminCommands.cs:290-303` - `OnPauseCommand()` - requer `@css/generic`
- Ambos chamam `MatchService.PauseMatch(caller)`

#### Caminho 2: Votacao de pause (`!votepause` ou `css_votepause`)
- `ChatCommandHandler.cs:196-204` - `HandleVotePause()`
- `VoteCommands.cs:104-117` - `OnVotePauseCommand()`
- Chama `VoteService.StartVote(VoteType.Pause, player)`
- Voto restrito ao time do iniciador (linhas 94-117 do VoteService)
- Se aprovado: `CS2AdminServiceCollection.cs:80-82` chama `MatchService.PauseMatch()`

#### Caminho 3: Pause na knife round
- `MatchService.EndKnifeRound()` (linha 248) chama `mp_pause_match`
- Este e um caso especial, nao eh pause de jogador

### 4. Fluxo de Votacao de Pause

```
Jogador digita !votepause
  → ChatCommandHandler.OnPlayerChat (linha 90)
  → HandleVotePause (linha 196)
  → VoteService.StartVote(VoteType.Pause, player) (linha 198)
    → Verifica: voto ativo? (linha 55)
    → Verifica: min jogadores? (linha 60)
    → Verifica: cooldown? (linha 66)
    → Filtra jogadores do MESMO TIME (linhas 94-102)
    → Cria RecipientFilter com apenas o time (linhas 104-107)
    → Envia voto Panorama para o time (linha 109)
  → Jogadores votam F1/F2
    → EventVoteCast → PanoramaVote.VoteCast → VoteService.OnVoteAction
  → Voto termina (tempo ou todos votaram)
    → VoteService.OnVoteResult (linha 174)
    → yesPercent >= 60% → aprovado
    → CS2AdminServiceCollection.OnVotePassed → MatchService.PauseMatch()
```

### 5. Sistema de Timers Existente

O plugin ja usa timers em varios locais, estabelecendo um padrao:

```csharp
// Padrao 1: AddTimer do plugin (one-shot)
_plugin.AddTimer(3.0f, () => { /* acao */ });

// Padrao 2: Timer do CounterStrikeSharp (one-shot)
new CounterStrikeSharp.API.Modules.Timers.Timer(flDuration, () => { /* acao */ });
```

**Exemplos existentes:**
- `PanoramaVote.cs:284` - Timer de duracao do voto
- `CS2Admin.cs:69,121,133` - Timers de inicializacao
- `CS2Admin.cs:201,213` - Timers de 0.1-0.2s para spawn
- `PlayerConnectionHandler.cs:75` - Timer de mensagem de boas-vindas

### 6. Display de Mensagens Existente

```csharp
// Chat individual
player.PrintToChat($"{_config.ChatPrefix} mensagem");

// Chat para todos
Server.PrintToChatAll($"{_config.ChatPrefix} mensagem");

// UI nativa de votacao (Panorama)
UserMessage voteStart = UserMessage.FromId(346);  // CS_UM_VoteStart
UserMessage votePass = UserMessage.FromId(347);    // CS_UM_VotePass
UserMessage voteFailed = UserMessage.FromId(348);  // CS_UM_VoteFailed
```

**Nota:** Nao existe atualmente HUD display customizado (tipo center message ou HTML panel). Para mostrar countdown de pause na tela seria necessario usar:
- `Server.ExecuteCommand("mp_ct_default_secondary ...")` - nao aplicavel
- UserMessage customizado
- PrintToCenter (se disponivel no CounterStrikeSharp)
- HUD hint text via game event

### 7. Handler de Desconexao Atual

**Arquivo**: `Handlers/PlayerConnectionHandler.cs:99-109`

```csharp
public HookResult OnPlayerDisconnect(EventPlayerDisconnect @event, GameEventInfo info)
{
    var player = @event.Userid;
    if (player == null || !player.IsValid || player.IsBot || player.IsHLTV)
        return HookResult.Continue;

    // Apenas limpa mute de sessao
    _muteService.RemoveSessionMute(player.SteamID);

    return HookResult.Continue;
}
```

**O que NAO existe:**
- Nenhuma logica de pause automatico por desconexao
- Nenhuma verificacao de time do jogador desconectado
- Nenhum timer de 2 minutos

### 8. Configuracao Atual (PluginConfig.cs)

**Configuracoes relacionadas a pause/vote existentes:**
- `VoteThresholdPercent` (60) - % para aprovar voto
- `VoteDurationSeconds` (30) - Duracao do voto
- `VoteCooldownSeconds` (60) - Cooldown entre votos
- `MinimumVotersRequired` (3) - Min jogadores para votar

**Configuracoes que NAO existem:**
- Limite de pauses por time
- Duracao do pause de jogador
- Duracao do pause por desconexao
- Habilitar/desabilitar pause por desconexao
- Habilitar/desabilitar pause timed

### 9. Estado do MatchService

O MatchService rastreia:
```csharp
private bool _isPaused;             // Match pausada?
private bool _isWarmup;             // Em warmup?
private bool _isKnifeRound;         // Em knife round?
private bool _isKnifeOnly;          // Modo somente faca?
private int _knifeRoundWinnerTeam;  // Vencedor do knife (2=T, 3=CT)
private bool _waitingForSideChoice; // Esperando escolha de lado?
```

**Campos que seriam necessarios para o novo sistema:**
- Contagem de pauses usados por time (CT e TR)
- Tipo de pause ativo (admin/jogador/desconexao)
- Timer reference para countdown
- Tempo restante do pause

### 10. Identificacao de Times

O plugin ja identifica times em varios locais:
```csharp
// No VoteService (linhas 96, 101)
var initiatorTeam = (int)initiator.Team;
// Team: 0=Unassigned, 1=Spectator, 2=Terrorist, 3=Counter-Terrorist

// No VoteService (linhas 258-262) - para side choice
var winnerPlayers = Utilities.GetPlayers()
    .Where(p => (int)p.Team == winnerTeam)
    .ToList();
```

---

## Referencia de Codigo

| Arquivo | Linhas | Descricao |
|---------|--------|-----------|
| `Services/MatchService.cs:108-119` | PauseMatch - executa mp_pause_match |
| `Services/MatchService.cs:121-132` | UnpauseMatch - executa mp_unpause_match |
| `Services/MatchService.cs:13,20` | Estado _isPaused |
| `Services/VoteService.cs:53-139` | StartVote - cria e inicia votacao |
| `Services/VoteService.cs:94-117` | Filtro por time para votekick/votepause |
| `Services/VoteService.cs:174-199` | OnVoteResult - calcula resultado |
| `Services/PanoramaVote.cs:239-291` | SendYesNoVote - envia voto Panorama |
| `Services/PanoramaVote.cs:283-288` | Timer de expiracao do voto |
| `CS2AdminServiceCollection.cs:69-95` | OnVotePassed - executa acao do voto |
| `CS2AdminServiceCollection.cs:80-82` | Caso VoteType.Pause |
| `Commands/AdminCommands.cs:290-303` | Comando admin !pause |
| `Commands/AdminCommands.cs:305-318` | Comando admin !unpause |
| `Commands/ChatCommandHandler.cs:641-657` | Handler chat !pause |
| `Commands/ChatCommandHandler.cs:660-677` | Handler chat !unpause |
| `Commands/ChatCommandHandler.cs:196-204` | Handler chat !votepause |
| `Handlers/PlayerConnectionHandler.cs:99-109` | OnPlayerDisconnect |
| `Config/PluginConfig.cs:11-21` | Config de votacao |
| `Models/VoteType.cs:1-9` | Enum de tipos de voto |
| `Models/Vote.cs:1-19` | Modelo de voto |

## Documentacao de Arquitetura

### Fluxo de Dependencias
```
CS2Admin.cs (Plugin Principal)
  └── CS2AdminServiceCollection.cs (Container de Servicos)
        ├── MatchService.cs         (Pause/Unpause, Warmup, Knife)
        ├── VoteService.cs          (Orquestracao de Votos)
        │     └── PanoramaVote.cs   (UI de Votacao CS2)
        ├── PlayerService.cs        (Kick, Slay, Slap)
        ├── BanService.cs           (Bans)
        ├── MuteService.cs          (Mutes)
        ├── AdminService.cs         (Gerenciamento de Admins)
        ├── AdminCommands.cs        (Comandos css_*)
        ├── VoteCommands.cs         (Comandos css_vote*)
        ├── ChatCommandHandler.cs   (Roteamento de !comandos)
        └── PlayerConnectionHandler.cs (Eventos de conexao)
```

### Padrao de Comunicacao
1. **Chat -> ChatCommandHandler** -> Router por switch/case
2. **ChatCommandHandler -> VoteService** -> Inicia votacao
3. **VoteService -> PanoramaVote** -> Mostra UI na tela
4. **PanoramaVote -> VoteService** (callback) -> Resultado do voto
5. **VoteService -> CS2AdminServiceCollection** (callback) -> Executa acao
6. **CS2AdminServiceCollection -> MatchService** -> PauseMatch()/UnpauseMatch()

## Questoes Abertas

1. **Display na tela**: Qual metodo usar para mostrar countdown de pause? Opcoes: `PrintToCenter`, `PrintToHud`, UserMessage customizado, ou HTML panel
2. **ConVars de pause do CS2**: O CS2 tem `mp_team_timeout_time`, `mp_team_timeout_max`, `sv_pause_timeout` - investigar se podem ser usados nativamente
3. **Pause por desconexao**: Como detectar se a desconexao e temporaria (crash) vs permanente (rage quit)?
4. **Interacao com knife round**: O pause na knife round (linha 248) deveria ser afetado pelo novo sistema?
