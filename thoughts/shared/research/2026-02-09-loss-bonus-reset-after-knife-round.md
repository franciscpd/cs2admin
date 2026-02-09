---
date: 2026-02-09T19:45:14-03:00
researcher: franciscpd
git_commit: f05127bd8447ad1bc3c766d0035ceb859fda506e
branch: main
repository: cs2admin
topic: "Após o round faca o loss bonus deve ser resetado"
tags: [research, codebase, knife-round, loss-bonus, economy, mp_consecutive_loss_forgiveness]
status: complete
last_updated: 2026-02-09
last_updated_by: franciscpd
---

# Research: Loss Bonus Reset Após o Knife Round

**Date**: 2026-02-09T19:45:14-03:00
**Researcher**: franciscpd
**Git Commit**: f05127bd8447ad1bc3c766d0035ceb859fda506e
**Branch**: main
**Repository**: cs2admin

## Research Question

Após o round faca o loss bonus deve ser resetado.

## Summary

O plugin **não define nenhum ConVar explícito para resetar o loss bonus** após o knife round. A transição knife→live acontece em `ChooseSide()` (`MatchService.cs:395-428`), que restaura ConVars competitivos e executa `mp_restartgame 3`. O `mp_restartgame` reseta o estado do match, incluindo money dos jogadores, mas **o comportamento exato do reset do loss streak counter pelo `mp_restartgame` não é explicitamente garantido pela documentação do CS2**.

Existem ConVars específicos do CS2 que controlam o loss bonus:
- **`mp_starting_losses`** — define o valor inicial do loss streak counter no início de cada half (default: 0, competitivo usa 1)
- **`mp_consecutive_loss_aversion`** — controla como vitórias afetam o loss streak (default: 1)
- **`mp_consecutive_loss_max`** — máximo de derrotas consecutivas antes do cap (default: 4)

Nenhum desses ConVars é definido pelo plugin em nenhum momento do fluxo.

## Detailed Findings

### 1. Fluxo Atual: Knife Round → Live Match

#### StartKnifeRound (`MatchService.cs:356-386`)

O knife round configura a economia para zero:
```
mp_startmoney 0          (linha 373)
mp_maxmoney 0            (linha 374)
mp_afterroundmoney 0     (linha 375)
```

Durante o knife round, não há economy management — tudo é zero. O knife round é jogado com apenas a faca.

#### EndKnifeRound (`MatchService.cs:388-393`)

Ao terminar o knife round:
```csharp
public void EndKnifeRound(int winnerTeam)
{
    _knifeRoundWinnerTeam = winnerTeam;   // linha 390
    _waitingForSideChoice = true;          // linha 391
    _isKnifeRound = false;                 // linha 392
}
```
Nenhum ConVar de economia é executado aqui.

#### ChooseSide (`MatchService.cs:395-428`)

Após o vote de side choice, a transição para o match ao vivo acontece:
```
mp_free_armor 0                          (linha 403)
mp_give_player_c4 1                      (linha 404)
mp_ct_default_secondary "weapon_hkp2000" (linha 405)
mp_t_default_secondary "weapon_glock"    (linha 406)
mp_buytime 20                            (linha 407)
mp_buy_during_immunity_time 1            (linha 408)
mp_startmoney 800                        (linha 409)
mp_maxmoney 16000                        (linha 410)
mp_afterroundmoney 0                     (linha 411)
mp_death_drop_gun 1                      (linha 412)
mp_swapteams                             (linha 417, se switch)
mp_unpause_match                         (linha 421)
mp_restartgame 3                         (linha 422)
```

**ConVars de loss bonus NÃO definidos em nenhum lugar:**
- `mp_starting_losses` — NÃO definido
- `mp_consecutive_loss_aversion` — NÃO definido
- `mp_consecutive_loss_max` — NÃO definido
- `mp_consecutive_loss_forgiveness` — NÃO existe no CS2 (o nome correto é `mp_consecutive_loss_aversion`)

### 2. O que `mp_restartgame` faz com o loss bonus

O `mp_restartgame 3` (executado em `ChooseSide()` na linha 422) faz um hard reset do match:
- Reseta o dinheiro de todos os jogadores para `mp_startmoney`
- Reseta o placar para 0-0
- Reseta as estatísticas de round

No entanto, **o comportamento exato em relação ao loss streak counter interno do engine é ambíguo**. O CS2 engine normalmente reseta tudo no `mp_restartgame`, mas não há documentação oficial explícita confirmando que o loss bonus counter é resetado.

### 3. ConVars do CS2 para Controle de Loss Bonus

#### `mp_starting_losses` (default: 0)
- Define o valor inicial do loss streak counter no início de cada half
- No matchmaking competitivo, é definido como `1` (por isso o primeiro round perdido dá $1900 e não $1400)
- Pode ser usado para garantir que o loss streak comece em um valor específico após o knife round

#### `mp_consecutive_loss_aversion` (default: 1)
- Controla como vitórias afetam o loss streak:
  - `0` = vitória reseta completamente o loss bonus para zero
  - `1` = vitória reduz o loss streak em 1 nível (comportamento padrão)
  - `2` = primeira vitória mantém o loss streak, redução começa na segunda vitória

#### `mp_consecutive_loss_max` (default: 4)
- Número máximo de derrotas consecutivas antes do cap ($3400)

#### Progressão do Loss Bonus no CS2 Competitivo
- 1ª derrota: $1400 (base: `cash_team_loser_bonus`, default 1400)
- 2ª derrota consecutiva: $1900 (+$500 via `cash_team_loser_bonus_consecutive_rounds`)
- 3ª derrota consecutiva: $2400
- 4ª derrota consecutiva: $2900
- 5ª+ derrota consecutiva: $3400 (cap em `mp_consecutive_loss_max` = 4)

### 4. O que o Plugin NÃO Define

Fazendo uma busca completa no `MatchService.cs` e em todo o codebase, os seguintes ConVars de loss bonus **nunca são executados** em nenhum lugar do plugin:

| ConVar | Definido pelo Plugin? | Default CS2 |
|--------|----------------------|-------------|
| `mp_starting_losses` | **NÃO** | 0 |
| `mp_consecutive_loss_aversion` | **NÃO** | 1 |
| `mp_consecutive_loss_max` | **NÃO** | 4 |
| `cash_team_loser_bonus` | **NÃO** | 1400 |
| `cash_team_loser_bonus_consecutive_rounds` | **NÃO** | 500 |

### 5. Comparação com MatchZy

O MatchZy (plugin competitivo popular para CS2) utiliza arquivos de configuração separados para cada fase:
- `warmup.cfg` — configurações de warmup
- `knife.cfg` — configurações do knife round
- `live.cfg` — configurações do match ao vivo

Isso permite controle granular sobre quais ConVars são aplicados em cada transição de fase, incluindo loss bonus settings.

## Code References

- `Services/MatchService.cs:356-386` — StartKnifeRound() configura economia zero
- `Services/MatchService.cs:373-375` — mp_startmoney/mp_maxmoney/mp_afterroundmoney = 0
- `Services/MatchService.cs:388-393` — EndKnifeRound() apenas atualiza estado
- `Services/MatchService.cs:395-428` — ChooseSide() restaura economia competitiva
- `Services/MatchService.cs:409-411` — mp_startmoney 800, mp_maxmoney 16000, mp_afterroundmoney 0
- `Services/MatchService.cs:422` — mp_restartgame 3 (reset do match)
- `Services/MatchService.cs:100-123` — EndWarmup() para comparação
- `Services/MatchService.cs:125-136` — StartMatch() para comparação

## Architecture Documentation

### Estado atual do tratamento de economy

O plugin segue um padrão "configurar e delegar" para a economia:
1. Define ConVars base (`mp_startmoney`, `mp_maxmoney`, `mp_afterroundmoney`)
2. Usa `mp_restartgame` para aplicar um reset
3. Deixa o engine do CS2 gerenciar a economia dinâmica (loss bonus, win bonus, kill rewards)

O plugin **não define** ConVars de loss bonus em nenhuma transição de estado (warmup→knife, knife→live, warmup→live).

### Transição knife→live: ConVars executados

```
ChooseSide() [MatchService.cs:395-428]
├── mp_free_armor 0
├── mp_give_player_c4 1
├── mp_ct_default_secondary "weapon_hkp2000"
├── mp_t_default_secondary "weapon_glock"
├── mp_buytime 20
├── mp_buy_during_immunity_time 1
├── mp_startmoney 800
├── mp_maxmoney 16000
├── mp_afterroundmoney 0
├── mp_death_drop_gun 1
├── [mp_swapteams]  (se switch)
├── mp_unpause_match
└── mp_restartgame 3
```

Nenhum ConVar de loss bonus (`mp_starting_losses`, `mp_consecutive_loss_aversion`, `mp_consecutive_loss_max`) é definido nesta transição.

## Historical Context (from thoughts/)

- `thoughts/shared/research/2026-02-09-cs-default-economy-outside-warmup.md` — Documenta que o plugin já segue a economia padrão do CS2 competitivo, com `mp_afterroundmoney = 0` delegando ao engine
- `thoughts/shared/research/2026-02-09-start-command-knife-round-warmup-flow.md` — Documenta o fluxo completo do !start com knife round
- `thoughts/shared/plans/2026-02-09-fix-knife-round-warmup-not-ending.md` — Plano que corrigiu o knife round não encerrando o warmup (adicionou `mp_warmup_end`)

## Related Research

- `thoughts/shared/research/2026-02-09-cs-default-economy-outside-warmup.md` — Economia padrão do CS2
- `thoughts/shared/research/2026-02-09-start-command-knife-round-warmup-flow.md` — Fluxo do knife round

## Open Questions

1. O `mp_restartgame 3` reseta completamente o loss streak counter interno do CS2 engine? É o comportamento observado em jogo?
2. É necessário definir `mp_starting_losses` explicitamente na transição knife→live para garantir que o loss bonus comece no valor competitivo correto (0 ou 1)?
3. O knife round, por ser um round jogado antes do `mp_restartgame`, pode deixar um loss streak residual que persiste após o restart?

## Sources (Web Research)

- [CS2 Loss Bonus Guide - Skin.land](https://skin.land/blog/cs2-loss-bonus/)
- [CS2 Economy Guide - TradeIt](https://tradeit.gg/blog/cs2-economy-guide/)
- [mp_consecutive_loss_max - Total CS](https://totalcsgo.com/commands/mpconsecutivelossmax)
- [mp_starting_losses - Total CS](https://totalcsgo.com/commands/mpstartinglosses)
- [mp_economy_reset_rounds - Total CS](https://totalcsgo.com/commands/mpeconomyresetrounds)
- [MatchZy Plugin - GitHub](https://github.com/shobhit-pathak/MatchZy)
- [MatchZy Documentation](https://shobhit-pathak.github.io/MatchZy/)
- [CS2 CVars List - GitHub (dgibbs64)](https://gist.github.com/dgibbs64/6c321d87004c96919fccd6163ca29d90)
- [CS2 Console Commands - Valve Developer](https://developer.valvesoftware.com/wiki/List_of_Counter-Strike_2_console_commands_and_variables)
