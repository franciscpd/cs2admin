---
date: 2026-02-09T19:12:29-03:00
researcher: franciscpd
git_commit: b082f180fd755ee2fbc9f0824b1296f9de017841
branch: main
repository: cs2admin
topic: "Opção de manter usuário congelado quando menu está aberto não funciona"
tags: [research, codebase, freeze-player, menu, wasd-menu, cs2menumanager, movetype]
status: complete
last_updated: 2026-02-09
last_updated_by: franciscpd
---

# Research: Freeze Player Durante Navegação de Menu Não Funciona

**Date**: 2026-02-09T19:12:29-03:00
**Researcher**: franciscpd
**Git Commit**: b082f180fd755ee2fbc9f0824b1296f9de017841
**Branch**: main
**Repository**: cs2admin

## Research Question

A opção de manter o usuário congelado quando o menu está aberto não funcionou.

## Summary

A implementação atual define `WasdMenu_FreezePlayer = true` em todos os 21 menus criados no codebase. Porém, a decompilação do CS2MenuManager v1.0.30 revela que o construtor do `WasdMenu` inicializa `WasdMenu_FreezePlayer` a partir do `ConfigManager.Config.WasdMenu.FreezePlayer`, cujo **default é `false`**. Em seguida, o object initializer `{ WasdMenu_FreezePlayer = true }` sobrescreve para `true`. Esse fluxo deveria funcionar.

A decompilação mostra que quando `WasdMenu_FreezePlayer = true`:
1. **Na abertura**: chama `Player.Freeze()` que executa `ChangeMoveType(MoveType_t.1)` (MOVETYPE_NONE)
2. **No fechamento**: chama `Player.Unfreeze()` que executa `ChangeMoveType(MoveType_t.2)` (MOVETYPE_WALK)

O `ChangeMoveType` define tanto `MoveType` quanto `m_nActualMoveType` e chama `SetStateChanged`.

O freeze é aplicado **uma única vez** na abertura do menu -- NÃO é reaplicado a cada tick. Se algo (ex: respawn, round restart, warmup respawn) resetar o MoveType do jogador depois da abertura do menu, o freeze será perdido.

## Detailed Findings

### 1. Implementação no CS2Admin - Todos os Menus com Freeze

Todos os 21 menus no codebase usam o padrão:
```csharp
var menu = new WasdMenu(title, _plugin) { WasdMenu_FreezePlayer = true };
```

**Locais em `Commands/ChatCommandHandler.cs`:**
- Linha 147: ShowPlayerSelectionMenu()
- Linha 243: ShowMapSelectionMenu()
- Linha 347: ShowBanDurationMenu()
- Linha 446: ShowMuteDurationMenu()
- Linha 576: ShowSlapDamageMenu()
- Linha 828: ShowTeamSelectionMenu()
- Linha 890: HandleHelp() (menu principal `!command`)
- Linha 918: ShowVoteCommands()
- Linha 931: ShowAdminCommands()
- Linha 963: ShowRootAdminCommands()
- Linha 987: ShowAddAdminMenu()
- Linha 1000: ShowFlagSelectionMenu()
- Linha 1035: ShowRemoveAdminMenu()
- Linha 1071: ShowEditAdminMenu()
- Linha 1090: ShowAdminPermissionsMenu()
- Linha 1106: ShowAddFlagsMenu()
- Linha 1148: ShowFlagSelectionMenuForEdit()
- Linha 1182: ShowSetGroupMenu()
- Linha 1208: ShowGroupSelectionForAdmin()

**Locais em `Commands/VoteCommands.cs`:**
- Linha 72: ShowPlayerSelectionMenu()
- Linha 158: ShowMapSelectionMenu()

### 2. Decompilação do CS2MenuManager v1.0.30 - WasdMenuInstance

#### Construtor (abertura do menu):
```csharp
// Quando o menu é instanciado para um jogador:
if (wasdMenu.WasdMenu_FreezePlayer)
{
    base.Player.Freeze();  // Chamado UMA VEZ na abertura
}
```

#### Close (fechamento do menu):
```csharp
// Quando o menu é fechado (seleção, exit, timeout):
if (((WasdMenu)base.Menu).WasdMenu_FreezePlayer)
{
    base.Player.Unfreeze();
}
```

#### OnTick (atualização a cada tick):
O método `OnTick()` do WasdMenuInstance NÃO reaplica o freeze. Ele apenas:
- Lê os botões pressionados (`base.Player.Buttons`)
- Verifica key presses para navegação (W/S/E/A/R)
- Atualiza a exibição do menu na tela

### 3. Implementação de Freeze/Unfreeze

```csharp
public static void Freeze(this CCSPlayerController player)
{
    player.PlayerPawn.Value?.ChangeMoveType(MoveType_t.1);  // MOVETYPE_NONE
}

public static void Unfreeze(this CCSPlayerController player)
{
    player.PlayerPawn.Value?.ChangeMoveType(MoveType_t.2);  // MOVETYPE_WALK
}

public static void ChangeMoveType(this CBasePlayerPawn pawn, MoveType_t movetype)
{
    if (pawn.Handle != IntPtr.Zero)
    {
        pawn.MoveType = movetype;
        Schema.SetSchemaValue<MoveType_t>(pawn.Handle, "CBaseEntity", "m_nActualMoveType", movetype);
        Utilities.SetStateChanged(pawn, "CBaseEntity", "m_MoveType", 0);
    }
}
```

O freeze funciona definindo:
- `CBaseEntity.MoveType` = 1 (MOVETYPE_NONE)
- `CBaseEntity.m_nActualMoveType` = 1 (MOVETYPE_NONE)
- Chama `SetStateChanged` para sincronizar com o cliente

### 4. Config Default do CS2MenuManager

O construtor do WasdMenu carrega o valor default de `ConfigManager.Config.WasdMenu.FreezePlayer`:

```csharp
// Default da config:
WasdMenu = new WasdMenu
{
    // ...
    FreezePlayer = false  // DEFAULT É FALSE
};
```

Porém isso é **sobrescrito** pelo object initializer `{ WasdMenu_FreezePlayer = true }` no CS2Admin.

A config pode ser alterada via `config.toml` do CS2MenuManager em:
```
addons/counterstrikesharp/shared/CS2MenuManager/config.toml
```

Com a chave `WasdMenu.FreezePlayer = true/false`.

### 5. Fluxo Completo Documentado

```
Jogador digita !command
  → CS2Admin.OnPlayerSay() [CS2Admin.cs:226]
    → ChatCommandHandler.OnPlayerChat() [ChatCommandHandler.cs:63]
      → HandleHelp(player) [ChatCommandHandler.cs:87]
        → new WasdMenu("CS2Admin Help", _plugin) { WasdMenu_FreezePlayer = true }
          → Construtor WasdMenu: FreezePlayer = config default (false)
          → Object initializer: WasdMenu_FreezePlayer = true (sobrescreve)
        → menu.Display(player, 30)
          → CS2MenuManager cria WasdMenuInstance
            → Verifica WasdMenu_FreezePlayer == true
            → Chama Player.Freeze() → ChangeMoveType(MOVETYPE_NONE)
            → Registra key bindings (W/S/E/A/R)
          → Menu exibido na tela

[Jogador navega o menu com W/S/E/A/R]
  → OnTick() processa inputs
  → NÃO reaplica freeze

[Menu fecha (timeout 30s, seleção com E, exit com R)]
  → Close()
    → Verifica WasdMenu_FreezePlayer == true
    → Chama Player.Unfreeze() → ChangeMoveType(MOVETYPE_WALK)
```

### 6. Cenários que Podem Resetar o MoveType

O freeze via MoveType pode ser perdido se, durante a navegação do menu:
- O jogador respawna (warmup tem `mp_respawn_on_death_ct/t 1`)
- Um `mp_restartgame` é executado
- O round reinicia
- Outro plugin ou o engine modifica o MoveType do jogador

O CS2MenuManager NÃO reaplica o freeze a cada tick, então qualquer reset externo do MoveType desfaz o freeze silenciosamente.

## Code References

- `Commands/ChatCommandHandler.cs:147` - Primeiro exemplo de menu com freeze
- `Commands/ChatCommandHandler.cs:890` - Menu principal (!command) com freeze
- `Commands/VoteCommands.cs:72` - Menu de vote com freeze
- `CS2Admin.csproj:12` - Dependência CS2MenuManager v1.0.30
- CS2MenuManager DLL (decompilada): `~/.nuget/packages/cs2menumanager/1.0.30/lib/net8.0/CS2MenuManager.dll`
  - WasdMenuInstance constructor: `Player.Freeze()` na abertura
  - WasdMenuInstance.Close(): `Player.Unfreeze()` no fechamento
  - WasdMenuInstance.OnTick(): NÃO reaplica freeze
  - Extension method `Freeze()`: `ChangeMoveType(MoveType_t.1)`
  - Extension method `Unfreeze()`: `ChangeMoveType(MoveType_t.2)`
  - Config default: `WasdMenu.FreezePlayer = false`

## Architecture Documentation

### Cadeia de Dependências
```
CS2Admin → CS2MenuManager v1.0.30 → CounterStrikeSharp API
                |                           |
                |                           ├─ CCSPlayerController
                |                           ├─ CBasePlayerPawn.MoveType
                |                           └─ Schema.SetSchemaValue
                |
                ├─ WasdMenu (classe de definição do menu)
                │   └─ WasdMenu_FreezePlayer = true/false
                │
                └─ WasdMenuInstance (instância ativa para jogador)
                    ├─ Constructor → Player.Freeze()
                    ├─ OnTick() → processa inputs (SEM refreeze)
                    └─ Close() → Player.Unfreeze()
```

### Mecanismo de Freeze
```
Freeze: MoveType = MOVETYPE_NONE (1)
        m_nActualMoveType = MOVETYPE_NONE (1)
        SetStateChanged("CBaseEntity", "m_MoveType")

Unfreeze: MoveType = MOVETYPE_WALK (2)
          m_nActualMoveType = MOVETYPE_WALK (2)
          SetStateChanged("CBaseEntity", "m_MoveType")
```

## Historical Context (from thoughts/)

- `thoughts/shared/research/2026-02-09-admin-command-rename-and-menu-input-blocking.md` - Pesquisa original que identificou a ausência de mecanismo de freeze (antes da implementação)
- `thoughts/shared/plans/2026-02-09-rename-help-to-command-and-freeze-on-menu.md` - Plano de implementação do freeze

## Open Questions

1. O `Player.Freeze()` via MoveType funciona corretamente durante warmup quando respawn está habilitado (`mp_respawn_on_death_ct/t 1`)?
2. O CS2 engine reseta o MoveType do jogador em eventos como respawn ou round_start?
3. A versão mais recente do CS2MenuManager (1.0.42) mudou o comportamento do freeze para reaplica-lo a cada tick?
4. Existe algum conflito entre o `MoveType = MOVETYPE_NONE` e as teclas W/S que o CS2MenuManager usa para navegação? (O MoveType bloqueia o movimento físico mas os key events ainda são detectados?)
