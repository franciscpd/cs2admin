---
date: 2026-02-09T19:52:21-03:00
researcher: franciscpd
git_commit: e6a143bfa37cff736c8465d12fc63b3a79f97e46
branch: main
repository: cs2admin
topic: "Multi-language support - Current string architecture and CounterStrikeSharp i18n capabilities"
tags: [research, codebase, i18n, localization, multi-language, translations, localizer]
status: complete
last_updated: 2026-02-09
last_updated_by: franciscpd
---

# Research: Suporte Multi-Idioma - Arquitetura Atual de Strings e Capacidades de i18n do CounterStrikeSharp

**Date**: 2026-02-09T19:52:21-03:00
**Researcher**: franciscpd
**Git Commit**: e6a143bfa37cff736c8465d12fc63b3a79f97e46
**Branch**: main
**Repository**: cs2admin

## Research Question

Adicionar suporte multi idioma - Mapear todas as strings do usuário no codebase e documentar as capacidades de i18n do CounterStrikeSharp para planejar a implementação de suporte a múltiplos idiomas.

## Summary

O CS2Admin possui atualmente **~250+ strings hardcoded** espalhadas por 8 arquivos de código, e apenas **10 mensagens configuráveis** via `PluginConfig.cs`. Todas as strings são em inglês e não utilizam nenhum sistema de localização.

O CounterStrikeSharp oferece um **sistema de localização nativo** baseado em `Microsoft.Extensions.Localization` com suporte a:
- Propriedade `Localizer` disponível em todo plugin (tipo `IStringLocalizer`)
- Arquivos JSON de tradução em pasta `lang/` (ex: `lang/en.json`, `lang/pt.json`)
- Método `Localizer.ForPlayer(player, "key")` para mensagens por jogador baseado no idioma configurado
- Detecção de idioma do jogador via `player.GetLanguage()` (retorna `CultureInfo`)
- Comandos `!lang <code>` / `css_lang <code>` para jogadores configurarem idioma

## Detailed Findings

### 1. Strings Atuais por Arquivo

#### PluginConfig.cs - 10 Mensagens Configuráveis
Mensagens definidas como propriedades públicas com valores default:

| Propriedade | Default | Placeholders |
|-------------|---------|-------------|
| `ChatPrefix` (line 46) | `"[CS2Admin]"` | Nenhum |
| `DefaultBanReason` (line 37) | `"Banned by admin"` | Nenhum |
| `DefaultKickReason` (line 40) | `"Kicked by admin"` | Nenhum |
| `DefaultMuteReason` (line 43) | `"Muted by admin"` | Nenhum |
| `WelcomeMessage` (line 55) | `"Welcome to the server, {player}!"` | `{player}`, `{steamid}` |
| `PlayerJoinMessage` (line 64) | `"{player} joined the server."` | `{player}`, `{steamid}` |
| `WarmupMessage` (line 73) | `"Server is in warmup. Waiting for admin to start the match."` | Nenhum |
| `MatchStartMessage` (line 76) | `"Match starting! Good luck, have fun!"` | Nenhum |
| `KnifeRoundMessage` (line 85) | `"Knife round! Winner chooses side."` | Nenhum |
| `KnifeRoundWinnerMessage` (line 88) | `"{team} won the knife round! Type !stay or !switch (or vote F1/F2)"` | `{team}` |

#### ChatCommandHandler.cs - ~150+ Strings Hardcoded
O arquivo mais pesado em strings. Categorias:

**Mensagens de Permissão (17 ocorrencias):**
- `"You don't have permission to use this command."` - Linhas 273, 306, 376, 408, 473, 517, 547, 601, 631, 651, 670, 689, 702, 738, 759, 784, 807

**Mensagens de Erro de Player (11 ocorrencias):**
- `"Player not found."` - Linhas 183, 291, 324, 334, 424, 497, 534, 563, 618
- `"No players available."` - Linha 147
- `"You cannot vote to kick yourself."` - Linha 189

**Mensagens de Estado de Match:**
- `"Match is already paused."` - Linha 657
- `"Match is not paused."` - Linha 676
- `"Server is not in warmup mode."` - Linhas 708, 764
- `"Server is already in warmup mode."` - Linha 745
- `"Not enough players. Need at least {N} players."` - Linhas 715, 772
- `"No side choice is pending."` - Linha 836
- `"Only the winning team can choose sides."` - Linha 842

**Mensagens de Ação Admin (broadcasts a todos jogadores):**
- `"{player} was kicked by {admin}. Reason: {reason}"` - Linhas 283, 298
- `"{player} was banned by {admin} for {duration}. Reason: {reason}"` - Linhas 345, 369
- `"{player} was muted by {admin} for {duration}."` - Linhas 444, 467
- `"{player} was unmuted by {admin}."` - Linhas 484, 503
- `"{player} was slayed by {admin}."` - Linhas 526, 539
- `"{player} was slapped for {damage} damage."` - Linhas 574, 594
- `"{player} was respawned."` - Linhas 610, 623
- `"Changing map to {map}..."` - Linha 642
- `"Match paused by {player}."` - Linha 662
- `"Match unpaused by {player}."` - Linha 681
- `"Match restarting by {player}..."` - Linha 693
- `"Match started by {player}."` - Linhas 722, 728
- `"Warmup started by {player}."` - Linha 749
- `"Warmup ended by {player}."` - Linha 776
- `"Knife only mode enabled/disabled by {player}."` - Linhas 792, 797
- `"{team} chose to STAY/SWITCH! Match starting!"` - Linha 854

**Menus - Títulos (~25 títulos):**
- `"CS2Admin Help"`, `"Vote Commands"`, `"Admin Commands"`, `"Root Admin Commands"`
- `"Kick - Select Player"`, `"Ban {name} - Duration"`, `"Mute {name} - Duration"`
- `"Vote Kick - Select Player"`, `"Vote Map - Select Map"`
- `"Slap {name} - Damage"`, `"Respawn - Select Player"`, `"Slay - Select Player"`
- `"Select players for Team 1 (T)"`
- `"Add Admin - Select Player"`, `"Flags for {name}"`, `"Remove Admin - Select"`
- `"Edit Admin - Select"`, `"Permissions: {name}"`, `"Toggle Flags: {name}"`
- `"Set Role: {name}"`, `"Set Group - Select Admin"`, `"Group for {name}"`

**Menus - Itens (~50+ itens):**
- Durações: `"30 minutes"`, `"1 hour"`, `"1 day"`, `"1 week"`, `"Permanent"`
- Danos: `"No damage"`, `"5 damage"`, `"10 damage"`, `"25 damage"`, `"50 damage"`
- Navegação: `"« Back"`, `"Cancel"`, `"✓ Confirm Teams"`
- Ações: `"Vote Kick Player"`, `"Vote Pause Match"`, `"Vote Restart Match"`, `"Vote Change Map"`
- Roles: `"Full Admin (@css/root)"`, `"Moderator (kick,ban,chat)"`, `"Basic Admin (kick,slay)"`, `"Match Admin (generic,map)"`, `"VIP"`
- Info: `"Current: {flags}"`, `"[X] {flag}"`, `"[ ] {flag}"`

**Mensagens de Admin Management:**
- `"Admin added: {name}"`, `"Admin already exists."`, `"No admins found."`
- `"Admin removed: {name}"`, `"Failed to remove admin."`, `"Failed to set group."`
- `"{name} assigned to group: {group}"`, `"No groups found. Create a group first."`
- `"Added/Removed {flag} from {name}"`, `"Updated {name}'s permissions to: {flags}"`

#### AdminCommands.cs - ~40 Strings Hardcoded
Console commands (css_*) com mensagens duplicadas das do ChatCommandHandler:
- Usage messages: `"Usage: css_kick <player> [reason]"`, `"Usage: css_ban <player> <duration> [reason]"`, etc.
- Mesmo padrão de erro/sucesso/broadcast

#### VoteCommands.cs - ~15 Strings Hardcoded
- `"This command can only be used by players."` (4 ocorrencias)
- `"No players available to kick."`, `"No maps configured for voting."`
- `"You cannot vote to kick yourself."`

#### AdminManagementCommands.cs - ~45 Strings Hardcoded
Console + chat admin management commands:
- `"Usage: css_add_admin <steamid|player> <flags>"`, `"Flags: @css/kick,@css/ban,..."`
- `"Invalid flag: {flag}"`, `"Invalid player or SteamID."`
- `"Admins ({count}):"`, `"Groups ({count}):"`
- `"Admins reloaded from database."`, `"Admins reloaded."`

#### VoteService.cs - ~10 Strings Hardcoded
- `"A vote is already in progress."`, `"Not enough players to start a vote."`
- `"Vote on cooldown. Please wait {N} seconds."`, `"Failed to start vote."`
- `"Vote started: {desc}"`, `"Vote passed: {desc} ({yes}/{total}, {pct}%)"`
- `"Vote failed: {desc} ({yes}/{total}, {pct}%)"`, `"Vote cancelled."`
- `"Kick {player}"`, `"Pause match"`, `"Restart match"`, `"Change map to {map}"`
- `"{team} choose side: F1=Stay, F2=Switch"`, `"{team} chose to STAY/SWITCH. Match starting!"`

#### MatchService.cs - 3 Strings Hardcoded
- `"TACTICAL PAUSE"`, `"DISCONNECT PAUSE"`, `"PAUSED"` - PrintToCenter (linha 249-253)
- `"Disconnected player reconnected. Match resumed."` - (linha 214)
- `"Match resumed."` - (linha 278)

#### PlayerConnectionHandler.cs - 2 Strings Hardcoded
- `"You are banned {duration}. Reason: {reason}"` - Kick message (linha 49)
- `"{player} disconnected. Match paused for {N} minutes."` - (linha 125)

#### CS2Admin.cs - 1 String Hardcoded
- `"Purchases are disabled in knife mode!"` - (linha 245)

#### PanoramaVote.cs - 7 Strings (Console-only, debug)
- `"[Vote Error] A vote is already in progress."` (linha 246)
- `"[Vote Start] Starting a new vote..."` (linha 259)
- `"[Vote Ending]..."` (linhas 389, 392, 395)
- `"[Vote Cancel] Vote has been cancelled."` (linha 496)
- `"No vote is currently in progress."` (linhas 493, 507) - PrintToChat para admin

### 2. Sistema de i18n do CounterStrikeSharp

O CounterStrikeSharp provê um sistema completo de localização:

#### API Principal: `Localizer`

Todo plugin herda uma propriedade `Localizer` (tipo `IStringLocalizer`):

```csharp
// Mensagem no idioma do servidor
Localizer["key.name"]

// Mensagem com argumentos formatados
Localizer["key.name", arg1, arg2]

// Mensagem no idioma do jogador específico
Localizer.ForPlayer(player, "key.name")
Localizer.ForPlayer(player, "key.name", arg1, arg2)
```

#### Arquivos de Tradução

Estrutura de pastas:
```
plugins/CS2Admin/
  ├── CS2Admin.dll
  └── lang/
      ├── en.json    (inglês)
      ├── pt.json    (português)
      ├── de.json    (alemão)
      ├── ru.json    (russo)
      └── es.json    (espanhol)
```

Formato JSON (flat key-value):
```json
{
  "error.no_permission": "You don't have permission to use this command.",
  "error.player_not_found": "Player not found.",
  "admin.kicked": "{0} was kicked by {1}. Reason: {2}",
  "vote.passed": "Vote passed: {0} ({1}/{2}, {3}%)"
}
```

#### Detecção de Idioma do Jogador

1. **Via comando**: Jogadores executam `!lang pt` ou `css_lang pt`
2. **Via API**: `player.GetLanguage()` retorna `CultureInfo`
3. **Fallback**: Se jogador não configurou, usa idioma do servidor
4. **Opção avançada**: Plugin [GeoLocationLanguageManagerPlugin](https://github.com/aprox2/GeoLocationLanguageManagerPlugin) detecta idioma por IP/geolocalização

#### Implementação Interna

- Fonte: [JsonStringLocalizerFactory.cs](https://github.com/roflmuffin/CounterStrikeSharp/blob/main/managed/CounterStrikeSharp.API/Core/Translations/JsonStringLocalizerFactory.cs)
- Busca arquivos em: `Path.Join(Path.GetDirectoryName(_pluginContext.FilePath), "lang")`
- Baseado em `Microsoft.Extensions.Localization`
- Suporta injeção de dependência: `IStringLocalizer` pode ser injetado em serviços

### 3. Padrão Atual de Mensagens Configuráveis

O fluxo atual de mensagens no CS2Admin:

```
PluginConfig.cs (definição com default)
    ↓
JSON config file (customização pelo usuário)
    ↓
CS2Admin.cs:OnConfigParsed() (parsing)
    ↓
CS2AdminServiceCollection (distribuição para services/handlers)
    ↓
Service/Handler constructor (armazenamento em _config)
    ↓
Event/Command handler (uso da mensagem)
    ↓
Variable substitution: .Replace("{player}", name) (se necessário)
    ↓
ChatPrefix prepending: $"{_config.ChatPrefix} {message}"
    ↓
PrintToChat / PrintToChatAll / ReplyToCommand (exibição)
```

**Distribuição do Config:**
- Handlers que recebem `PluginConfig` completo: `AdminCommands`, `VoteCommands`, `AdminManagementCommands`, `ChatCommandHandler`, `PlayerConnectionHandler`
- Services que recebem valores individuais: `MatchService` (recebe `_chatPrefix` como string), `VoteService` (recebe callback `_broadcastMessage`)

**Padrão de substituição de variáveis:**
```csharp
// Padrão atual: .Replace() inline
var message = Config.KnifeRoundWinnerMessage.Replace("{team}", teamName);
Server.PrintToChatAll($"{Config.ChatPrefix} {message}");
```

**Padrão do Localizer (como ficaria):**
```csharp
// Com Localizer: usa {0}, {1} ao invés de {team}
Server.PrintToChatAll($"{Config.ChatPrefix} {Localizer["knife.winner", teamName]}");

// Per-player:
player.PrintToChat(Localizer.ForPlayer(player, "error.no_permission"));
```

### 4. SFUI Translation Keys Usadas

O sistema de votação Panorama usa keys SFUI nativas do CS2:

| Key | Uso | Arquivo:Linha |
|-----|-----|--------------|
| `#SFUI_vote_kick_player_other` | Vote kick title | VoteService.cs:234 |
| `#SFUI_vote_pause_match` | Vote pause title | VoteService.cs:235 |
| `#SFUI_vote_restart_game` | Vote restart title | VoteService.cs:236 |
| `#SFUI_vote_changelevel` | Vote map change title | VoteService.cs:237 |
| `#SFUI_vote_panorama_vote_default` | Fallback vote title | VoteService.cs:238 |
| `#SFUI_vote_passed_continue_or_swap` | Side choice vote title | VoteService.cs:280 |
| `#SFUI_vote_passed_panorama_vote` | Vote passed notification | PanoramaVote.cs:444 |

Estas keys são traduzidas pelo próprio CS2 engine e não precisam de localização pelo plugin.

### 5. Plugins de Referência com i18n

| Plugin | Idiomas | Padrão |
|--------|---------|--------|
| [CS2-AdminPlus](https://github.com/debr1sj/CS2-AdminPlus) | en, tr, fr, ru, de | `lang/*.json` flat keys |
| [poor-sharptimer](https://github.com/Letaryat/poor-sharptimer) | Multi | `lang/*.json` com cores |
| [cs2-gungame](https://github.com/ssypchenko/cs2-gungame) | en, ru | `lang/*.json` |
| [cs2-roll-the-dice](https://github.com/Kandru/cs2-roll-the-dice) | Multi | `lang/*.json` com template |
| [BannedWords](https://github.com/M-archand/BannedWords) | Multi | `lang/*.json` flat keys |

Todos usam o mesmo padrão: pasta `lang/` com arquivos JSON nomeados por código ISO de idioma.

## Code References

### Arquivos com Strings Hardcoded (por volume)
- `Commands/ChatCommandHandler.cs` - ~150+ strings (maior concentração)
- `Commands/AdminManagementCommands.cs` - ~45 strings
- `Commands/AdminCommands.cs` - ~40 strings
- `Commands/VoteCommands.cs` - ~15 strings
- `Services/VoteService.cs` - ~10 strings
- `Services/PanoramaVote.cs` - ~7 strings (debug/admin)
- `Services/MatchService.cs` - ~3 strings
- `Handlers/PlayerConnectionHandler.cs` - ~2 strings
- `CS2Admin.cs` - ~1 string

### Configuração
- `Config/PluginConfig.cs:37-88` - 10 mensagens configuráveis
- `CS2AdminServiceCollection.cs:30-64` - Distribuição do config para services

### Integração do Localizer (onde usar)
- `CS2Admin.cs` - `Localizer` disponível diretamente (herda de `BasePlugin`)
- Services precisariam receber `IStringLocalizer` via constructor injection ou um wrapper

## Architecture Documentation

### Contagem de Strings por Tipo

| Tipo | Quantidade | Localizável? |
|------|-----------|-------------|
| PrintToChat (erros, feedback) | ~80 | Sim - per-player via `Localizer.ForPlayer()` |
| PrintToChatAll (broadcasts) | ~45 | Sim - mas broadcast é server-wide, não per-player |
| ReplyToCommand (console) | ~40 | Sim - per-player |
| Menu títulos | ~25 | Sim - per-player (menu é per-player) |
| Menu itens | ~50+ | Sim - per-player |
| PrintToCenter (HUD) | 3 | Sim - per-player |
| Console.WriteLine (debug) | ~7 | Não necessário |
| Config messages | 10 | Já configuráveis, precisariam migrar para Localizer |
| SFUI keys | 7 | Não - traduzidas pelo CS2 engine |

### Desafio: PrintToChatAll

`Server.PrintToChatAll()` envia a mesma mensagem a todos os jogadores. Para suporte multi-idioma completo, broadcasts precisariam iterar jogadores e usar `Localizer.ForPlayer()` individualmente:

```csharp
// Atual
Server.PrintToChatAll($"{prefix} {message}");

// Multi-idioma
foreach (var p in Utilities.GetPlayers().Where(p => p.IsValid && !p.IsBot))
{
    p.PrintToChat(Localizer.ForPlayer(p, "key", args));
}
```

Isso afeta ~45 chamadas `PrintToChatAll` no codebase.

### Desafio: Config Messages vs Localizer

Atualmente 10 mensagens são configuráveis via `PluginConfig.cs`. Com o Localizer, essas mensagens migrariam para `lang/*.json`. Isso significaria:
- Remover essas propriedades do config (breaking change)
- OU manter config como override opcional sobre o Localizer

## Historical Context (from thoughts/)

Nenhum documento anterior sobre i18n/localização foi encontrado no diretório thoughts/.

## Related Research

- [CounterStrikeSharp WithTranslations Example](https://github.com/roflmuffin/CounterStrikeSharp/blob/main/examples/WithTranslations/WithTranslationsPlugin.cs)
- [JsonStringLocalizerFactory Source](https://github.com/roflmuffin/CounterStrikeSharp/blob/main/managed/CounterStrikeSharp.API/Core/Translations/JsonStringLocalizerFactory.cs)
- [CS2-AdminPlus (multi-lang reference)](https://github.com/debr1sj/CS2-AdminPlus)
- [GeoLocationLanguageManagerPlugin](https://github.com/aprox2/GeoLocationLanguageManagerPlugin)
- [Microsoft.Extensions.Localization Docs](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/localization-extensibility?view=aspnetcore-8.0)

## Open Questions

1. O `Localizer` do CounterStrikeSharp está disponível em services que não herdam de `BasePlugin`? Se não, como passar o `IStringLocalizer` para `MatchService`, `VoteService`, etc.?
2. `PrintToChatAll` com cores funciona igual quando substituído por loop de `PrintToChat` per-player?
3. As mensagens de menu (`WasdMenu` titles/items) suportam o `Localizer` ou precisam de strings prontas?
4. Como manter retrocompatibilidade com as 10 mensagens configuráveis atuais do `PluginConfig.cs`?
