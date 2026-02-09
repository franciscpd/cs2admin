# Fix Freeze Player During Menu Navigation - Implementation Plan

## Overview

O freeze do jogador durante menus não funciona porque estamos usando CS2MenuManager v1.0.30 que usa `MoveType = MOVETYPE_OBSOLETE` aplicado uma única vez na abertura do menu. O CS2 engine reseta o MoveType, anulando o freeze. A v1.0.34+ mudou para `VelocityModifier = 0.0f` reaplicado a cada tick, que é robusto contra resets do engine. A solução é atualizar o pacote para v1.0.42.

## Current State Analysis

- CS2MenuManager v1.0.30 referenciado em `CS2Admin.csproj:12`
- Todos os 21 menus já usam `{ WasdMenu_FreezePlayer = true }` corretamente
- O freeze não funciona porque a v1.0.30 usa `ChangeMoveType(MOVETYPE_OBSOLETE)` que é aplicado uma vez e resetado pelo engine

### Key Discoveries:
- **v1.0.34** (Mai 2025): Mudou freeze de MoveType para VelocityModifier, reaplicado a cada tick
- **v1.0.42** (Fev 2026): Corrigiu state persistence issue + cleanup de estado ao desconectar/round end
- O código do CS2Admin NÃO precisa mudar -- apenas a versão do pacote

## Desired End State

Ao abrir qualquer menu com `!command`, votekick, etc., o jogador fica imóvel (VelocityModifier = 0) enquanto o menu está aberto, e volta ao normal quando fecha.

**Verificação:** Abrir `!command`, verificar que o jogador não se move. Fechar o menu, verificar que volta a andar.

## What We're NOT Doing

- Não implementamos freeze manual (não é necessário com a versão atualizada)
- Não mudamos nenhum código de menu no CS2Admin
- Não alteramos a lógica de `WasdMenu_FreezePlayer = true` (já está correto)

## Phase 1: Atualizar CS2MenuManager para v1.0.42

### Overview
Atualizar a referência do pacote NuGet de v1.0.30 para v1.0.42.

### Changes Required:

#### 1. CS2Admin.csproj
**File**: `CS2Admin.csproj`
**Changes**: Atualizar versão do PackageReference

```xml
<!-- De: -->
<PackageReference Include="CS2MenuManager" Version="1.0.30" />

<!-- Para: -->
<PackageReference Include="CS2MenuManager" Version="1.0.42" />
```

### Success Criteria:

#### Automated Verification:
- [x] `dotnet restore` baixa a nova versão sem erros
- [x] `dotnet build` compila sem erros (API compatível)

#### Manual Verification:
- [ ] Abrir `!command` e verificar que o jogador fica imóvel
- [ ] Navegar o menu com W/S e verificar que não anda
- [ ] Fechar o menu (R ou timeout) e verificar que volta a andar
- [ ] Testar durante warmup (com respawn habilitado)
- [ ] Testar durante jogo normal

## References

- Research: `thoughts/shared/research/2026-02-09-freeze-player-during-menu-navigation.md`
- CS2MenuManager v1.0.34 changelog: Mudou de MoveType para VelocityModifier
- CS2MenuManager v1.0.42 changelog: Fix WasdMenu state persistence issue
- GitHub: https://github.com/schwarper/CS2MenuManager/releases
