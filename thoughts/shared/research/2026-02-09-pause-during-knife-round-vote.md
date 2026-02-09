---
date: 2026-02-09T19:57:39-03:00
researcher: franciscpd
git_commit: 68fcf1e44e0399fefc8e869b917c420378d4eaa9
branch: main
repository: cs2admin
topic: "Pause match after knife round while winning team votes"
tags: [research, codebase, knife-round, pause, vote, side-choice, match-flow]
status: complete
last_updated: 2026-02-09
last_updated_by: franciscpd
---

# Research: Pause no Match Após Knife Round Durante o Vote

**Date**: 2026-02-09T19:57:39-03:00
**Researcher**: franciscpd
**Git Commit**: 68fcf1e44e0399fefc8e869b917c420378d4eaa9
**Branch**: main
**Repository**: cs2admin

## Research Question

Quero que após o round faca fique em pause enquanto o time vota.

## Summary

Atualmente, quando o knife round termina, o fluxo é:
1. `OnRoundEnd` detecta o vencedor (`CS2Admin.cs:167-196`)
2. `EndKnifeRound()` é chamado (`MatchService.cs:389-394`) — atualiza apenas estado interno, **NÃO pausa o match**
3. Após 3 segundos de delay, o vote de side choice é disparado (`CS2Admin.cs:183-192`)
4. O match **NÃO fica pausado** durante o vote — o CS2 engine segue normalmente

O `mp_pause_match` **não é chamado** em nenhum lugar do fluxo knife→vote. O método `EndKnifeRound()` apenas atualiza flags internas (`_knifeRoundWinnerTeam`, `_waitingForSideChoice`, `_isKnifeRound`).

Quando o vote termina (ou quando `!stay`/`!switch` é usado), `ChooseSide()` (`MatchService.cs:396-430`) executa `mp_unpause_match` (linha 423) e `mp_restartgame 3` (linha 424), mas como o match nunca foi pausado, o `mp_unpause_match` é essencialmente um no-op.

## Detailed Findings

### 1. Fluxo Atual do Fim do Knife Round

#### OnRoundEnd (`CS2Admin.cs:167-196`)

```csharp
if (_services.MatchService.IsKnifeRound)
{
    var winnerTeam = @event.Winner;                    // linha 174
    _services.MatchService.EndKnifeRound(winnerTeam);  // linha 175

    // Broadcast winner message                        // linhas 177-179

    AddTimer(3.0f, () =>                               // linha 183
    {
        if (_services?.MatchService.WaitingForSideChoice == true)
        {
            _services.VoteService.StartSideChoiceVote(winnerTeam, (stayOnSide) =>
            {
                _services.MatchService.ChooseSide(stayOnSide);  // linha 189
            });
        }
    });
}
```

O fluxo após o knife round end:
1. Detecta vencedor e chama `EndKnifeRound()`
2. Broadcast da mensagem de vitória
3. **3 segundos de delay** (timer)
4. Inicia o Panorama vote de 30 segundos

**Nenhum `mp_pause_match` é chamado entre os passos 1-4.**

#### EndKnifeRound (`MatchService.cs:389-394`)

```csharp
public void EndKnifeRound(int winnerTeam)
{
    _knifeRoundWinnerTeam = winnerTeam;   // linha 391
    _waitingForSideChoice = true;          // linha 392
    _isKnifeRound = false;                 // linha 393
}
```

Apenas atualiza estado interno. **Não executa nenhum comando do servidor.**

### 2. Caminho Alternativo: !stay / !switch

O `HandleSideChoice()` em `ChatCommandHandler.cs:832-858`:

```csharp
private bool HandleSideChoice(CCSPlayerController player, bool stayOnSide)
{
    if (!_matchService.WaitingForSideChoice) { ... }      // linha 834
    if ((int)player.Team != _matchService.KnifeRoundWinnerTeam) { ... } // linha 840

    // Cancel Panorama vote if running
    if (_voteService.PanoramaVote.IsVoteInProgress())
    {
        _voteService.PanoramaVote.CancelVote();            // linha 849
    }

    _matchService.ChooseSide(stayOnSide);                  // linha 856
}
```

Tanto o Panorama vote callback quanto `!stay`/`!switch` chamam `ChooseSide()` para finalizar.

### 3. ChooseSide — Transição para Live (`MatchService.cs:396-430`)

```csharp
public void ChooseSide(bool stayOnSide, CCSPlayerController? admin = null)
{
    if (!_waitingForSideChoice) return;                   // linha 398

    _waitingForSideChoice = false;                        // linha 400
    _isKnifeOnly = false;                                 // linha 401

    // Reset to normal settings
    Server.ExecuteCommand("mp_free_armor 0");             // linha 404
    // ... mais ConVars ...                               // linhas 405-414

    if (!stayOnSide)
    {
        Server.ExecuteCommand("mp_swapteams");            // linha 419
    }

    // Unpause and restart for actual match
    Server.ExecuteCommand("mp_unpause_match");            // linha 423
    Server.ExecuteCommand("mp_restartgame 3");            // linha 424
}
```

O `mp_unpause_match` na linha 423 existe para desfazer o pause, mas como o match **nunca é pausado** no fluxo atual, esse comando é um no-op.

### 4. Sistema de Pause Existente

O `MatchService` tem um sistema de pause completo (`MatchService.cs:139-286`):

- `PauseMatch()` (linha 139) — pausa admin via `mp_pause_match`
- `UnpauseMatch()` (linha 153) — unpausa admin via `mp_unpause_match`
- `PauseMatchTimed()` (linha 172) — pausa com timer (usado para vote pause e disconnect pause)
- `ForceUnpause()` (linha 268) — unpausa forçada interna
- Estado: `_isPaused`, `_activePauseType`, `_pauseTeam`, `_pauseRemainingSeconds`

**Importante**: `PauseMatchTimed()` (linha 176) rejeita pauses durante knife round ou side choice:
```csharp
if (_isWarmup || _isKnifeRound || _waitingForSideChoice)
    return (false, "Can only pause during a live match.");
```

Isso significa que o sistema de pause existente (timed pause) **não pode ser usado** durante o `WaitingForSideChoice` state.

### 5. O que Acontece no CS2 Engine Sem Pause

Sem `mp_pause_match`:
- O CS2 engine continua normalmente após o round end
- Um novo round começa automaticamente após o freeze time
- Os jogadores vão spawnar e poder se mover enquanto o vote está acontecendo
- O vote de 30 segundos do Panorama funciona normalmente sobre isso

## Code References

- `CS2Admin.cs:167-196` — OnRoundEnd handler que detecta fim do knife round
- `CS2Admin.cs:175` — Chama EndKnifeRound(winnerTeam)
- `CS2Admin.cs:183-192` — Timer de 3s + StartSideChoiceVote()
- `MatchService.cs:389-394` — EndKnifeRound() apenas atualiza flags
- `MatchService.cs:396-430` — ChooseSide() restaura ConVars + mp_unpause_match + mp_restartgame 3
- `MatchService.cs:423` — mp_unpause_match (no-op atualmente porque match nunca é pausado)
- `MatchService.cs:172-177` — PauseMatchTimed() rejeita durante _waitingForSideChoice
- `ChatCommandHandler.cs:832-858` — HandleSideChoice() via !stay/!switch
- `VoteService.cs:249-297` — StartSideChoiceVote() com Panorama vote

## Architecture Documentation

### Máquina de Estados Durante Side Choice

Quando `EndKnifeRound()` é chamado:
- `_isKnifeRound` = false
- `_isKnifeOnly` = true (permanece true até `ChooseSide()`)
- `_waitingForSideChoice` = true
- `_isPaused` = false (nunca muda)

O match **não** é pausado no CS2 engine. O `_waitingForSideChoice` flag apenas controla o estado do plugin, não o estado do jogo.

### Timeline do Fluxo Atual

```
Knife Round Termina
  │
  ├─ T=0s: OnRoundEnd detecta vencedor
  │         EndKnifeRound() atualiza flags
  │         Broadcast da mensagem de vitória
  │
  ├─ T=3s: StartSideChoiceVote() (Panorama 30s)
  │         OU jogador pode usar !stay/!switch
  │
  │  [CS2 engine continua normalmente - novo round pode começar]
  │
  ├─ T=3-33s: Vote em andamento / Esperando !stay ou !switch
  │
  └─ T=?s: ChooseSide()
            mp_unpause_match (no-op)
            mp_restartgame 3
```

## Open Questions

Nenhuma — o fluxo está documentado de forma completa.
