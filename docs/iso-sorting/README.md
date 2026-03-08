---
layout: default
title: ISOSORTING
permalink: /iso-sorting/
---

# ISOSORTING

**Versão:** 1.0  
**Status:** Em documentação  
**Projeto:** Mylena (Jogo Isométrico — Unity)  
**Módulo:** IsoSorting  
**Autor da documentação:** Gerado a partir da análise de código-fonte  

---

## ÍNDICE

1. [Visão Geral do Módulo](#1-visão-geral-do-módulo)
2. [Por Que o Sorting Manual É Necessário](#2-por-que-o-sorting-manual-é-necessário)
3. [Objetivo](#3-objetivo)
4. [Arquitetura do Sistema](#4-arquitetura-do-sistema)
5. [Componentes Principais](#5-componentes-principais)
6. [Estrutura de Classes](#6-estrutura-de-classes)
7. [Fluxo de Execução](#7-fluxo-de-execução)
8. [Integração entre Scripts](#8-integração-entre-scripts)
9. [Campos, Propriedades e Métodos — Detalhamento por Classe](#9-campos-propriedades-e-métodos--detalhamento-por-classe)
   - 9.1 [Bounds2D](#91-bounds2d)
   - 9.2 [IsoSortable](#92-isosortable)
   - 9.3 [IsoSortingManager](#93-isosortingmanager)
   - 9.4 [IsoEntityHeight](#94-isoentityheight)
   - 9.5 [IsoTilemapBoundary](#95-isotilemapboundary)
   - 9.6 [StairZone](#96-stairzone)
   - 9.7 [StairExitZone](#97-stairexitzone)
10. [O Sistema de Comparação Geométrica](#10-o-sistema-de-comparação-geométrica)
11. [O Topological Sort](#11-o-topological-sort)
12. [O Sistema de Alturas Lógicas](#12-o-sistema-de-alturas-lógicas)
13. [Fluxos Importantes](#13-fluxos-importantes)
    - 13.1 [Fluxo de Registro e Inicialização](#131-fluxo-de-registro-e-inicialização)
    - 13.2 [Fluxo de Sorting por Frame](#132-fluxo-de-sorting-por-frame)
    - 13.3 [Fluxo de Escada — Subida](#133-fluxo-de-escada--subida)
    - 13.4 [Fluxo de Escada — Saída Forçada](#134-fluxo-de-escada--saída-forçada)
    - 13.5 [Fluxo da Borda Automática de Tilemap](#135-fluxo-da-borda-automática-de-tilemap)
14. [Regras de Negócio](#14-regras-de-negócio)
15. [Decisões Arquiteturais](#15-decisões-arquiteturais)
16. [Dependências e Relações com Outros Módulos](#16-dependências-e-relações-com-outros-módulos)
17. [Setup na Cena / Inspector](#17-setup-na-cena--inspector)
18. [Boas Práticas e Manutenção](#18-boas-práticas-e-manutenção)
19. [Limitações Conhecidas e Bugs Potenciais](#19-limitações-conhecidas-e-bugs-potenciais)
20. [Como Expandir o Sistema](#20-como-expandir-o-sistema)
21. [Conclusão](#21-conclusão)

---

## 1. Visão Geral do Módulo

O módulo **IsoSorting** é o sistema responsável por resolver a **ordem de renderização visual em um ambiente isométrico com múltiplos níveis de altura**. Em Unity, a renderização 2D padrão usa o eixo Z (ou `sortingOrder`) para definir quem aparece na frente. Em jogos isométricos, isso é insuficiente: um objeto que está "ao norte" na tela deve aparecer atrás de um objeto que está "ao sul", independentemente de suas posições absolutas no eixo Z.

Este módulo implementa um **sistema completo de sorting manual**, construído do zero, que resolve:

- A **ordem de profundidade visual** entre sprites estáticos e móveis em tempo real,
- A **separação de alturas lógicas** (andares do mapa), garantindo que objetos em andares diferentes nunca interfiram visualmente,
- A **comparação geométrica avançada** para footprints de diferentes formatos (ponto, linha, polyline),
- A **transição física e de camada** quando entidades sobem ou descem escadas,
- A **geração automática de bordas de sorting** para tilemaps isométricos.

Os sete scripts que compõem este módulo formam uma hierarquia funcional clara, desde estruturas de dados utilitárias (`Bounds2D`) até o orquestrador central do sistema (`IsoSortingManager`).

---

## 2. Por Que o Sorting Manual É Necessário

### O Problema do Sorting Isométrico

Em uma perspectiva isométrica, o mundo 3D é projetado em uma tela 2D de forma oblíqua. Isso cria uma ambiguidade visual fundamental: **dois sprites que se sobrepõem na tela podem estar em posições completamente diferentes no espaço lógico do jogo**.

```
Vista isométrica de dois objetos A e B:

        ╱╲   ╱╲
       ╱ A╲ ╱ B╲
      ╲    ╳    ╱
       ╲  ╱ ╲  ╱
        ╲╱   ╲╱

A e B se sobrepõem na tela. Qual aparece na frente?
→ Depende de suas posições relativas no eixo Y do mundo.
```

O Unity não tem como saber automaticamente essa relação — ele apenas vê dois sprites com coordenadas de tela sobrepostas. O `sortingOrder` padrão é um valor fixo, incapaz de resolver relações **dinâmicas** entre sprites que mudam de posição a cada frame.

### Por Que `sortingOrder` Fixo Falha

- Um `sortingOrder` fixo funciona para cenas completamente estáticas onde nenhum sprite se cruza.
- Assim que um personagem caminha por trás de uma árvore ou de um muro, as relações de profundidade **mudam dinamicamente**.
- Para múltiplos andares, o `sortingOrder` precisa refletir não só a posição Y, mas também a **altura lógica** do objeto.

### A Solução Implementada

O sistema usa uma abordagem de **topological sort por dependências de profundidade**:

1. Para cada par de sprites que se sobrepõem na tela (determinado por `Bounds2D.Intersects`), o sistema calcula qual deve vir na frente usando geometria isométrica.
2. Essas relações formam um **grafo de dependências**.
3. Um **topological sort** percorre esse grafo e gera uma ordem linear consistente.
4. O `sortingOrder` de cada sprite é atribuído com base nessa ordem linear, a cada frame.

---

## 3. Objetivo

| Objetivo | Descrição |
|---|---|
| Sorting visual correto | Garantir que sprites isométricos se sobreponham corretamente em qualquer posição |
| Suporte a múltiplas alturas | Separar visualmente objetos em diferentes andares do mapa |
| Sorting dinâmico | Recalcular a ordem a cada frame para objetos móveis (personagem, NPCs) |
| Sorting eficiente | Minimizar recálculos para objetos estáticos via cache de dependências |
| Integração com tilemaps | Gerar automaticamente bordas de sorting para tiles isométricos |
| Integração com escadas | Atualizar altura lógica e layer física ao atravessar zonas de escada |

---

## 4. Arquitetura do Sistema

### Diagrama Textual de Arquitetura

```
┌──────────────────────────────────────────────────────────────────────┐
│                         MÓDULO ISOSORTING                            │
│                                                                      │
│  ┌─────────────────────────────────────────────────────────────────┐ │
│  │                    IsoSortingManager                            │ │
│  │  (Singleton, DontDestroyOnLoad, auto-criado)                   │ │
│  │                                                                 │ │
│  │  Listas:                                                        │ │
│  │  ├─ _staticList    → IsoSortable estáticos                     │ │
│  │  ├─ _movableList   → IsoSortable móveis (player, NPCs)         │ │
│  │  └─ _belowAllList  → IsoSortable abaixo de tudo (sombras)      │ │
│  │                                                                 │ │
│  │  Loop por frame (Update):                                       │ │
│  │  1. RefreshMovableCache()                                       │ │
│  │  2. FilterVisible()      ← usa Bounds2D + CULL_RANGE           │ │
│  │  3. BuildMovingDeps()    ← usa IsoSortable.Compare()           │ │
│  │  4. TopoSort()           ← grafo de dependências               │ │
│  │  5. ApplyOrders()        ← escreve sortingOrder                │ │
│  └──────────────────┬────────────────────────────────────────────┘ │
│                     │ Register / Unregister                         │
│                     ▼                                               │
│  ┌─────────────────────────────────────────────────────────────────┐ │
│  │                      IsoSortable                                │ │
│  │  (componente em cada sprite do cenário e do personagem)        │ │
│  │                                                                 │ │
│  │  ├─ footprintType: Point | Line | Polyline                     │ │
│  │  ├─ logicalHeight, heightSpan                                  │ │
│  │  ├─ isMovable, renderBelowAll                                  │ │
│  │  ├─ cachedBounds: Bounds2D                                     │ │
│  │  ├─ worldPoint1, worldPoint2, worldPolylinePoints              │ │
│  │  ├─ staticDeps, inverseStaticDeps, movingDeps                  │ │
│  │  └─ Compare(a, b) [static] ← lógica de comparação geométrica  │ │
│  └──────────────────┬────────────────────────────────────────────┘ │
│                     │ requer                                         │
│                     ▼                                               │
│  ┌──────────────────────────┐    ┌──────────────────────────────┐   │
│  │     IsoEntityHeight      │    │       Bounds2D               │   │
│  │  (entidades com altura)  │    │  (struct utilitária AABB 2D) │   │
│  │                          │    └──────────────────────────────┘   │
│  │  ├─ CurrentHeight        │                                       │
│  │  ├─ Ascend() / Descend() │    ┌──────────────────────────────┐   │
│  │  ├─ ForceHeight()        │    │    IsoTilemapBoundary        │   │
│  │  └─ OnHeightChanged      │    │  (gera polyline da borda     │   │
│  └──────┬───────────────────┘    │   sul do Tilemap)            │   │
│         │ ForceHeight()          └──────────────────────────────┘   │
│         │                                                           │
│  ┌──────┴──────────────────────┐                                    │
│  │  StairZone / StairExitZone  │                                    │
│  │  (triggers de escada)       │                                    │
│  └─────────────────────────────┘                                    │
└──────────────────────────────────────────────────────────────────────┘

  EXTERNO: PhysicsLayerController ← escuta IsoEntityHeight.OnHeightChanged
           ClimbController        ← chama IsoEntityHeight.Ascend()/Descend()
```

### Camadas Conceituais do Módulo

```
┌─────────────────────────────────────────────────────┐
│  CAMADA DE ZONAS (escadas, saídas)                   │
│  StairZone, StairExitZone                            │
│  → detectam entidades, chamam IsoEntityHeight        │
├─────────────────────────────────────────────────────┤
│  CAMADA DE ENTIDADE (altura lógica)                  │
│  IsoEntityHeight                                     │
│  → mantém altura, propaga evento OnHeightChanged     │
├─────────────────────────────────────────────────────┤
│  CAMADA DE SORTING (ordenação visual)                │
│  IsoSortable, IsoSortingManager                      │
│  → calcula e aplica sortingOrder a cada frame        │
├─────────────────────────────────────────────────────┤
│  CAMADA DE GEOMETRIA (suporte ao sorting)            │
│  Bounds2D, IsoTilemapBoundary                        │
│  → estruturas de dados e geração de footprints       │
└─────────────────────────────────────────────────────┘
```

---

## 5. Componentes Principais

| Script | Tipo | Responsabilidade Principal |
|---|---|---|
| `Bounds2D` | `struct` (serializable) | AABB 2D utilitário para detecção de sobreposição de bounds |
| `IsoSortable` | `MonoBehaviour` | Componente de sorting em cada sprite; contém footprint, comparação e cache |
| `IsoSortingManager` | `MonoBehaviour` (Singleton) | Orquestra o sorting global: coleta, ordena e aplica `sortingOrder` a cada frame |
| `IsoEntityHeight` | `MonoBehaviour` | Gerencia a altura lógica de uma entidade e dispara evento ao mudar |
| `IsoTilemapBoundary` | `MonoBehaviour` | Gera automaticamente a polyline de sorting a partir da borda sul de um Tilemap |
| `StairZone` | `MonoBehaviour` | Trigger que força a altura lógica para o topo da escada ao entrar |
| `StairExitZone` | `MonoBehaviour` | Trigger que força a altura lógica para o nível do chão ao sair pela base |

---

## 6. Estrutura de Classes

### Dependências por `[RequireComponent]`

```
IsoEntityHeight
  └── [RequireComponent] IsoSortable

IsoTilemapBoundary
  └── [RequireComponent] IsoSortable

StairZone
  └── [RequireComponent] Collider2D

StairExitZone
  └── [RequireComponent] Collider2D
```

### Enum `IsoSortable.FootprintType`

Definido internamente em `IsoSortable`:

| Valor | Uso |
|---|---|
| `Point` | Objetos pontuais — personagens, NPCs, barris, itens |
| `Line` | Objetos com extensão horizontal — blocos, muros, troncos caídos |
| `Polyline` | Bordas complexas — tilemap de andar superior, plataformas irregulares |

---

## 7. Fluxo de Execução

### Sequência de Inicialização (frame a frame)

```
Awake (todos os IsoSortable)
  └── Armazena referência ao Transform

Start (todos os IsoSortable) [Coroutine — espera 1 frame]
  ├── Acessa IsoSortingManager.Instance → cria o singleton se não existir
  ├── yield return null   → aguarda todos os Awakes completarem
  └── Initialize()
        ├── Resolve renderersToSort (auto-detecção se vazio)
        ├── RefreshCache() → calcula worldPoint1/2, cachedBounds
        └── IsoSortingManager.Register(this)
              ├── Se renderBelowAll → _belowAllList
              ├── Se isMovable      → _movableList
              └── Caso contrário    → _staticList + BuildStaticDependencies()

Start (IsoEntityHeight)
  └── SetHeight(startingHeight)
        ├── Atualiza _sortable.logicalHeight
        ├── Define _sortable.forceSort = true
        └── Dispara OnHeightChanged → PhysicsLayerController reage
```

### Loop por Frame (IsoSortingManager.Update)

```
Update()  [toda frame]
  1. RefreshMovableCache()
       └── Para cada IsoSortable móvel:
             if NeedsRefresh() → RefreshCache()
             (apenas se transform.hasChanged neste frame)

  2. FilterVisible(_staticList, _visibleStatic)
     FilterVisible(_movableList, _visibleMovable)
       └── Para cada IsoSortable:
             if forceSort → inclui incondicionalmente
             else if dentro do CULL_RANGE (80u) da câmera → inclui

  3. ClearMovingDeps()
       └── Zera movingDeps de todos os visíveis

  4. BuildMovingDependencies()
       └── Para cada móvel vs cada estático visível:
             if cachedBounds.Intersects() →
               IsoSortable.Compare() → adiciona à movingDeps do perdedor

  5. TopoSort(_visibleMovable, _visibleStatic, _sorted)
       └── DFS recursivo pelo grafo de dependências

  6. ApplyOrders(_sorted)
       └── sorted[i].SortingOrder = i * 2

  7. ApplyBelowAll(_belowAllList)
       └── Atribui sortingOrder negativos (-N, -N+2, ...)

LateUpdate()
  └── ClearChangedFlag() em todos os móveis
        → reseta transform.hasChanged
```

---

## 8. Integração entre Scripts

### Tabela de Comunicação

| Emissor | Receptor | Mecanismo | Dado |
|---|---|---|---|
| `IsoSortable.Start()` | `IsoSortingManager` | Chamada estática | `Register(this)` |
| `IsoSortable.OnDestroy()` | `IsoSortingManager` | Chamada estática | `Unregister(this)` |
| `IsoSortingManager.Update()` | `IsoSortable` | Acesso direto a campos | `cachedBounds`, `worldPoint1`, `movingDeps`, `SortingOrder` |
| `IsoEntityHeight.SetHeight()` | `IsoSortable` | Acesso direto a campos | `logicalHeight`, `forceSort` |
| `IsoEntityHeight.SetHeight()` | `PhysicsLayerController` | Evento C# | `OnHeightChanged(int, int)` |
| `IsoEntityHeight.SetHeight()` | `ClimbController` | Chamada de método | `Ascend()` / `Descend()` |
| `StairZone.OnTriggerEnter2D()` | `IsoEntityHeight` | Chamada de método | `ForceHeight(topHeight)` |
| `StairExitZone.OnTriggerEnter2D()` | `IsoEntityHeight` | Chamada de método | `ForceHeight(bottomHeight)` |
| `IsoTilemapBoundary.BuildBoundary()` | `IsoSortable` | Acesso direto a campos | `footprintType`, `polylinePoints` |

---

## 9. Campos, Propriedades e Métodos — Detalhamento por Classe

---

### 9.1 `Bounds2D`

**Tipo:** `struct` (serializable)  
**Localização:** Utilitário de geometria, usado pelo `IsoSortable` e `IsoSortingManager`  
**Responsabilidade:** Representa um AABB (Axis-Aligned Bounding Box) 2D simples, utilizado para verificar sobreposição de sprites antes de calcular sorting.

#### Campos

| Campo | Tipo | Descrição |
|---|---|---|
| `minX` | `float` | Borda esquerda do bounds |
| `minY` | `float` | Borda inferior do bounds |
| `maxX` | `float` | Borda direita do bounds |
| `maxY` | `float` | Borda superior do bounds |

#### Construtores

| Construtor | Descrição |
|---|---|
| `Bounds2D(Bounds bounds)` | Converte `UnityEngine.Bounds` 3D para `Bounds2D` 2D |
| `Bounds2D(float, float, float, float)` | Construção direta com os quatro valores |

#### Métodos

| Método | Retorno | Descrição |
|---|---|---|
| `Intersects(Bounds2D other)` | `bool` | Retorna `true` se os dois bounds se sobrepõem em ambos os eixos (AABB clássico) |
| `Contains(Vector2 point)` | `bool` | Retorna `true` se o ponto está dentro dos bounds |
| `ToString()` | `string` | Representação textual para debug |

#### Lógica de Intersecção AABB

```csharp
// Dois retângulos se intersectam se NÃO houver separação em nenhum eixo:
minX <= other.maxX && other.minX <= maxX   // sem separação horizontal
maxY >= other.minY && other.maxY >= minY   // sem separação vertical
```

#### Papel no Sistema

O `Bounds2D` é o **filtro de fase larga** (broad phase) do sorting. Antes de executar qualquer cálculo geométrico custoso de comparação de footprints, o `IsoSortingManager` verifica se os bounds dos dois sprites sequer se sobrepõem. Se não se sobrepõem, não há necessidade de comparação — economizando processamento significativo.

#### Riscos e Cuidados

- O `cachedBounds` de cada `IsoSortable` é calculado a partir de `renderersToSort[0].bounds`, ou seja, apenas o **primeiro renderer** define o bounds. Para sprites com múltiplos renderers muito separados, isso pode causar imprecisão.
- O bounds **não é atualizado automaticamente** para objetos estáticos. Para móveis, é atualizado via `RefreshCache()` quando `NeedsRefresh()` retorna `true`.

---

### 9.2 `IsoSortable`

**Tipo:** `MonoBehaviour`  
**Responsabilidade:** É o componente central do sistema de sorting. Deve ser adicionado a **todo sprite** que precisa participar da ordenação isométrica. Armazena o footprint do sprite, os dados de cache, as dependências de sorting e implementa a lógica de comparação geométrica estática.

#### Campos Serializados — Altura

| Campo | Tipo | Descrição |
|---|---|---|
| `logicalHeight` | `int` | Andar em que o objeto reside. `0` = chão, `1` = platô, etc. |
| `heightSpan` | `int` | Quantos andares o objeto ocupa visualmente. Mínimo: `1`. |

#### Campos Serializados — Footprint

| Campo | Tipo | Descrição |
|---|---|---|
| `footprintType` | `FootprintType` | Tipo geométrico do footprint: `Point`, `Line` ou `Polyline` |
| `footprintOffset` | `Vector2` | Deslocamento do ponto 1 (ou único ponto) em relação ao transform |
| `footprintOffset2` | `Vector2` | Deslocamento do ponto 2 (Line footprint) |
| `polylinePoints` | `Vector2[]` | Pontos da polyline em espaço local (mínimo 2 para Polyline) |

#### Campos Serializados — Comportamento

| Campo | Tipo | Descrição |
|---|---|---|
| `isMovable` | `bool` | Indica se o objeto se move. Objetos móveis são recalculados todo frame |
| `renderBelowAll` | `bool` | Força renderização abaixo de todos os outros (ex: sombras no chão) |
| `renderersToSort` | `Renderer[]` | Renderers que terão o `sortingOrder` aplicado. Auto-detectados se vazio |

#### Campos de Runtime (NonSerialized)

| Campo | Tipo | Descrição |
|---|---|---|
| `registered` | `bool` | `true` após registro no `IsoSortingManager` |
| `forceSort` | `bool` | Força inclusão na lista visível mesmo fora do culling range |
| `worldPoint1` | `Vector2` | Ponto 1 do footprint em world space (atualizado pelo RefreshCache) |
| `worldPoint2` | `Vector2` | Ponto 2 do footprint em world space |
| `worldPolylinePoints` | `Vector2[]` | Pontos da polyline em world space |
| `cachedBounds` | `Bounds2D` | Bounds AABB do primeiro renderer em world space |
| `staticDeps` | `List<IsoSortable>` | Dependências estáticas: sprites que devem renderizar ANTES deste |
| `inverseStaticDeps` | `List<IsoSortable>` | Inverso das staticDeps (para remoção eficiente) |
| `movingDeps` | `List<IsoSortable>` | Dependências dinâmicas reconstruídas a cada frame |

#### Propriedades Públicas

| Propriedade | Tipo | Descrição |
|---|---|---|
| `MaxHeight` | `int` | `logicalHeight + heightSpan - 1` — altura máxima ocupada |
| `FootprintCenter` | `Vector2` | Centro geométrico do footprint em world space |
| `SortingOrder` | `int` (set/get) | Escreve `sortingOrder` em todos os renderers gerenciados |

#### Lógica do `SortingOrder` (set)

```csharp
// Ao setar SortingOrder = value:
for (int i = 0; i < renderersToSort.Length; i++)
{
    renderersToSort[i].sortingLayerName = "World";
    renderersToSort[i].sortingOrder = value + i;
}
```

Cada renderer subsequente recebe `value + i`, garantindo que múltiplos renderers em um mesmo sprite mantenham ordem interna correta dentro do conjunto.

#### Método `Compare(IsoSortable a, IsoSortable b)` — Estático

Este é o coração da lógica de sorting. Determina qual sprite deve renderizar antes do outro.

**Etapa 1 — Comparação por Altura:**

```
Se as alturas NÃO se sobrepõem:
  → menor logicalHeight = renderiza ANTES (atrás)
  → maior logicalHeight = renderiza DEPOIS (na frente)
```

**Etapa 2 — Comparação Geométrica (alturas sobrepostas):**

```
→ Delega para CompareGeometric(a, b)
   que seleciona o método correto pelo par de footprintTypes:

   Point × Point    → CompareY simples (maior Y = mais atrás)
   Line × Line      → CompareLineVsLine
   Point × Line     → ComparePointVsLine
   Line × Point     → -ComparePointVsLine (invertido)
   Point × Polyline → ComparePointVsPolyline
   Polyline × Point → -ComparePointVsPolyline (invertido)
   Polyline × Poly  → CompareY dos centros (fallback)
   Line × Polyline  → ComparePointVsPolyline (com centro da linha)
   Polyline × Line  → -ComparePointVsPolyline (invertido)
```

#### `NeedsRefresh()` — Otimização de Cache

```csharp
return isMovable && _t.hasChanged && _lastCacheFrame < Time.frameCount;
```

O cache só é atualizado para objetos móveis **quando o transform mudou de fato neste frame**, evitando recálculo desnecessário.

#### Gizmos de Editor

O `IsoSortable` desenha no editor (somente quando selecionado):

- **Point:** Esfera ciano (móvel) ou amarela (estático)
- **Line:** Linha com esferas nos extremos
- **Polyline:** Linha conectando todos os pontos, em magenta
- **Label de altura:** `h{logicalHeight}` ou `h{logicalHeight}→h{MaxHeight}` para span > 1

#### Riscos e Cuidados

- O campo `renderersToSort` deve ter a Sorting Layer `"World"` criada no projeto, pois é a layer hardcoded no setter de `SortingOrder`.
- O `heightSpan > 1` é necessário para objetos como árvores altas que cruzam visualmente múltiplos andares. Sem isso, a comparação de altura rejeitará corretamente a comparação geométrica entre um objeto de `h0` e um de `h1`, mas pode falhar para objetos que se estendem verticalmente.
- O log de debug `"[Compare] {mover.name}... vs Tree..."` presente em `IsoSortingManager.BuildMovingDependencies()` é claramente **código de debug temporário** e deve ser removido antes da build final.

---

### 9.3 `IsoSortingManager`

**Tipo:** `MonoBehaviour` (Singleton, `DontDestroyOnLoad`)  
**Responsabilidade:** Orquestra todo o sistema de sorting. Mantém as listas de sprites registrados, executa o sorting a cada frame e aplica os `sortingOrder` finais. Auto-cria-se na cena quando um `IsoSortable` tenta acessar a instância.

#### Constantes

| Constante | Valor | Descrição |
|---|---|---|
| `HEIGHT_BAND` | `2000` | Slots reservados por andar (não usada ativamente na versão atual) |
| `CULL_RANGE` | `80f` | Raio máximo da câmera para incluir sprite no sorting |
| `PIXELS_PER_UNIT` | `32f` | PPU do projeto (declarada mas não usada ativamente) |

#### Listas Internas

| Lista | Capacidade Inicial | Descrição |
|---|---|---|
| `_staticList` | 256 | Todos os `IsoSortable` estáticos registrados |
| `_movableList` | 64 | Todos os `IsoSortable` móveis registrados |
| `_belowAllList` | 16 | Todos os `IsoSortable` com `renderBelowAll = true` |
| `_visibleStatic` | 128 | Estáticos filtrados visíveis neste frame (lista de trabalho) |
| `_visibleMovable` | 32 | Móveis filtrados visíveis neste frame (lista de trabalho) |
| `_sorted` | 160 | Resultado do topological sort (lista de trabalho) |

Todas as listas de trabalho são **reutilizadas a cada frame sem alocação** (`Clear()` ao invés de `new`).

#### API Pública

| Método | Descrição |
|---|---|
| `Register(IsoSortable)` | Adiciona sprite à lista correta e constrói dependências estáticas |
| `Unregister(IsoSortable)` | Remove sprite da lista e limpa suas dependências |
| `RefreshStaticObject(IsoSortable)` | Força recálculo de dependências de um objeto estático em runtime |

#### Singleton com Auto-Criação

```csharp
public static IsoSortingManager Instance
{
    get
    {
        if (_instance == null)
        {
            var go = new GameObject("(IsoSortingManager)");
            _instance = go.AddComponent<IsoSortingManager>();
            DontDestroyOnLoad(go);
        }
        return _instance;
    }
}
```

O design é deliberado: nenhum `IsoSortingManager` precisa ser colocado na cena manualmente. Ele nasce automaticamente quando o primeiro `IsoSortable` tenta se registrar.

#### Dependências Estáticas vs. Dependências Móveis

O sistema divide as dependências em dois tipos:

**Dependências Estáticas** (`staticDeps`):
- Calculadas **uma única vez** no momento do `Register()`.
- Armazenadas permanentemente em `staticDeps` e `inverseStaticDeps` de cada sprite.
- Válidas enquanto o sprite não mudar de posição ou altura.
- Para atualizar: `RefreshStaticObject()`.

**Dependências Móveis** (`movingDeps`):
- Recalculadas **todo frame** entre móveis × estáticos e móveis × móveis.
- Zeradas em `ClearMovingDeps()` no início de cada frame.
- Consideram apenas sprites cujos bounds se intersectam.

#### Pipeline de Sorting (Update)

```
┌─────────────────────────────────────────────────────────────────┐
│  1. RefreshMovableCache                                         │
│     → Atualiza worldPoint1/2 e cachedBounds dos sprites móveis │
│       que se moveram neste frame (transform.hasChanged)         │
├─────────────────────────────────────────────────────────────────┤
│  2. FilterVisible                                               │
│     → Exclui sprites além de 80u da câmera (culling)           │
│     → Sprites com forceSort=true passam incondicionalmente      │
├─────────────────────────────────────────────────────────────────┤
│  3. ClearMovingDeps + BuildMovingDependencies                   │
│     → Para cada par (móvel, estático) cujos bounds se          │
│       intersectam: chama IsoSortable.Compare()                  │
│     → O "perdedor" recebe o "vencedor" em sua movingDeps       │
├─────────────────────────────────────────────────────────────────┤
│  4. TopoSort (DFS)                                              │
│     → Visita móveis antes de estáticos (desempate)             │
│     → Recursão via staticDeps + movingDeps                     │
│     → Resultado: _sorted[0] = mais atrás, _sorted[N] = frente │
├─────────────────────────────────────────────────────────────────┤
│  5. ApplyOrders                                                 │
│     → _sorted[i].SortingOrder = i * 2                          │
│     → Intervalo de 2 entre orders garante espaço para          │
│       múltiplos renderers por sprite                           │
├─────────────────────────────────────────────────────────────────┤
│  6. ApplyBelowAll                                               │
│     → Atribui orders negativos para sombras/elementos de chão  │
└─────────────────────────────────────────────────────────────────┘
```

#### Riscos e Cuidados

- O log de debug com `stat.name == "Tree"` em `BuildMovingDependencies` é **código temporário** e deve ser removido.
- `PIXELS_PER_UNIT` e `HEIGHT_BAND` estão declarados mas não usados ativamente na versão atual — indicam planejamento de funcionalidade futura ou refatoração incompleta.
- As listas estáticas (`_staticList`, `_movableList`) são `static readonly`, o que significa que **persistem entre cenas** mesmo que o singleton seja destruído e recriado. Isso é intencional para `DontDestroyOnLoad`, mas pode causar listas sujas se a cena for recarregada sem o `Unregister` de todos os sprites.

---

### 9.4 `IsoEntityHeight`

**Tipo:** `MonoBehaviour`  
**Requer:** `IsoSortable`  
**Responsabilidade:** Gerencia a **altura lógica** de uma entidade no mundo isométrico. É o ponto central de comunicação entre o módulo de sorting e os sistemas de física, climb e zonas de escada.

#### Campos Serializados

| Campo | Tipo | Descrição |
|---|---|---|
| `startingHeight` | `int` | Altura inicial da entidade ao entrar na cena |
| `_currentHeight` | `int` | Altura atual (readonly no Inspector — somente debug) |

#### Propriedades

| Propriedade | Tipo | Descrição |
|---|---|---|
| `CurrentHeight` | `int` | Getter somente-leitura de `_currentHeight` |

#### Evento

```csharp
public event Action<int, int> OnHeightChanged;
// Parâmetros: (int oldHeight, int newHeight)
```

Disparado sempre que a altura muda. Inscritores conhecidos:
- `PhysicsLayerController` → altera a Physics Layer do personagem
- (Potencialmente qualquer sistema externo que precise reagir a mudanças de altura)

#### Métodos

| Método | Retorno | Descrição |
|---|---|---|
| `Ascend()` | `void` | Incrementa altura em 1 |
| `Descend()` | `void` | Decrementa altura em 1 |
| `ForceHeight(int)` | `void` | Define altura diretamente (usado pelas StairZones) |
| `SetHeight(int)` | `void` (privado) | Lógica interna: clamp, atualiza IsoSortable, dispara evento |

#### Lógica do `SetHeight`

```csharp
private void SetHeight(int height)
{
    int clamped = Mathf.Max(0, height);      // nunca abaixo de 0
    if (_currentHeight == clamped) return;   // sem mudança = sem evento

    int oldHeight = _currentHeight;
    _currentHeight = clamped;
    _sortable.logicalHeight = clamped;       // atualiza sorting
    _sortable.forceSort = true;              // força reavaliação visual imediata

    OnHeightChanged?.Invoke(oldHeight, clamped);
}
```

#### Papel no Sistema de Sorting

A linha `_sortable.logicalHeight = clamped` é o elo entre o sistema de altura e o sistema de sorting. Quando a altura muda:

1. O `IsoSortable` passa a comparar sua altura com outros usando o novo valor.
2. `forceSort = true` garante que na próxima frame o objeto seja incluído na lista visível **independentemente do culling**, forçando uma reavaliação completa das dependências.

#### Riscos e Cuidados

- A altura é clamped em `0`, mas **não possui limite superior**. Um sistema externo mal configurado poderia aumentar a altura indefinidamente.
- `Descend()` em `h0` resulta em `SetHeight(-1)` → clampeia para `0` → detecta que `_currentHeight == 0` já → **nenhuma chamada de evento**, comportamento correto.

---

### 9.5 `IsoTilemapBoundary`

**Tipo:** `MonoBehaviour`  
**Requer:** `IsoSortable`  
**Responsabilidade:** Gera automaticamente a polyline de sorting que representa a **borda sul** de um Tilemap isométrico. Isso permite que o `IsoSortable` do andar superior use um footprint preciso e complexo, ao invés de um ponto ou linha simples.

#### Campos Serializados

| Campo | Tipo | Descrição |
|---|---|---|
| `tilemap` | `Tilemap` | O Tilemap cujas bordas serão calculadas |
| `yBias` | `float` | Offset vertical aplicado aos pontos calculados (ajuste fino visual) |

#### Método Principal: `BuildBoundary()`

Pode ser chamado em runtime (via `Start`) ou manualmente no editor via `[ContextMenu("Rebuild Boundary")]`.

**Algoritmo:**

```
1. tilemap.CompressBounds()
   → otimiza o BoundsInt para a área real com tiles

2. Para cada célula (x, y) com tile:
   → Verifica presença de tiles vizinhos ao sudoeste (x-1, y-1) e sudeste (x, y-1)
   → Se ambos presentes: célula não é borda sul → skip
   → Se um ou ambos ausentes: calcula pontos da borda sul desta célula

3. Por cada X, mantém apenas o ponto com menor Y (southernmost point)
   (usando Dictionary<float, float> byX)

4. Ordena todos os X coletados
5. Converte para Vector2[] em espaço local (relativo ao transform)
6. Atribui ao IsoSortable.polylinePoints
7. Chama IsoSortable.RefreshCache()
```

#### Visualização no Editor

O `OnDrawGizmosSelected` desenha a polyline calculada em vermelho com esferas nos vértices, facilitando a inspeção visual do resultado.

#### Casos de Borda do Algoritmo

| Condição | Ação |
|---|---|
| Sem tile SW e sem tile SE | Adiciona ponto oeste (mid), centro sul e ponto leste (mid) |
| Sem tile SW apenas | Adiciona ponto oeste (mid) e centro sul |
| Sem tile SE apenas | Adiciona centro sul e ponto leste (mid) |

Isso garante que a polyline seja contínua e siga com precisão o contorno isométrico do andar.

#### Riscos e Cuidados

- O `BuildBoundary` depende que o `Tilemap` esteja **totalmente populado antes** de ser chamado. Tilemaps gerados proceduralmente em runtime precisariam chamar `BuildBoundary()` manualmente após a geração.
- A polyline é gerada em **espaço local** (relativo ao `transform.position`). Se o objeto com `IsoTilemapBoundary` for movido, os pontos locais ainda são válidos, mas o `IsoSortable.RefreshCache()` deve ser chamado novamente.
- A ferramenta `[ContextMenu("Rebuild Boundary")]` é muito útil para iterar no editor sem entrar em play mode.

---

### 9.6 `StairZone`

**Tipo:** `MonoBehaviour`  
**Requer:** `Collider2D` (configurado como trigger automaticamente)  
**Responsabilidade:** Trigger que representa a **área de topo de uma escada**. Ao entrar nessa zona, uma entidade é forçada para a altura máxima da escada.

#### Campos Serializados

| Campo | Tipo | Descrição |
|---|---|---|
| `topHeight` | `int` | Altura lógica do topo da escada |
| `StairWall` | `GameObject` | Objeto de parede/visual que é ativado ao entrar na zona |

#### Callbacks de Trigger

| Callback | Ação |
|---|---|
| `OnTriggerEnter2D` | `entity.ForceHeight(topHeight)` + ativa `StairWall` |
| `OnTriggerStay2D` | `entity.ForceHeight(topHeight)` somente se `CurrentHeight < topHeight` |
| `OnTriggerExit2D` | `entity.ForceHeight(topHeight)` + desativa `StairWall` |

#### Papel do `StairWall`

O `StairWall` é um objeto visual (provavelmente um sprite de parede ou borda) que deve aparecer **na frente do personagem** quando ele está próximo à borda do andar superior, completando a ilusão de que ele acabou de subir. É ativado ao entrar e desativado ao sair.

> **Observação:** O comportamento de `OnTriggerExit2D` chamar `ForceHeight(topHeight)` — e não reduzir a altura — implica que a descida de volta ao andar inferior é tratada pelo `StairExitZone` (uma zona separada na base da escada) ou pelo `ClimbController`. A `StairZone` é responsável apenas pelo andar superior.

#### Lógica do `OnTriggerStay2D`

```csharp
if (entity.CurrentHeight < topHeight)
    entity.ForceHeight(topHeight);
```

O `Stay` usa condição de guarda para evitar disparar o evento desnecessariamente a cada frame se a entidade já está na altura correta. Boa prática de performance.

---

### 9.7 `StairExitZone`

**Tipo:** `MonoBehaviour`  
**Requer:** `Collider2D` (configurado como trigger automaticamente)  
**Responsabilidade:** Trigger na **base da escada** que força a entidade a retornar à altura do chão. Complementa a `StairZone` para tratar o retorno ao andar inferior.

#### Campos Serializados

| Campo | Tipo | Descrição |
|---|---|---|
| `bottomHeight` | `int` | Altura lógica do andar base |

#### Callbacks de Trigger

Todos os três callbacks (`Enter`, `Stay`, `Exit`) chamam `entity.ForceHeight(bottomHeight)` incondicionalmente.

> **Observação:** Ao contrário da `StairZone`, a `StairExitZone` não tem condição de guarda no `Stay`, o que significa que o evento `OnHeightChanged` potencialmente dispara toda frame enquanto a entidade está na zona **e** sua altura já é `bottomHeight`. Porém, a lógica interna de `IsoEntityHeight.SetHeight()` retorna cedo (`if (_currentHeight == clamped) return`) quando a altura não mudou, então o evento não é propagado desnecessariamente.

#### Relação com StairZone

```
Escada isométrica vista de cima:

  ┌──────────────┐
  │  StairZone   │  ← topo da escada
  │  (topHeight) │
  └──────┬───────┘
         │ escada
  ┌──────┴───────┐
  │ StairExitZone│  ← base da escada
  │(bottomHeight)│
  └──────────────┘
```

As duas zonas trabalham em conjunto: `StairZone` força para o andar de cima, `StairExitZone` força para o andar de baixo.

---

## 10. O Sistema de Comparação Geométrica

Esta seção detalha a lógica de comparação implementada em `IsoSortable.Compare()` e seus métodos auxiliares, que é o núcleo do sorting isométrico.

### Princípio Base: Posição Y Isométrica

Em um jogo isométrico, objetos mais ao sul na tela (Y menor em world space) devem aparecer **na frente** de objetos ao norte (Y maior). Esta é a regra fundamental:

```
Norte (Y alto) = mais atrás
Sul  (Y baixo) = mais na frente
```

### Comparação por Altura (Fase 1)

Antes de qualquer geometria, o sistema verifica se as faixas de altura se sobrepõem:

```
A.logicalHeight ≤ B.MaxHeight  E  B.logicalHeight ≤ A.MaxHeight
→ Sobreposição = true → vai para comparação geométrica

Caso contrário:
→ menor logicalHeight = -1 (atrás), maior = +1 (na frente)
```

Isso garante que um personagem em `h0` nunca seja comparado geometricamente com um objeto exclusivo de `h1`.

### Comparação Point × Point

O mais simples: o objeto com **maior Y** está mais ao norte → aparece **atrás**.

```
return b.worldPoint1.y.CompareTo(a.worldPoint1.y);
// Se B.y > A.y → B vem antes (-1 para A = A atrás)
```

### Comparação Point × Line

Dado um ponto e uma linha (segmento), o sistema interpola o Y da linha na posição X do ponto:

```
Se ponto está acima de toda a linha: ponto vai atrás (-1)
Se ponto está abaixo de toda a linha: ponto vai na frente (+1)
Se ponto está na faixa Y: interpola e compara
```

```
Exemplo visual isométrico:

     ponto (P)               P está acima da linha L
         •                   → P mais ao norte → P vai atrás
    ─────────────────── L
    p1              p2
```

### Comparação Line × Line

Testa os dois extremos de A contra a linha B, e os dois extremos de B contra a linha A:

```
c1 = ComparePointVsLine(A.p1, B)
c2 = ComparePointVsLine(A.p2, B)

Se c1 == c2 → resultado consistente, retorna c1
Senão testa B contra A
Se ainda inconsistente (linhas se cruzam): usa center Y como desempate
```

### Comparação Point × Polyline

Para cada segmento da polyline, verifica se o X do ponto está na faixa horizontal do segmento. Se sim, interpola o Y do segmento naquele X e compara:

```
Para o segmento [p_i, p_{i+1}]:
  if point.x ∈ [minX, maxX] do segmento:
    yOnLine = Lerp(p_i.y, p_{i+1}.y, t)
    return point.y < yOnLine ? +1 : -1

Se fora de todos os segmentos:
  compara com extremo mais próximo (esquerda ou direita)
```

### Diagrama Completo de Tipos de Footprint

```
POINT          LINE                POLYLINE
  •            p1──────p2          p1──p2
                                       └──p3
                                          └──p4

Gizmo: esfera   linha com esferas   linha conectada (magenta)
       (ciano=móvel, amarelo=estático)
```

---

## 11. O Topological Sort

### Por Que Topological Sort?

Um simples `List.Sort()` com o `Compare()` poderia resolver situações simples, mas falha em casos onde as relações de ordem formam **ciclos** (A na frente de B, B na frente de C, C na frente de A — impossível de resolver linearmente). O topological sort, por ser baseado em DFS de grafo, é mais robusto e lida naturalmente com dependências em cadeia.

### Algoritmo DFS

```
TopoSort(movables, statics, result):
  _visited.Clear()
  Para cada móvel: Visit(móvel)
  Para cada estático: Visit(estático)

Visit(node):
  if node já visitado: return
  marcar como visitado
  Para cada dep em node.movingDeps: Visit(dep)    ← quem deve vir ANTES
  Para cada dep em node.staticDeps: Visit(dep)    ← quem deve vir ANTES
  result.Add(node)                                 ← eu venho depois deles
```

O nó é adicionado ao resultado **depois** de todas as suas dependências serem resolvidas. Isso garante que `result[0]` é o sprite mais atrás e `result[N]` é o mais na frente.

### Prioridade: Móveis Antes de Estáticos

Os móveis são visitados primeiro no loop de `TopoSort`. Em caso de empate de profundidade (sem dependência mútua), o sprite móvel tenderá a ter um index mais alto no array `_sorted`, portanto um `sortingOrder` maior — aparecendo **na frente** de estáticos sem relação de dependência. Esta é uma escolha de design explícita, garantindo que o personagem fique na frente em situações ambíguas.

### Detecção de Ciclos

O sistema atual **não implementa detecção explícita de ciclos**. Se um ciclo existir no grafo (causado por configurações inválidas de footprint ou altura), o DFS poderia entrar em loop infinito. O `HashSet<int> _visited` previne revisitas dentro de uma única execução de `TopoSort`, mas não previne grafos com ciclos em si.

> **Observação:** Em prática, ciclos genuínos no sorting isométrico são raros e geralmente indicam problemas de configuração de footprint ou sobreposição física impossível de resolver. Esta é uma limitação conhecida do sistema.

---

## 12. O Sistema de Alturas Lógicas

### Conceito de Andar (Height)

Cada posição no mundo isométrico pertence a um andar lógico:

```
h0 = chão (nível base)
h1 = primeiro platô/segundo andar
h2 = segundo platô/terceiro andar
...
```

### Separação por Faixa de SortingOrder

A constante `HEIGHT_BAND = 2000` indica que o sistema foi projetado para reservar 2000 slots de `sortingOrder` por andar. Na implementação atual, `ApplyOrders` usa `i * 2` sequencialmente sem respeitar bandas fixas — a constante `HEIGHT_BAND` está **declarada mas não aplicada ativamente**.

A separação de altura é feita **pelo `Compare()` via `logicalHeight`**: sprites de andares diferentes e sem sobreposição de span não são comparados geometricamente, e o de maior altura sempre ganha. Isso naturalmente os mantém separados sem precisar de bandas numéricas fixas.

### Efeito em Cadeia da Mudança de Altura

```
IsoEntityHeight.SetHeight(newH)
        │
        ├──► IsoSortable.logicalHeight = newH
        │     → Compare() agora usa a nova altura
        │
        ├──► IsoSortable.forceSort = true
        │     → próxima frame: incluído na lista visível incondicionalmente
        │
        └──► OnHeightChanged(old, new)
              → PhysicsLayerController: altera layer física
              → (qualquer outro inscrito)
```

---

## 13. Fluxos Importantes

### 13.1 Fluxo de Registro e Inicialização

```
[Cena carrega]
        │
        ▼
IsoSortable.Start() [Coroutine]
  → acessa IsoSortingManager.Instance
        │ (se não existe)
        ▼
  GameObject "(IsoSortingManager)" criado automaticamente
  IsoSortingManager.Awake() → DontDestroyOnLoad
        │
        ▼
  yield return null → aguarda 1 frame
        │
        ▼
  IsoSortable.Initialize()
    → auto-detecta Renderers se necessário
    → RefreshCache() → calcula worldPoints e cachedBounds
    → IsoSortingManager.Register(this)
          │
          ├─ isMovable=false → _staticList + BuildStaticDependencies()
          ├─ isMovable=true  → _movableList
          └─ renderBelowAll  → _belowAllList
```

### 13.2 Fluxo de Sorting por Frame

```
[Todo frame — IsoSortingManager.Update()]

1. Móveis que se moveram → RefreshCache()
        │
2. Filtra visíveis (distância câmera ≤ 80u ou forceSort=true)
        │
3. Zera movingDeps de todos os visíveis
        │
4. Para cada par (móvel, estático) com bounds sobrepostos:
     IsoSortable.Compare() → vencedor entra em movingDeps do perdedor
        │
5. DFS topological sort
     → produz array _sorted: [mais atrás ... mais na frente]
        │
6. sorted[i].SortingOrder = i * 2
        │
7. Sombras: SortingOrder = valores negativos (-N, -N+2, ...)

[LateUpdate]
   → ClearChangedFlag() em móveis
```

### 13.3 Fluxo de Escada — Subida

```
[Personagem entra no Collider2D da StairZone]
        │
        ▼
StairZone.OnTriggerEnter2D(collider)
  → entity = collider.GetComponentInParent<IsoEntityHeight>()
  → entity.ForceHeight(topHeight)     ← ex: topHeight = 1
  → StairWall.SetActive(true)         ← ativa parede visual
        │
        ▼
IsoEntityHeight.SetHeight(1)
  → IsoSortable.logicalHeight = 1
  → IsoSortable.forceSort = true
  → OnHeightChanged(0, 1)
        │
        ├──► PhysicsLayerController → layer = "Entities_H1"
        │     → personagem agora colide com objetos de h1
        │
        └──► (IsoSortingManager na próxima frame)
              → personagem agora compara com objetos de h1
              → objetos de h0 ficam automaticamente atrás
```

### 13.4 Fluxo de Escada — Saída Forçada

```
[Personagem entra no Collider2D da StairExitZone (na base)]
        │
        ▼
StairExitZone.OnTriggerEnter2D(collider)
  → entity.ForceHeight(bottomHeight)  ← ex: bottomHeight = 0
        │
        ▼
IsoEntityHeight.SetHeight(0)
  → IsoSortable.logicalHeight = 0
  → OnHeightChanged(1, 0)
  → PhysicsLayerController → layer = "Entities_H0"
```

### 13.5 Fluxo da Borda Automática de Tilemap

```
[Cena carrega com objeto H1_SortBoundary]
        │
        ▼
IsoTilemapBoundary.Start()
  → BuildBoundary()
        │
        ▼
1. Coleta todas as células do Tilemap em um HashSet
2. Para cada célula de borda sul: calcula pontos da borda isométrica
3. Para cada X: mantém apenas o ponto mais ao sul (byX dictionary)
4. Ordena por X → cria array de pontos em espaço local
        │
        ▼
5. IsoSortable.footprintType = Polyline
   IsoSortable.polylinePoints = points[]
   IsoSortable.RefreshCache()
        │
        ▼
[Na próxima frame de sorting]
   O piso do h1 agora tem um footprint tipo Polyline complexo
   que representa com precisão sua borda sul isométrica
   → Personagem em h0 é comparado via ComparePointVsPolyline()
```

---

## 14. Regras de Negócio

1. **Objetos em alturas não sobrepostas nunca são comparados geometricamente.** A comparação de altura é a fase prioritária. Isso garante que sprites de andares diferentes nunca "vazem" visualmente entre si.

2. **Spans de altura permitem objetos que cruzam andares.** Um objeto com `logicalHeight=0` e `heightSpan=2` possui `MaxHeight=1`, sobrepondose tanto com h0 quanto com h1. Útil para árvores, postes, etc.

3. **Objetos estáticos têm dependências calculadas uma única vez.** O custo de `BuildStaticDependencies` ocorre apenas no `Register()`, não a cada frame.

4. **Objetos móveis têm dependências recalculadas todo frame.** Mas somente entre objetos cujos bounds se intersectam, otimizando o custo.

5. **Culling por distância de câmera.** Apenas sprites dentro de 80 unidades da câmera participam do sorting. Sprites com `forceSort=true` são incluídos incondicionalmente (usado quando a altura muda).

6. **`renderBelowAll` = sortingOrder negativo.** Garante que sombras e elementos de chão fiquem abaixo de absolutamente tudo.

7. **A altura lógica mínima é 0.** `SetHeight` faz clamp para evitar alturas negativas.

8. **O `StairWall` é responsabilidade da `StairZone`.** Ele é ativado/desativado pelo trigger para complementar a ilusão visual da escada.

9. **O espaçamento de 2 entre sortingOrders** (`i * 2`) permite que sprites com múltiplos renderers usem `value + 0`, `value + 1` sem conflito com o sortingOrder do sprite seguinte.

---

## 15. Decisões Arquiteturais

### ✅ Separação entre Dependências Estáticas e Móveis

**Decisão:** Calcular dependências entre objetos estáticos uma única vez e recalcular apenas as dependências envolvendo objetos móveis.

**Justificativa:** A vasta maioria dos sprites de cena (paredes, árvores, blocos) nunca muda de posição. Recalcular suas relações a cada frame seria desperdiçador. O cache estático permite que apenas os sprites relevantes para o movimento do personagem sejam reavaliados.

### ✅ Topological Sort com DFS ao Invés de Sort Comparativo

**Decisão:** Usar DFS de grafo ao invés de `List.Sort()` com `Compare()`.

**Justificativa:** A função `Compare()` não é transitiva em todos os casos (especialmente com polylines e linhas cruzadas). Um sort comparativo que assume transitividade pode produzir resultados incorretos. O DFS de grafo respeita as dependências diretas calculadas par a par.

### ✅ Footprints de Três Tipos (Point, Line, Polyline)

**Decisão:** Permitir que cada sprite defina sua "pegada" geométrica no chão com três níveis de precisão.

**Justificativa:** A comparação por ponto Y simples falha para objetos com extensão horizontal (muros, blocos). A comparação linear permite resolver a maioria dos casos. A polyline permite bordas complexas (tilemaps irregulares) sem comprometer a precisão.

### ✅ Auto-Criação do Manager

**Decisão:** `IsoSortingManager` cria-se automaticamente quando acessado, via `DontDestroyOnLoad`.

**Justificativa:** Remove dependência de setup manual na cena. Qualquer sprite pode ser adicionado ao projeto sem preocupação com a presença do manager.

### ✅ `forceSort` para Mudanças de Altura

**Decisão:** Quando `IsoEntityHeight.SetHeight()` é chamado, `forceSort` é ativado no `IsoSortable`.

**Justificativa:** Sem isso, um objeto que acabou de mudar de andar poderia ficar fora do culling range e não ter seu sortingOrder atualizado imediatamente, causando um frame de artefato visual.

---

## 16. Dependências e Relações com Outros Módulos

| Dependência | Módulo | Como é usada |
|---|---|---|
| `IsoEntityHeight` | Este módulo | `ClimbController` chama `Ascend()`/`Descend()`; `PhysicsLayerController` escuta `OnHeightChanged` |
| `Tilemap` | Unity Tilemaps | `IsoTilemapBoundary` usa `tilemap.cellBounds`, `HasTile()` e `GetCellCenterWorld()` |
| `Camera.main` | Unity | `IsoSortingManager` usa posição da câmera para culling |
| `Renderer` | Unity | `IsoSortable.SortingOrder` escreve em `renderer.sortingOrder` e `sortingLayerName` |

---

## 17. Setup na Cena / Inspector

### Hierarquia Típica de Cena

```
Scene
├── Tilemap_H0          (andar base)
│   └── IsoSortable (isMovable=false, logicalHeight=0, footprint=Line/Polyline)
│
├── H1_SortBoundary     (borda do andar superior)
│   └── IsoSortable (isMovable=false, logicalHeight=1, footprint=Polyline)
│   └── IsoTilemapBoundary (referência ao Tilemap_H1)
│
├── Tilemap_H1          (andar superior)
│   └── IsoSortable (isMovable=false, logicalHeight=1)
│
├── Tree                (árvore cruzando h0→h1)
│   └── IsoSortable (isMovable=false, logicalHeight=0, heightSpan=2, footprint=Point)
│
├── Stair
│   ├── StairZone (topHeight=1)    ← trigger no topo
│   │   └── Collider2D (trigger)
│   └── StairExitZone (bottomHeight=0) ← trigger na base
│       └── Collider2D (trigger)
│
├── Shadow_Mylena
│   └── IsoSortable (renderBelowAll=true)
│
└── Mylena
    └── IsoSortable (isMovable=true, logicalHeight=0, footprint=Point)
    └── IsoEntityHeight (startingHeight=0)
```

### Checklist de Configuração

| Item | Onde configurar | Observação |
|---|---|---|
| Sorting Layer `"World"` | `Edit → Project Settings → Tags and Layers` | Obrigatória; usada pelo `SortingOrder` setter |
| `isMovable = true` | `IsoSortable` do personagem/NPCs | Garante recalculação por frame |
| `footprintType` correto | `IsoSortable` de cada objeto | Point para entidades, Line para blocos, Polyline para tilemaps |
| `logicalHeight` correto | `IsoSortable` de cada objeto | Corresponde ao andar visual do objeto |
| `heightSpan > 1` | `IsoSortable` de objetos altos | Para árvores, postes, elementos que cruzam andares |
| `startingHeight` | `IsoEntityHeight` do personagem | Deve corresponder ao andar inicial |
| Triggers configurados | `StairZone` / `StairExitZone` | O Collider2D é automaticamente marcado como trigger no Awake |
| `StairWall` referenciado | `StairZone` | Pode ser nulo se não houver parede visual de escada |
| `yBias` | `IsoTilemapBoundary` | Ajuste fino se a polyline gerada estiver ligeiramente desalinhada |
| `CULL_RANGE` | Constante em `IsoSortingManager` | Ajustar se a câmera for muito zoom-out |

---

## 18. Boas Práticas e Manutenção

- **`forceSort = true` após qualquer mudança de dados do IsoSortable.** Se alterar `logicalHeight` ou `footprint` de um objeto estático em runtime, chamar `IsoSortingManager.RefreshStaticObject()` para reconstruir as dependências.

- **Não mover objetos estáticos em runtime sem notificar o Manager.** As dependências estáticas são calculadas uma vez. Mover um estático sem chamar `RefreshStaticObject()` resultará em sorting incorreto.

- **Manter Sorting Layer `"World"` no projeto.** A string está hardcoded no setter de `SortingOrder`. Uma renomeação desta layer quebraria o sistema silenciosamente.

- **Remover o log de debug `"Tree"` antes da build final.** O log em `BuildMovingDependencies()` roda toda frame enquanto a Tree está visível e tem impacto de performance.

- **Usar `heightSpan` para objetos altos.** Uma árvore sem `heightSpan=2` pode ser incorretamente "ocultada" atrás de objetos do andar superior quando deveria cruzar ambos.

- **`IsoTilemapBoundary.BuildBoundary()` em Editor.** Use o `[ContextMenu]` para pré-computar a polyline no editor e verificar o resultado com o gizmo antes de entrar em play mode.

---

## 19. Limitações Conhecidas e Bugs Potenciais

| Limitação / Bug | Descrição | Mitigação |
|---|---|---|
| Ciclos no grafo de sorting | Se dois sprites formam dependência mútua (A na frente de B e B na frente de A), o sistema não detecta e pode retornar resultado inconsistente | Garantir que footprints não criem ambiguidades geométricas; revisar configuração de objetos que causam flickering |
| `cachedBounds` usa apenas o primeiro renderer | Sprites com múltiplos renderers muito separados podem ter bounds imprecisos, causando culling prematuro | Garantir que o primeiro renderer em `renderersToSort` englobe o sprite inteiro |
| Listas estáticas persistem entre cenas | `_staticList`, `_movableList` são `static readonly` e não são limpas ao descarregar cenas | Garantir que todos os objetos cheguem ao `OnDestroy` → `Unregister()` corretamente |
| Log de debug da "Tree" | Impacto de performance todo frame com a Tree visível | Remover antes da build |
| `HEIGHT_BAND` e `PIXELS_PER_UNIT` não usados | Constantes declaradas sem uso ativo — indicam refatoração incompleta | Implementar ou remover para clareza |
| Sem limite superior de altura | `Ascend()` pode incrementar indefinidamente | Adicionar clamp superior em `SetHeight()` se houver número máximo de andares |
| Polyline × Line fallback não geométrico | A comparação `Line vs Polyline` usa o centro da linha, não o ponto mais próximo | Pode causar sorting errado em bordas irregulares; considerar implementação mais precisa |
| `StairExitZone` sem guarda no Stay | `ForceHeight(bottomHeight)` chamado todo frame no Stay, mas `SetHeight` tem guarda interna | Sem efeito prático, mas adicionar a guarda no `Stay` seria mais explícito |

---

## 20. Como Expandir o Sistema

### Adicionar um Novo Tipo de Footprint

1. Adicionar o novo valor ao enum `FootprintType`.
2. Implementar método `CompareXVsY` e seu inverso.
3. Adicionar o par ao `switch` em `CompareGeometric()`.
4. Atualizar o `OnDrawGizmosSelected` para visualizar o novo tipo.
5. Atualizar `RefreshCache()` para calcular os pontos em world space.

### Adicionar Suporte a Mais de Uma Sorting Layer

1. Tornar a string `"World"` configurável por campo no `IsoSortable`.
2. Atualizar o setter de `SortingOrder` para usar o campo configurável.

### Implementar Detecção de Ciclos

1. Antes de `ApplyOrders`, verificar se `_sorted.Count < _visibleStatic.Count + _visibleMovable.Count`.
2. Se sim, um ciclo impediu a visita de alguns nós — logar aviso e forçar uma ordem fallback.

### Adicionar NPCs ao Sistema

1. Criar objeto com `IsoSortable` (isMovable=true) e `IsoEntityHeight`.
2. Adicionar `PhysicsLayerController` se precisar de separação física por altura.
3. O `IsoSortingManager` gerenciará automaticamente.

### Suporte a Objetos Procedurais

1. Para objetos criados em runtime, garantir que `IsoSortable.Start()` seja executado (ou chamar `Initialize()` manualmente se instanciado fora do ciclo normal).
2. Para tilemaps procedurais, chamar `IsoTilemapBoundary.BuildBoundary()` após a geração do tilemap.

---

## 21. Conclusão

O módulo **IsoSorting** implementa um sistema completo e sofisticado de sorting manual para ambiente isométrico com múltiplos andares. A arquitetura é sólida nas seguintes decisões:

- **Separação estático/móvel** com cache de dependências estáticas — excelente para performance.
- **Topological sort por DFS** — robusto para relações de profundidade não transitivias.
- **Três tipos de footprint** (Point, Line, Polyline) — cobre a grande maioria dos casos de uso isométricos.
- **`IsoEntityHeight` como ponto central de altura** — desacoplado por evento, extensível por novos inscritores.
- **Auto-criação do Manager** — elimina setup manual frágil.
- **`IsoTilemapBoundary`** — ferramenta inteligente que gera footprints complexos sem trabalho manual.

Os pontos que merecem atenção antes de uma build de produção são:

1. **Remover o log de debug da Tree** em `BuildMovingDependencies`.
2. **Definir uso ou remover** `HEIGHT_BAND` e `PIXELS_PER_UNIT`.
3. **Implementar ou documentar** a estratégia para limpeza de listas estáticas entre cenas.
4. **Avaliar implementação de detecção de ciclos** para debug mais robusto em fases de desenvolvimento.

O sistema está preparado para suportar o desenvolvimento completo do jogo com extensões incrementais conforme o level design evoluir.

---

*Documentação gerada com base na análise dos scripts: `Bounds2D.cs`, `IsoEntityHeight.cs`, `IsoSortable.cs`, `IsoSortingManager.cs`, `IsoTilemapBoundary.cs`, `StairExitZone.cs`, `StairZone.cs` — Projeto Mylena.*