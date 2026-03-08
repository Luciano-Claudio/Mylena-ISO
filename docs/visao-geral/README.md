---
layout: default
title: VISÃO GERAL DO PROJETO — MYLENA
permalink: /visao-geral/
---

# VISÃO GERAL DO PROJETO — MYLENA
## Documentação Técnica dos Módulos Core

**Versão:** 1.0  
**Status:** Em documentação  
**Projeto:** Mylena (Jogo Isométrico — Unity)  

---

## MÓDULOS DOCUMENTADOS

| Módulo | Scripts | Responsabilidade |
|---|---|---|
| `PlayerControllers / Inputs` | 6 scripts | Captura de input, movimento físico, animação direcional, vida, climb, layers físicas |
| `IsoSorting` | 7 scripts | Ordenação visual isométrica manual, alturas lógicas, zonas de escada, footprints |

---

## MAPA DE DEPENDÊNCIAS ENTRE MÓDULOS

```
╔══════════════════════════════════════╗     ╔══════════════════════════════════════╗
║      PLAYERCONTROLLERS / INPUTS      ║     ║            ISOSORTING                ║
║                                      ║     ║                                      ║
║  InputController                     ║     ║  IsoSortingManager (Singleton)       ║
║       │                              ║     ║       ▲ Register/Unregister          ║
║       ▼                              ║     ║       │                              ║
║  MovementController ─────────────────╬─────╬──►  IsoSortable                     ║
║       │ (SetMovement)                ║     ║       │ (logicalHeight, footprint)   ║
║       ▼                              ║     ║       │                              ║
║  AnimationController                 ║     ║  IsoEntityHeight ◄──────────────────╬╗
║                                      ║     ║       │ OnHeightChanged              ║║
║  CharacterController                 ║     ║       ▼                              ║║
║   (RunSpeed, vida)                   ║     ║  PhysicsLayerController ─────────────╬╝
║                                      ║     ║   (altera Physics Layer)             ║
║  ClimbController ────────────────────╬─────╬──► IsoEntityHeight.Ascend/Descend   ║
║   (StartClimb, SetClimbLock)         ║     ║                                      ║
║                                      ║     ║  StairZone / StairExitZone           ║
║  PhysicsLayerController ─────────────╬─────╬──► IsoEntityHeight.ForceHeight      ║
║   (escuta OnHeightChanged)           ║     ║                                      ║
╚══════════════════════════════════════╝     ║  Bounds2D (struct utilitário)        ║
                                             ║  IsoTilemapBoundary (geração auto)   ║
                                             ╚══════════════════════════════════════╝
```

---

## RESUMO POR MÓDULO

### PlayerControllers / Inputs

**13 elementos principais:** 6 scripts + 1 enum + componentes Unity (PlayerInput, Rigidbody2D, Animator)

Responsabilidades cobertas:
- Captura de input via Unity New Input System com jump buffer de 100ms
- Movimentação física via `Rigidbody2D` com normalização diagonal
- Animação direcional isométrica de 8 eixos via blend tree
- Gerenciamento de vida, dano, cura e estado de morte
- Transição de altura por arco senoidal com delay e cooldown
- Sincronização de Physics Layer por altura via evento

Scripts:

| Script | Linhas aprox. | Complexidade |
|---|---|---|
| `InputController` | ~50 | Baixa |
| `MovementController` | ~60 | Baixa |
| `AnimationController` | ~80 | Média |
| `CharacterController` | ~60 | Baixa |
| `ClimbController` | ~100 | Alta |
| `PhysicsLayerController` | ~70 | Média |

Pontos de expansão identificados:
- Sistema de invulnerabilidade (campo pronto, sem implementação)
- Animação de morte (hook comentado)
- Consumidor do jump buffer (buffer pronto, sem consumidor visível)

---

### IsoSorting

**7 scripts** que compõem o sistema completo de sorting manual isométrico.

Responsabilidades cobertas:
- Sorting visual correto por posição Y isométrica
- Comparação geométrica para 3 tipos de footprint (Point, Line, Polyline)
- Separação de alturas lógicas (andares do mapa)
- Topological sort por DFS para resolução de dependências em cadeia
- Cache de dependências estáticas para performance
- Culling por distância de câmera
- Altura lógica de entidades com propagação por evento
- Zonas de escada para controle de transição entre andares
- Geração automática de bordas de sorting para tilemaps

Scripts:

| Script | Linhas aprox. | Complexidade |
|---|---|---|
| `Bounds2D` | ~40 | Baixa |
| `IsoEntityHeight` | ~60 | Baixa |
| `IsoSortable` | ~280 | Muito Alta |
| `IsoSortingManager` | ~240 | Alta |
| `IsoTilemapBoundary` | ~120 | Média |
| `StairZone` | ~35 | Baixa |
| `StairExitZone` | ~30 | Baixa |

Pontos de atenção identificados:
- Log de debug temporário (`"Tree"`) em `IsoSortingManager`
- Constantes `HEIGHT_BAND` e `PIXELS_PER_UNIT` declaradas sem uso ativo
- Sem detecção explícita de ciclos no grafo de sorting
- Listas estáticas persistem entre cenas (considerar limpeza ao trocar de cena)

---

## PONTO DE INTEGRAÇÃO ENTRE MÓDULOS

O elo entre os dois módulos é o componente `IsoEntityHeight`. Ele pertence ao módulo IsoSorting, mas é manipulado diretamente pelo `ClimbController` (PlayerControllers) e escutado pelo `PhysicsLayerController` (PlayerControllers).

```
[PlayerControllers]          [IsoSorting]
ClimbController ──────────► IsoEntityHeight ──────────► IsoSortable
                Ascend()                    logicalHeight
                Descend()                   forceSort
                                │
                                │ OnHeightChanged
                                ▼
[PlayerControllers]
PhysicsLayerController
  → altera Physics Layer
```

Esta é a única ponte direta entre os dois módulos. A dependência é unidirecional: PlayerControllers depende de IsoSorting (através de `IsoEntityHeight`), mas IsoSorting não tem conhecimento de PlayerControllers.

---

*Documentação de Visão Geral — Projeto Mylena*