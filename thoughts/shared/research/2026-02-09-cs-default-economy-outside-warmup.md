---
date: 2026-02-09T00:00:00-03:00
researcher: franciscpd
git_commit: b082f180fd755ee2fbc9f0824b1296f9de017841
branch: main
repository: cs2admin
topic: "Economia padrão do CS quando não está em warmup - como funciona atualmente"
tags: [research, codebase, economy, money, warmup, match, competitive]
status: complete
last_updated: 2026-02-09
last_updated_by: franciscpd
---

# Research: Economia padrão do CS quando não está em warmup

**Date**: 2026-02-09
**Researcher**: franciscpd
**Git Commit**: b082f180fd755ee2fbc9f0824b1296f9de017841
**Branch**: main
**Repository**: cs2admin

## Research Question
É possível seguir a economia padrão do CS quando não estiver em warmup?

## Summary

Sim, o plugin **já segue a economia padrão do CS2 competitivo** quando não está em warmup. Os valores definidos pelo plugin ao sair do warmup (`EndWarmup`, `ChooseSide`, `DisableKnifeOnly`) são exatamente os valores padrão do CS2 competitivo. O plugin **não interfere** na economia durante a partida ao vivo — ele apenas define os ConVars iniciais e o motor do CS2 cuida do resto (loss bonus progressivo, bônus de round, bônus de planta de bomba, etc.).

## Detailed Findings

### Como a economia é gerenciada pelo plugin

O plugin controla a economia exclusivamente através de ConVars do CS2 — ele **não manipula dinheiro dos jogadores diretamente** durante a partida ao vivo. A manipulação direta de dinheiro (`InGameMoneyServices.Account`) só acontece durante o warmup.

Existem 4 estados de economia no plugin:

#### 1. Warmup (dinheiro ilimitado)
**Arquivo**: `Services/MatchService.cs:69-98`
```
mp_startmoney = 60000  (configurável via WarmupMoney)
mp_maxmoney = 60000
mp_afterroundmoney = 60000
mp_buytime = 9999
mp_free_armor = 2
```
- Além dos ConVars, o plugin também força dinheiro diretamente em cada jogador via `GiveMoneyToPlayer()` (linha 344) e `GiveMoneyToAllPlayers()` (linha 332)
- No spawn de cada jogador durante warmup, o dinheiro é dado novamente (`CS2Admin.cs:200-206`)

#### 2. Match ao vivo (economia padrão CS2 competitivo)
**Arquivo**: `Services/MatchService.cs:100-123` (EndWarmup) e `Services/MatchService.cs:125-136` (StartMatch)
```
mp_startmoney = 800
mp_maxmoney = 16000
mp_afterroundmoney = 0
mp_buytime = 20
mp_free_armor = 0
```
- Esses são os **valores padrão do CS2 competitivo**
- `mp_afterroundmoney = 0` significa que o motor do CS2 usa seu próprio sistema de economia (loss bonus, round win bonus, etc.)
- O plugin **NÃO manipula dinheiro dos jogadores** durante a partida — toda a economia é gerida pelo motor do jogo

#### 3. Knife round (sem dinheiro)
**Arquivo**: `Services/MatchService.cs:356-385`
```
mp_startmoney = 0
mp_maxmoney = 0
mp_buytime = 0
mp_free_armor = 1
```

#### 4. Pós-knife / Escolha de lado
**Arquivo**: `Services/MatchService.cs:397-428` (ChooseSide)
```
mp_startmoney = 800
mp_maxmoney = 16000
mp_buytime = 20
mp_free_armor = 0
```
- Retorna aos mesmos valores competitivos e faz `mp_restartgame 3`

### O que o motor do CS2 controla automaticamente

Quando `mp_afterroundmoney = 0` (que é o valor definido pelo plugin ao sair do warmup), o motor do CS2 gerencia automaticamente:

- **Loss bonus progressivo**: $1400 → $1900 → $2400 → $2900 → $3400 (incrementa a cada derrota consecutiva)
- **Round win bonus**: $3250 (vitória por eliminação/tempo), $3500 (vitória por detonação da bomba)
- **Bônus de planta de bomba**: $300 para quem planta
- **Kill rewards**: variam por arma ($300 padrão, $100 AWP, $600 SMGs, etc.)
- **Half-time reset**: o motor reseta o dinheiro para `mp_startmoney` na troca de lado

### Onde o plugin NÃO interfere na economia durante o match

Verificando todo o código, durante uma partida ao vivo (não-warmup, não-knife):
- `OnPlayerSpawn()` (`CS2Admin.cs:191-207`) — só dá dinheiro se `IsWarmup == true`
- `GiveMoneyToPlayer()` (`MatchService.cs:344-354`) — só é chamado durante warmup
- Não há nenhum event handler que modifique dinheiro durante rounds normais
- O sistema de pause (`PauseMatch`/`UnpauseMatch`) não afeta a economia

### ConVars não definidos explicitamente pelo plugin

O plugin **não define** os seguintes ConVars de economia, o que significa que ficam com os valores padrão do servidor/gametype:

- `cash_team_win_by_time_running_out_bomb` (padrão: 3250)
- `cash_team_win_by_defusing_bomb` (padrão: 3250)
- `cash_team_win_by_hostage_rescue` (padrão: 3500)
- `cash_team_loser_bonus` (padrão: 1400)
- `cash_team_loser_bonus_consecutive_rounds` (padrão: 500)
- `cash_team_planted_bomb_but_defused` (padrão: 800)
- `cash_team_terrorist_win_bomb` (padrão: 3500)
- `cash_player_killed_enemy_default` (padrão: 300)
- `cash_player_bomb_planted` (padrão: 300)
- E todos os outros ConVars `cash_*`

Isso é **correto** — ao não definir esses valores, o motor do CS2 usa seus valores padrão competitivos.

## Code References

- `Services/MatchService.cs:69-98` — StartWarmup() define economia de warmup
- `Services/MatchService.cs:100-123` — EndWarmup() reseta para economia competitiva
- `Services/MatchService.cs:111-113` — Valores específicos: 800/16000/0
- `Services/MatchService.cs:125-136` — StartMatch() chama EndWarmup() + restartgame
- `Services/MatchService.cs:344-354` — GiveMoneyToPlayer() (só usado em warmup)
- `Services/MatchService.cs:356-385` — StartKnifeRound() economia zerada
- `Services/MatchService.cs:397-428` — ChooseSide() restaura economia competitiva
- `CS2Admin.cs:191-207` — OnPlayerSpawn() check de IsWarmup antes de dar dinheiro

## Architecture Documentation

O plugin segue um padrão claro de "configurar e delegar":
1. **Durante warmup**: O plugin controla ativamente a economia (ConVars + manipulação direta de dinheiro)
2. **Durante partida ao vivo**: O plugin apenas configura os ConVars iniciais e deixa o motor do CS2 gerenciar toda a economia automaticamente
3. **Transições de estado**: Cada mudança de estado (warmup→match, warmup→knife, knife→match) tem um conjunto explícito de ConVars que são aplicados

## Resposta Direta

**Sim, o plugin já segue a economia padrão do CS2 competitivo quando não está em warmup.** Os valores `mp_startmoney=800`, `mp_maxmoney=16000`, e `mp_afterroundmoney=0` são os valores competitivos padrão, e o motor do CS2 cuida de todo o resto (loss bonus, round win money, kill rewards, etc.) automaticamente.

O plugin não faz nenhuma intervenção na economia durante a partida ao vivo — ele só toca nos ConVars na transição entre estados (warmup → match, warmup → knife, knife → match).

## Open Questions

- Nenhuma questão em aberto — a economia padrão do CS2 já está sendo respeitada quando não está em warmup.
