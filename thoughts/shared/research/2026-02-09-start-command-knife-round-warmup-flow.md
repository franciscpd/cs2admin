---
date: 2026-02-09T18:34:30-03:00
researcher: franciscpd
git_commit: e159f535d97df59df2034ee062c4deeb7f0a3891
branch: main
repository: cs2admin
topic: "!start command knife round flow - why killing opponent doesn't finalize and stays showing as warmup"
tags: [research, codebase, start-command, knife-round, warmup, match-flow, round-end]
status: complete
last_updated: 2026-02-09
last_updated_by: franciscpd
---

# Research: Fluxo do comando !start - Knife Round, Warmup e Finalização

**Date**: 2026-02-09T18:34:30-03:00
**Researcher**: franciscpd
**Git Commit**: e159f535d97df59df2034ee062c4deeb7f0a3891
**Branch**: main
**Repository**: cs2admin

## Research Question

Quando se executa o comando `!start`, ele entra na partida de faca, mas ao matar o adversário não finaliza e fica aparecendo como aquecimento.

## Summary

O fluxo do `!start` com knife round habilitado funciona assim: o admin executa `!start` → `HandleStart()` verifica permissões, warmup e player count → chama `MatchService.StartKnifeRound()` que configura ConVars de knife e executa `mp_restartgame 1` (duas vezes com delay de 3s). Quando o round termina, o handler `OnRoundEnd` em `CS2Admin.cs:172` verifica `IsKnifeRound`, obtém o vencedor de `@event.Winner`, chama `EndKnifeRound()` que pausa a partida, e então inicia o vote de side choice.

O ponto central documentado aqui é: **o `StartKnifeRound()` define `_isWarmup = false` (linha 358 de MatchService.cs), mas NÃO executa `mp_warmup_end`**. A warmup é encerrada logicamente no estado interno do plugin (`_isWarmup = false`), mas o ConVar de warmup do CS2 pode não ser atualizado corretamente. O método `EndWarmup()` (que executa `mp_warmup_end` na linha 117) NÃO é chamado dentro do `StartKnifeRound()`.

## Detailed Findings

### 1. Registro do Comando !start

O comando `!start` é registrado como chat command no switch de `ChatCommandHandler.cs:108`:

```csharp
"start" => HandleStart(player),
```

E como console command em `AdminCommands.cs:49`:
```csharp
plugin.AddCommand("css_start", "Start the match", OnStartCommand);
```

### 2. HandleStart - Validações e Decisão (`ChatCommandHandler.cs:694-729`)

O handler executa as seguintes verificações em ordem:

1. **Permissão admin** (linha 696): Verifica `@css/generic`
2. **Estado de warmup** (linha 702): Verifica `_matchService.IsWarmup` - retorna erro se não estiver em warmup
3. **Contagem de jogadores** (linha 708): Verifica `PlayerFinder.GetPlayerCount()` contra `_config.MinPlayersToStart` (default: 2)
4. **Decisão knife round** (linha 715): Se `_config.EnableKnifeRound` é `true`:
   - Broadcast de mensagem de knife round (linha 717)
   - Broadcast de quem iniciou (linha 718)
   - Chama `_matchService.StartKnifeRound(player)` (linha 719)
5. **Sem knife round** (linha 721): Se `EnableKnifeRound` é `false`:
   - Chama `_matchService.StartMatch(player)` (linha 725)

### 3. StartKnifeRound - Configuração da Partida de Faca (`MatchService.cs:356-392`)

#### Estado interno atualizado:
- Linha 358: `_isWarmup = false`
- Linha 359: `_isKnifeRound = true`
- Linha 360: `_isKnifeOnly = true`
- Linha 361: `_waitingForSideChoice = false`
- Linha 362: `_knifeRoundWinnerTeam = 0`

#### ConVars configurados (linhas 365-374):
```
mp_respawn_on_death_ct 0
mp_respawn_on_death_t 0
mp_free_armor 1
mp_give_player_c4 0
mp_ct_default_secondary ""
mp_t_default_secondary ""
mp_buytime 0
mp_buy_during_immunity_time 0
mp_startmoney 0
mp_maxmoney 0
```

#### Restart do jogo:
- Linha 377: `mp_restartgame 1` (primeiro restart para aplicar settings)
- Linha 380-386: Timer de 3 segundos + segundo `mp_restartgame 1` (se ainda em knife round)

#### O que NÃO é feito:
- **NÃO executa `mp_warmup_end`** - o warmup do CS2 engine pode não ser efetivamente encerrado
- **NÃO chama `EndWarmup()`** - apenas define `_isWarmup = false` no estado interno do plugin

### 4. Comparação com StartMatch (sem knife) (`MatchService.cs:125-136`)

Quando o knife round está desabilitado, o `StartMatch()` chama `EndWarmup()` na linha 127, que executa `mp_warmup_end` na linha 117 de `EndWarmup()`. Este é o caminho que efetivamente encerra o warmup do engine do CS2.

### 5. EndWarmup - O Encerramento Completo do Warmup (`MatchService.cs:100-123`)

O `EndWarmup()` faz a transição completa:
- Linha 102: Verifica se está em warmup (`if (!_isWarmup) return;`)
- Linha 104: `_isWarmup = false`
- Linhas 107-116: Reseta ConVars para valores competitivos
- **Linha 117: `mp_warmup_end`** - o comando que efetivamente encerra o warmup no CS2 engine

### 6. Detecção de Fim do Knife Round (`CS2Admin.cs:167-189`)

O handler `OnRoundEnd` é registrado na linha 60 apenas se `Config.EnableWarmupMode` é `true`:
```csharp
RegisterEventHandler<EventRoundEnd>(OnRoundEnd);
```

Quando o round termina:
1. Linha 172: Verifica `_services.MatchService.IsKnifeRound`
2. Linha 174: Obtém o time vencedor de `@event.Winner` (2 = T, 3 = CT)
3. Linha 175: Chama `_services.MatchService.EndKnifeRound(winnerTeam)`
4. Linhas 177-179: Broadcast da mensagem de vitória
5. Linhas 182-185: Inicia vote de side choice

### 7. EndKnifeRound - Pausa e Espera (`MatchService.cs:394-402`)

```csharp
public void EndKnifeRound(int winnerTeam)
{
    _knifeRoundWinnerTeam = winnerTeam;
    _waitingForSideChoice = true;
    _isKnifeRound = false;
    Server.ExecuteCommand("mp_pause_match");
}
```

### 8. Side Choice Vote (`VoteService.cs:249-297`)

- Cria filtro com apenas jogadores do time vencedor (linhas 257-262)
- Panorama vote de 30 segundos (linha 278)
- F1 = Stay (Yes), F2 = Switch (No)
- Maioria simples decide: `stayOnSide = info.yes_votes >= info.no_votes` (linha 286)
- Callback chama `MatchService.ChooseSide(stayOnSide)` (CS2Admin.cs:184)

### 9. ChooseSide - Transição para Partida Live (`MatchService.cs:404-435`)

Após o vote:
- Linha 408: `_waitingForSideChoice = false`
- Linha 409: `_isKnifeOnly = false`
- Linhas 412-419: Restaura ConVars competitivos (armor, C4, pistolas, buy time, money)
- Linha 421-425: Se switch, executa `mp_swapteams`
- Linha 428: `mp_unpause_match`
- Linha 429: `mp_restartgame 3`

### 10. Weapon Stripping no Spawn (`CS2Admin.cs:191-224`)

O `OnPlayerSpawn` garante knife-only:
- Linha 212: Verifica `_services.MatchService.IsKnifeOnly`
- Linha 214-220: Timer de 0.2s → `StripPlayerWeapons(player)`

O `StripPlayerWeapons` (`MatchService.cs:504-532`) remove todas as armas exceto as que contêm "knife" ou "bayonet" no nome.

### 11. Bloqueio de Compras (`CS2Admin.cs:232-242`)

Listeners de "buy", "autobuy", "rebuy" registrados em Pre mode (linhas 41-43). Se `IsKnifeOnly` é `true`, retorna `HookResult.Handled` bloqueando a compra.

## Code References

- `Commands/ChatCommandHandler.cs:694-729` - HandleStart() com validações e decisão knife/match
- `Commands/ChatCommandHandler.cs:108` - Registro do comando "start"
- `Commands/AdminCommands.cs:49` - Registro do console command css_start
- `Services/MatchService.cs:356-392` - StartKnifeRound() com state e ConVars
- `Services/MatchService.cs:358` - `_isWarmup = false` sem `mp_warmup_end`
- `Services/MatchService.cs:100-123` - EndWarmup() com `mp_warmup_end`
- `Services/MatchService.cs:125-136` - StartMatch() que chama EndWarmup()
- `Services/MatchService.cs:394-402` - EndKnifeRound() com mp_pause_match
- `Services/MatchService.cs:404-435` - ChooseSide() com restauração de ConVars
- `Services/MatchService.cs:504-532` - StripPlayerWeapons()
- `Services/MatchService.cs:550-558` - ResetKnifeRoundState()
- `Services/VoteService.cs:249-297` - StartSideChoiceVote()
- `CS2Admin.cs:57-61` - Registro condicional de EventRoundStart/EventRoundEnd
- `CS2Admin.cs:154-165` - OnRoundStart handler (broadcast knife message)
- `CS2Admin.cs:167-189` - OnRoundEnd handler (detecção do vencedor do knife)
- `CS2Admin.cs:191-224` - OnPlayerSpawn handler (weapon strip)
- `CS2Admin.cs:232-242` - OnBuyCommand handler (bloqueio de compras)
- `Config/PluginConfig.cs:81-88` - Configurações de knife round

## Architecture Documentation

### Máquina de Estados (Boolean Flags)

O sistema utiliza boolean flags independentes em `MatchService` para rastrear a fase atual:

| Flag | Warmup | Knife Round | Side Selection | Live Match |
|------|--------|-------------|----------------|------------|
| `_isWarmup` | `true` | `false` | `false` | `false` |
| `_isKnifeRound` | `false` | `true` | `false` | `false` |
| `_isKnifeOnly` | `false` | `true` | `true` | `false` |
| `_waitingForSideChoice` | `false` | `false` | `true` | `false` |

### Fluxo Completo

```
MAP LOAD → OnMapStart() [CS2Admin.cs:114]
  → ResetKnifeRoundState() + ResetPauseState() [CS2Admin.cs:130-131]
  → [2s delay] → StartWarmup() [CS2Admin.cs:139]

WARMUP (respawn, $60000, armor+helmet, buy anywhere)
  ↓
Admin !start [ChatCommandHandler.cs:694]
  ↓
EnableKnifeRound == true?
  YES → StartKnifeRound() [MatchService.cs:356]
    → _isWarmup = false (SEM mp_warmup_end)
    → ConVars knife + mp_restartgame 1 (×2)
    ↓
  KNIFE ROUND (no buy, no pistol, no C4, knife only)
    ↓
  EventRoundEnd fires [CS2Admin.cs:167]
    → @event.Winner == team vencedor
    → EndKnifeRound(winner) [MatchService.cs:394]
    → mp_pause_match
    ↓
  SIDE CHOICE VOTE (30s, F1=Stay, F2=Switch)
    ↓
  ChooseSide() [MatchService.cs:404]
    → Restaura ConVars competitivos
    → mp_swapteams (se switch)
    → mp_unpause_match + mp_restartgame 3
    ↓
  LIVE MATCH ($800, buy 20s, normal rules)
```

## Historical Context (from thoughts/)

- `thoughts/shared/research/2026-02-09-admin-command-rename-and-menu-input-blocking.md` - Pesquisa anterior sobre renomeação de comandos admin

## Open Questions

1. O `mp_restartgame 1` executado em `StartKnifeRound()` (linhas 377, 384) encerra implicitamente o warmup do CS2 engine, ou o CS2 continua considerando a fase como warmup sem o comando explícito `mp_warmup_end`?
2. O `@event.Winner` em `EventRoundEnd` retorna corretamente o time vencedor durante um round de knife dentro do warmup do engine?
3. O handler `OnRoundEnd` é disparado normalmente pelo CS2 engine durante rounds jogados em modo warmup, ou o engine trata rounds de warmup de forma diferente?
