---
date: 2026-02-09T00:00:00-03:00
researcher: franciscpd
git_commit: 9ec57cf37a308b78bb7a6b4cf57b1fd79e76e661
branch: main
repository: cs2admin
topic: "Renomear !admin para !command e bloquear movimento/menus do jogo quando menu admin estiver aberto"
tags: [research, codebase, chat-commands, menu-system, input-blocking, wasd-menu]
status: complete
last_updated: 2026-02-09
last_updated_by: franciscpd
---

# Research: Renomear !admin para !command e bloquear movimento/menus do jogo com menu aberto

**Date**: 2026-02-09
**Researcher**: franciscpd
**Git Commit**: 9ec57cf37a308b78bb7a6b4cf57b1fd79e76e661
**Branch**: main
**Repository**: cs2admin

## Research Question

Quero mudar o atalho `!admin` para `!command`, e quando o menu estiver aberto quero que evite de ficar andando e abrindo menus do game.

## Summary

### Sobre o comando `!admin`

**Atualmente nao existe um comando `!admin` no codebase.** O ponto de entrada principal para os menus administrativos e o comando `!help`, definido em `Commands/ChatCommandHandler.cs:87`. O `!help` abre um menu WASD interativo que mostra:
- "Vote Commands" (para todos)
- "Admin Commands" (para admins com permissoes)
- "Root Admin Commands" (para admins com `@css/root`)

### Sobre o sistema de menus

O plugin usa a biblioteca **CS2MenuManager** (v1.0.30) para exibir menus WASD interativos. A navegacao e feita com as teclas **W/S/E/A/R** (cima/baixo/selecionar/voltar/sair).

### Sobre bloqueio de movimento quando menu esta aberto

**Atualmente NAO existe nenhum mecanismo para bloquear o movimento do jogador ou impedir a abertura de menus do jogo quando um menu WASD esta aberto.** O jogador pode andar livremente e abrir o buy menu, scoreboard, etc., enquanto navega o menu admin.

O codebase nao possui:
- Manipulacao de `MoveType` ou `FL_FROZEN`
- Hooks de `OnPlayerRunCmd` ou `ProcessMovement`
- Bloqueio de input do jogador
- Prevencao de abertura de menus do jogo (buy menu, scoreboard, etc.)

O unico bloqueio de comando existente e para compras durante o modo faca (`CS2Admin.cs:41-43, 232-242`).

## Detailed Findings

### 1. Sistema de Roteamento de Comandos de Chat

**Arquivo**: `Commands/ChatCommandHandler.cs:63-126`

O metodo `OnPlayerChat()` e o ponto central de roteamento de todos os comandos `!`. Ele:
1. Recebe a mensagem do chat (linha 67)
2. Verifica se comeca com `!` (linha 68)
3. Faz parse do comando e argumentos (linhas 78-82)
4. Roteia via switch statement (linhas 84-123)

Os comandos registrados atualmente sao:
- `help` (linha 87) - Menu principal
- `votekick`, `votepause`, `voterestart`, `votechangemap`, `votemap` (linhas 90-93)
- `kick`, `ban`, `unban`, `mute`, `unmute`, `slay`, `slap`, `respawn` (linhas 96-103)
- `changemap`, `map`, `pause`, `unpause`, `restart`, `start`, `warmup`, `endwarmup` (linhas 104-110)
- `knife`, `teams` (linhas 111-112)
- `add_admin`, `remove_admin`, `list_admins`, `list_groups`, `set_group`, `reload_admins` (linhas 115-120)

**NAO existe** um comando `"admin"` no switch statement. Se o usuario digitar `!admin`, ele cai no `_ => false` (linha 122) e nao faz nada.

### 2. Menu Principal (HandleHelp)

**Arquivo**: `Commands/ChatCommandHandler.cs:888-914`

O `HandleHelp()` cria um `WasdMenu("CS2Admin Help", _plugin)` com os seguintes itens:
- "Vote Commands" - sempre visivel (linha 892)
- "Admin Commands" - visivel se o jogador tem qualquer permissao admin (linhas 894-904)
- "Root Admin Commands" - visivel se o jogador tem `@css/root` (linhas 907-909)

O menu e exibido com `menu.Display(player, 30)` - timeout de 30 segundos.

### 3. Criacao de Menus WASD

**Dependencia**: `CS2MenuManager` v1.0.30 (referenciado em `CS2Admin.csproj:12`)

**Imports**: `Commands/ChatCommandHandler.cs:9-11`
```csharp
using CS2MenuManager.API.Class;
using CS2MenuManager.API.Enum;
using CS2MenuManager.API.Menu;
```

O padrao de criacao de menus e consistente em todo o codebase:
1. `var menu = new WasdMenu(title, _plugin);`
2. `menu.AddItem(label, (player, option) => callback);`
3. `menu.Display(player, timeoutSeconds);`

Todos os menus usam `WasdMenu` como classe. Os menus aparecem em 23+ locais no `ChatCommandHandler.cs`.

### 4. Registro de Comandos de Chat no Plugin Principal

**Arquivo**: `CS2Admin.cs:37-38`

```csharp
AddCommandListener("say", OnPlayerSay);
AddCommandListener("say_team", OnPlayerSay);
```

O `OnPlayerSay` (linha 226-230) simplesmente delega para `_services.ChatCommandHandler.OnPlayerChat(player, info)`.

### 5. Mecanismos de Bloqueio Existentes

O unico bloqueio de comando no codebase e para compras durante modo faca:

**Arquivo**: `CS2Admin.cs:40-43`
```csharp
AddCommandListener("buy", OnBuyCommand, HookMode.Pre);
AddCommandListener("autobuy", OnBuyCommand, HookMode.Pre);
AddCommandListener("rebuy", OnBuyCommand, HookMode.Pre);
```

**Arquivo**: `CS2Admin.cs:232-242`
```csharp
private HookResult OnBuyCommand(CCSPlayerController? player, CommandInfo command)
{
    if (_services.MatchService.IsKnifeOnly && player != null && player.IsValid)
    {
        player.PrintToChat($"{Config.ChatPrefix} Purchases are disabled in knife mode!");
        return HookResult.Handled;
    }
    return HookResult.Continue;
}
```

### 6. Navegacao dos Menus (CS2MenuManager)

Conforme documentado em `README.md:148-159`:

| Tecla | Acao |
|-------|------|
| W | Move up |
| S | Move down |
| E | Select option |
| A | Go back |
| R | Exit menu |

**Conflitos com o jogo**: As teclas W e S sao usadas tanto para navegacao do menu WASD quanto para movimento do jogador no jogo. Quando o menu esta aberto, o jogador se move para frente/tras enquanto navega. Alem disso, teclas de menus do jogo (buy menu com B, scoreboard com Tab, etc.) continuam funcionando.

### 7. Locais onde `menu.Display()` e Chamado

Todos no `ChatCommandHandler.cs`:
- Linha 154: Player selection (timeout 30s)
- Linha 258: Map selection (timeout 30s)
- Linha 355: Ban duration (timeout 30s)
- Linha 454: Mute duration (timeout 30s)
- Linha 584: Slap damage (timeout 30s)
- Linha 850: Team selection (timeout 60s)
- Linha 912: Help menu (timeout 30s)
- Linha 926: Vote commands (timeout 30s)
- Linha 958: Admin commands (timeout 30s)
- Linha 974: Root admin commands (timeout 30s)
- Linha 995: Add admin player select (timeout 30s)
- Linha 1009: Flag selection (timeout 30s)
- Linha 1058: Remove admin (timeout 30s)
- Linha 1085: Edit admin (timeout 30s)
- Linha 1099: Admin permissions (timeout 30s)
- Linha 1117: Toggle flags (timeout 30s)
- Linha 1157: Flag selection for edit (timeout 30s)
- Linha 1195: Set group admin select (timeout 30s)
- Linha 1226: Group selection (timeout 30s)

E em `VoteCommands.cs`:
- Linha 92: Vote kick player selection (timeout 30s)
- Linha 172: Vote map selection (timeout 30s)

## Code References

- `Commands/ChatCommandHandler.cs:63-126` - Roteamento de comandos de chat (switch statement)
- `Commands/ChatCommandHandler.cs:84-87` - Onde `"help"` esta registrado como comando
- `Commands/ChatCommandHandler.cs:888-914` - `HandleHelp()` - menu principal
- `Commands/ChatCommandHandler.cs:9-11` - Imports do CS2MenuManager
- `Commands/ChatCommandHandler.cs:147` - Exemplo de criacao de WasdMenu
- `CS2Admin.cs:37-38` - Registro dos listeners de chat (`say`, `say_team`)
- `CS2Admin.cs:40-43` - Registro dos listeners de buy (unico bloqueio de comando)
- `CS2Admin.cs:226-230` - `OnPlayerSay()` - delega para ChatCommandHandler
- `CS2Admin.cs:232-242` - `OnBuyCommand()` - bloqueio de compras no modo faca
- `CS2Admin.csproj:12` - Dependencia CS2MenuManager v1.0.30
- `README.md:148-159` - Documentacao dos controles WASD do menu

## Architecture Documentation

### Fluxo de Comando de Chat
```
Player digita "!help" no chat
  → CS2Admin.OnPlayerSay() [CS2Admin.cs:226]
    → ChatCommandHandler.OnPlayerChat() [ChatCommandHandler.cs:63]
      → Parse: command="help", args=[]
        → Switch: "help" → HandleHelp(player) [ChatCommandHandler.cs:87]
          → new WasdMenu("CS2Admin Help") [ChatCommandHandler.cs:890]
          → menu.AddItem("Vote Commands", ...) [ChatCommandHandler.cs:892]
          → menu.AddItem("Admin Commands", ...) [ChatCommandHandler.cs:904] (se admin)
          → menu.AddItem("Root Admin Commands", ...) [ChatCommandHandler.cs:909] (se root)
          → menu.Display(player, 30) [ChatCommandHandler.cs:912]
      → return HookResult.Handled
```

### Padrao de Criacao de Menu
Todos os menus seguem o mesmo padrao:
1. Criar `WasdMenu` com titulo e referencia do plugin
2. Adicionar itens com `AddItem(label, callback)` ou `AddItem(label, DisableOption.DisableShowNumber)` para itens informativos
3. Opcionalmente adicionar "Back" com `AddItem("« Back", (p, o) => ParentMenu(p))`
4. Exibir com `Display(player, timeoutSeconds)`

### Limitacao: Sem Bloqueio de Input
O CS2MenuManager gerencia a exibicao e interacao dos menus, mas **nao bloqueia o movimento do jogador nem impede a abertura de menus nativos do CS2**. A biblioteca trata os inputs W/S/E/A/R para navegacao, mas esses mesmos inputs ainda controlam o jogador no jogo simultaneamente.

## Open Questions

1. **CS2MenuManager suporta bloqueio de movimento?** - Precisa investigar se a biblioteca CS2MenuManager v1.0.30 tem opcoes built-in para freezar o jogador ou bloquear inputs enquanto o menu esta aberto. A documentacao da biblioteca pode conter informacoes sobre `FreezePlayer` ou opcoes de menu que bloqueiam input.

2. **Como bloquear menus nativos do CS2?** - Para impedir que o buy menu (B), scoreboard (Tab), etc. abram enquanto o menu WASD esta ativo, seria necessario interceptar os comandos correspondentes (`buymenu`, `showscores`, etc.) via `AddCommandListener` e checar se o jogador tem um menu aberto.

3. **Tracking de estado do menu** - O CS2MenuManager pode ter APIs para verificar se um jogador tem um menu aberto atualmente. Isso seria necessario para condicionar o bloqueio de movimento/menus do jogo.
