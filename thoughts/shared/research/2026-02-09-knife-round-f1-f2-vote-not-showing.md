---
date: 2026-02-09T19:36:24-03:00
researcher: franciscpd
git_commit: f05127bd8447ad1bc3c766d0035ceb859fda506e
branch: main
repository: cs2admin
topic: "Knife round F1/F2 vote not showing - How the side choice vote works"
tags: [research, codebase, knife-round, vote, panorama-vote, f1-f2, side-choice, stay-switch]
status: complete
last_updated: 2026-02-09
last_updated_by: franciscpd
---

# Research: Knife Round F1/F2 Vote Not Showing - How the Side Choice Vote Works

**Date**: 2026-02-09T19:36:24-03:00
**Researcher**: franciscpd
**Git Commit**: f05127bd8447ad1bc3c766d0035ceb859fda506e
**Branch**: main
**Repository**: cs2admin

## Research Question

[CS2Admin] Counter-Terrorists won the knife round! Type !stay or !switch - ele não está apresentando o F1/F2 (the F1/F2 Panorama vote UI is not being shown to players after knife round win)

## Summary

O sistema de votação pós-knife round utiliza **dois mecanismos diferentes**, e atualmente existe uma **discrepância entre a documentação/configuração e a implementação real**:

1. **Implementação real no código**: Usa o sistema `PanoramaVote` nativo do CS2 com F1 (Stay) / F2 (Switch), acionado via `VoteService.StartSideChoiceVote()`. Este é o mecanismo que **realmente funciona** no código.

2. **Documentação/Config default**: O README e a config padrão mencionam `!stay` e `!switch` como chat commands. **Porém, estes comandos NÃO existem no `ChatCommandHandler`** - não há handlers para "stay" nem "switch" no switch de comandos (linha 84 de `ChatCommandHandler.cs`).

O fluxo real é: Knife round termina → `OnRoundEnd` detecta vencedor → 3s delay → `StartSideChoiceVote()` → Panorama Vote UI (F1/F2) aparece para o time vencedor. A mensagem de chat diz "Type !stay or !switch" mas o voto real é via F1/F2 na UI Panorama.

## Detailed Findings

### 1. Mensagem Exibida no Chat Após Vitória no Knife Round

Quando o knife round termina, em `CS2Admin.cs:177-179`:

```csharp
var teamName = winnerTeam == 2 ? "Terrorists" : "Counter-Terrorists";
var message = Config.KnifeRoundWinnerMessage.Replace("{team}", teamName);
Server.PrintToChatAll($"{Config.ChatPrefix} {message}");
```

A config padrão em `PluginConfig.cs:88`:
```csharp
public string KnifeRoundWinnerMessage { get; set; } = "{team} won the knife round! Vote: F1 = Stay, F2 = Switch";
```

A mensagem padrão no código é: **"Vote: F1 = Stay, F2 = Switch"**. Porém, o README documenta em `README.md:224` um formato diferente: `"Type !stay or !switch to choose side."`. Se o usuário editou a config para usar a mensagem do README, a mensagem exibida dirá "Type !stay or !switch" - **mas esses comandos não existem no código**.

### 2. !stay e !switch - Comandos Documentados Mas NÃO Implementados

O README documenta em `README.md:127-128`:
```
| `!stay`   | Winner team | Stay on current side after knife round |
| `!switch` | Winner team | Switch sides after knife round |
```

E no fluxo descrito em `README.md:291`:
```
4. Winning team captain types `!stay` or `!switch` to choose side
```

**Porém**, no `ChatCommandHandler.cs:84-123`, o switch de comandos **não contém** handlers para "stay" nem "switch":
```csharp
var handled = command switch
{
    "command" => HandleHelp(player),
    "votekick" => HandleVoteKick(player, args),
    "votepause" => HandleVotePause(player),
    "voterestart" => HandleVoteRestart(player),
    "votechangemap" or "votemap" => HandleVoteChangeMap(player, args),
    "kick" => HandleKick(player, args),
    // ... outros comandos ...
    "knife" => HandleKnife(player),
    "teams" => HandleTeams(player),
    // ... admin management commands ...
    _ => false  // !stay e !switch caem aqui - NÃO tratados
};
```

**Resultado**: Quando um jogador digita `!stay` ou `!switch`, o comando chega ao switch e cai no `_ => false`, retornando `HookResult.Continue` - ou seja, nada acontece.

### 3. O Mecanismo Real: PanoramaVote F1/F2

O mecanismo que **de fato** é usado é o `VoteService.StartSideChoiceVote()` em `VoteService.cs:249-297`:

```csharp
public bool StartSideChoiceVote(int winnerTeam, Action<bool> onSideChosen)
```

Ele faz:
1. Cria um `RecipientFilter` com apenas jogadores do time vencedor (linhas 258-272)
2. Chama `_panoramaVote.SendYesNoVote()` com 30s de duração (linha 277)
3. Usa `#SFUI_vote_passed_continue_or_swap` como título do vote (linha 280)
4. Detalhe: `$"{teamName} choose side: F1=Stay, F2=Switch"` (linha 281)
5. Callback decide: `stayOnSide = info.yes_votes >= info.no_votes` (linha 286)

### 4. Trigger da Votação: Timer de 3 Segundos

Em `CS2Admin.cs:182-192`:

```csharp
// Delay the vote to ensure round end processing is complete
// and the Panorama vote UI can render properly
AddTimer(3.0f, () =>
{
    if (_services?.MatchService.WaitingForSideChoice == true)
    {
        _services.VoteService.StartSideChoiceVote(winnerTeam, (stayOnSide) =>
        {
            _services.MatchService.ChooseSide(stayOnSide);
        });
    }
});
```

A votação é iniciada com um delay de 3 segundos após o fim do round. Ela só é acionada se `WaitingForSideChoice` for `true`, o que é setado em `MatchService.EndKnifeRound()` (linha 391).

### 5. PanoramaVote - Como o F1/F2 Funciona Internamente

A `CPanoramaVote` em `PanoramaVote.cs` é o componente que:

1. **Inicializa o VoteController** (`Init()`, linha 112): Busca a entidade `vote_controller` do CS2
2. **Envia UserMessage de VoteStart** (`SendVoteStartUM()`, linha 316): Envia `CS_UM_VoteStart` (ID 346) com o título e detalhes
3. **Captura votos via evento** (`VoteCast()`, linha 126): Recebe `EventVoteCast` que é disparado quando o jogador pressiona F1 ou F2
4. **Registra o handler** em `CS2Admin.cs:54`: `RegisterEventHandler<EventVoteCast>(OnVoteCast);`

O UserMessage 346 (CS_UM_VoteStart) é o que faz a UI nativa do CS2 aparecer na tela do jogador. Essa UI mostra automaticamente "F1 = Yes" e "F2 = No".

### 6. Condições que Podem Impedir a Votação de Aparecer

Analisando o fluxo, os seguintes pontos podem causar falha:

**A. VoteController não inicializado**: `PanoramaVote.Init()` busca a entidade `vote_controller` via `Utilities.FindAllEntitiesByDesignerName`. Se esta entidade não existir ou não estiver pronta, `VoteController` será `null`, e `SendYesNoVote()` retornará `false` (linha 241-242).

**B. Vote já em progresso**: Se `m_bIsVoteInProgress` for `true` quando `StartSideChoiceVote()` é chamado, a votação não inicia (linhas 244-248 do PanoramaVote / linha 251-254 do VoteService).

**C. RecipientFilter vazio**: Se nenhum jogador válido do time vencedor for encontrado, a votação não inicia (linhas 264-267 do VoteService e linha 250-251 do PanoramaVote).

**D. WaitingForSideChoice = false**: Se o estado `WaitingForSideChoice` for alterado entre o `EndKnifeRound()` e o timer de 3s, a votação não será iniciada (`CS2Admin.cs:185`).

**E. _services null**: Se o plugin for descarregado/recarregado durante o delay, `_services` pode ser null.

### 7. Fluxo Completo Documentado

```
KNIFE ROUND END
  ↓
OnRoundEnd fires [CS2Admin.cs:167]
  → IsKnifeRound == true
  → winnerTeam = @event.Winner (2=T, 3=CT)
  → MatchService.EndKnifeRound(winnerTeam) [MatchService.cs:388]
    → _knifeRoundWinnerTeam = winnerTeam
    → _waitingForSideChoice = true
    → _isKnifeRound = false
  → Chat: "{team} won the knife round! Vote: F1 = Stay, F2 = Switch"
  ↓
[3 second delay timer]
  ↓
WaitingForSideChoice == true?
  YES → VoteService.StartSideChoiceVote(winnerTeam, callback) [VoteService.cs:249]
    → Filter: only winning team players
    → PanoramaVote.SendYesNoVote(30s, SERVER, ...) [PanoramaVote.cs:239]
      → VoteController != null? → continue
      → m_bIsVoteInProgress == false? → continue
      → Reset VoteController
      → Set VoteController properties
      → InitVoters(filter)
      → SendVoteStartUM(filter) [PanoramaVote.cs:316]
        → UserMessage ID 346 (CS_UM_VoteStart) sent to winning team
        → CS2 Panorama UI shows F1/F2 vote on player screens
      → Timer(30s) → EndVote if not already ended
    ↓
  PLAYER PRESSES F1 or F2
    → CS2 engine fires EventVoteCast
    → CS2Admin.OnVoteCast [CS2Admin.cs:146]
    → PanoramaVote.VoteCast() [PanoramaVote.cs:126]
    → UpdateVoteCounts + CheckForEarlyVoteClose
    ↓
  VOTE ENDS (all votes cast or 30s timeout)
    → EndVote() [PanoramaVote.cs:379]
    → m_VoteResult callback → VoteService closure [VoteService.cs:283-293]
      → stayOnSide = yes_votes >= no_votes
      → onSideChosen(stayOnSide) → MatchService.ChooseSide() [MatchService.cs:395]
        → Restore competitive ConVars
        → mp_swapteams (if switch)
        → mp_unpause_match
        → mp_restartgame 3
```

## Code References

- `CS2Admin.cs:54` - RegisterEventHandler<EventVoteCast>
- `CS2Admin.cs:146-152` - OnVoteCast handler
- `CS2Admin.cs:167-196` - OnRoundEnd handler with knife round detection
- `CS2Admin.cs:182-192` - 3-second delayed vote trigger
- `Services/VoteService.cs:249-297` - StartSideChoiceVote() method
- `Services/VoteService.cs:280` - Vote title: `#SFUI_vote_passed_continue_or_swap`
- `Services/VoteService.cs:281` - Vote detail: `F1=Stay, F2=Switch`
- `Services/VoteService.cs:286` - Vote resolution: `yes_votes >= no_votes`
- `Services/PanoramaVote.cs:60-78` - CPanoramaVote class and constructor
- `Services/PanoramaVote.cs:112-121` - Init() with vote_controller lookup
- `Services/PanoramaVote.cs:126-140` - VoteCast() event handler
- `Services/PanoramaVote.cs:239-291` - SendYesNoVote() core logic
- `Services/PanoramaVote.cs:316-327` - SendVoteStartUM() UserMessage 346
- `Services/MatchService.cs:388-393` - EndKnifeRound() state transition
- `Services/MatchService.cs:395-428` - ChooseSide() with ConVar restoration
- `Config/PluginConfig.cs:88` - KnifeRoundWinnerMessage default value
- `Commands/ChatCommandHandler.cs:84-123` - Command switch (NO stay/switch handlers)
- `README.md:127-128` - Documented !stay/!switch commands
- `README.md:224` - Config example with "Type !stay or !switch"
- `README.md:291` - Flow description mentioning !stay/!switch

## Architecture Documentation

### Dois Mecanismos em Conflito na Documentação

| Aspecto | Implementação no Código | Documentação (README) |
|---------|------------------------|----------------------|
| Mecanismo de voto | Panorama Vote (F1/F2) | Chat commands (!stay/!switch) |
| Quem vota | Todo o time vencedor (maioria) | "Team captain" |
| Resolução | `yes_votes >= no_votes` | N/A (comando direto) |
| Handler no ChatCommandHandler | Não existe | Documentado na linha 127-128 |

### Dependências do PanoramaVote

```
EventVoteCast (CS2 engine event)
  → RegisterEventHandler<EventVoteCast> [CS2Admin.cs:54]
  → OnVoteCast [CS2Admin.cs:146]
  → CPanoramaVote.VoteCast() [PanoramaVote.cs:126]

CVoteController (CS2 entity: "vote_controller")
  → Found via Utilities.FindAllEntitiesByDesignerName [PanoramaVote.cs:116]
  → Must be initialized after map start [CS2Admin.cs:121-125]

UserMessage 346 (CS_UM_VoteStart)
  → Sent to RecipientFilter [PanoramaVote.cs:326]
  → Triggers native CS2 Panorama vote UI on client

UserMessage 347 (CS_UM_VotePass)
  → Sent on vote passed [PanoramaVote.cs:469-478]

UserMessage 348 (CS_UM_VoteFailed)
  → Sent on vote failed/cancelled [PanoramaVote.cs:453-461]
```

## Historical Context (from thoughts/)

- `thoughts/shared/research/2026-02-09-start-command-knife-round-warmup-flow.md` - Pesquisa anterior documentando o fluxo completo do !start, identificou que `StartKnifeRound()` não chamava `mp_warmup_end` (já corrigido no commit b082f18)
- `thoughts/shared/plans/2026-02-09-fix-knife-round-warmup-not-ending.md` - Plano para correção do warmup que resultou na adição de `mp_warmup_end` ao `StartKnifeRound()`

## Related Research

- `thoughts/shared/research/2026-02-09-start-command-knife-round-warmup-flow.md`
- `thoughts/shared/research/2026-02-09-freeze-player-during-menu-navigation.md`

## Open Questions

1. O `VoteController` está sendo inicializado corretamente antes da votação ser chamada? A inicialização acontece em `OnMapStart` com 1s de delay, mas se o knife round for muito curto, pode haver race condition?
2. A entidade `vote_controller` pode ter sido consumida/invalidada por outro vote antes do side choice vote?
3. O UserMessage 346 depende de algum estado do jogo (warmup/live/paused) para ser renderizado pela Panorama UI do CS2 client?
4. O `mp_pause_match` executado em `EndKnifeRound()` pode interferir com a renderização da Panorama vote UI?
