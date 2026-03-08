---
layout: default
title: PLAYERCONTROLLERS / INPUTS
permalink: /player-controllers-inputs/
---

# PLAYERCONTROLLERS / INPUTS

**Versão:** 1.0  
**Status:** Em documentação  
**Projeto:** Mylena (Jogo Isométrico — Unity)  
**Módulo:** PlayerControllers / Inputs  
**Autor da documentação:** Gerado a partir da análise de código-fonte  

---

## ÍNDICE

1. [Visão Geral do Módulo](#1-visão-geral-do-módulo)  
2. [Objetivo](#2-objetivo)  
3. [Arquitetura do Sistema](#3-arquitetura-do-sistema)  
4. [Componentes Principais](#4-componentes-principais)  
5. [Estrutura de Classes](#5-estrutura-de-classes)  
6. [Fluxo de Execução](#6-fluxo-de-execução)  
7. [Integração entre Scripts](#7-integração-entre-scripts)  
8. [Campos, Propriedades e Métodos — Detalhamento por Classe](#8-campos-propriedades-e-métodos--detalhamento-por-classe)  
   - 8.1 [InputController](#81-inputcontroller)  
   - 8.2 [MovementController](#82-movementcontroller)  
   - 8.3 [AnimationController](#83-animationcontroller)  
   - 8.4 [CharacterController](#84-charactercontroller)  
   - 8.5 [ClimbController](#85-climbcontroller)  
   - 8.6 [PhysicsLayerController](#86-physicslayercontroller)  
9. [Enum: IsoDirection8](#9-enum-isodirection8)  
10. [Regras de Negócio](#10-regras-de-negócio)  
11. [Fluxos Importantes](#11-fluxos-importantes)  
    - 11.1 [Fluxo de Movimentação Normal](#111-fluxo-de-movimentação-normal)  
    - 11.2 [Fluxo de Climb](#112-fluxo-de-climb)  
    - 11.3 [Fluxo de Mudança de Altura e Camada Física](#113-fluxo-de-mudança-de-altura-e-camada-física)  
12. [Dependências e Relações com Outros Módulos](#12-dependências-e-relações-com-outros-módulos)  
13. [Setup na Cena / Inspector](#13-setup-na-cena--inspector)  
14. [Boas Práticas e Manutenção](#14-boas-práticas-e-manutenção)  
15. [Pontos de Atenção / Troubleshooting](#15-pontos-de-atenção--troubleshooting)  
16. [Conclusão](#16-conclusão)  

---

## 1. Visão Geral do Módulo

O módulo **PlayerControllers / Inputs** é responsável por toda a lógica de controle do personagem jogável **Mylena**. Ele cobre a cadeia completa desde a **captura do input do jogador** até a **execução física do movimento**, passando por **animação direcional isométrica**, **sistema de climb com arco visual**, **controle de vida e stats** e **gerenciamento de camadas de física por altura**.

Os seis scripts que compõem este módulo vivem diretamente no objeto `Mylena` na hierarquia da cena e se comunicam de forma direta via referências serializadas ou via `GetComponent`. Não há uso de eventos desacoplados entre eles, exceto pelo `PhysicsLayerController`, que se inscreve no evento `OnHeightChanged` do componente `IsoEntityHeight` (módulo IsoSorting).

Todos os scripts residem no mesmo objeto raiz (`Mylena`) conforme a hierarquia de cena capturada:

```
▼ Mylena
  # AnimationController
  # CharacterController
  # ClimbController
  # InputController
  # MovementController
  # PhysicsLayerController
```

---

## 2. Objetivo

| Objetivo | Descrição |
|---|---|
| Captura de Input | Receber input do jogador via Unity Input System e disponibilizá-lo de forma centralizada |
| Movimentação | Converter o input em velocidade física aplicada ao `Rigidbody2D` |
| Animação Direcional | Calcular e aplicar direção isométrica de 8 eixos ao `Animator` |
| Gerenciamento de Vida | Armazenar stats do personagem e processar dano, cura e morte |
| Climb | Executar transição suave entre alturas com arco visual e animação dedicada |
| Camadas Físicas | Garantir que o personagem colida corretamente com objetos da sua altura atual |

---

## 3. Arquitetura do Sistema

### Diagrama Textual de Dependências

```
┌─────────────────────────────────────────────────────────────────┐
│                        OBJETO: Mylena                           │
│                                                                 │
│  ┌──────────────────┐                                           │
│  │  InputController │  ← Unity Input System (New Input System)  │
│  │  (fonte de input)│                                           │
│  └────────┬─────────┘                                           │
│           │ MoveInput / JumpPressed                             │
│           ▼                                                     │
│  ┌──────────────────────────────────────────────┐               │
│  │           MovementController                 │               │
│  │  (lê input, normaliza diagonal, move RB2D)   │               │
│  │  depende de: InputController                 │               │
│  │              CharacterController (RunSpeed)   │               │
│  │              AnimationController (SetMovement)│               │
│  │              Rigidbody2D                      │               │
│  └──────────────────────────────────────────────┘               │
│                                                                 │
│  ┌──────────────────────────────────────────────┐               │
│  │          AnimationController                 │               │
│  │  (calcula IsoDirection8, drive Animator)     │               │
│  └──────────────────────────────────────────────┘               │
│                                                                 │
│  ┌──────────────────────────────────────────────┐               │
│  │          CharacterController                 │               │
│  │  (stats: vida, velocidade, invulnerabilidade)│               │
│  └──────────────────────────────────────────────┘               │
│                                                                 │
│  ┌──────────────────────────────────────────────┐               │
│  │           ClimbController                    │               │
│  │  (coroutine de climb com arco, delay,        │               │
│  │   muda altura via IsoEntityHeight)            │               │
│  │  depende de: MovementController              │               │
│  │              AnimationController             │               │
│  │              IsoEntityHeight  ◄── IsoSorting │               │
│  └──────────────────────────────────────────────┘               │
│                                                                 │
│  ┌──────────────────────────────────────────────┐               │
│  │       PhysicsLayerController                 │               │
│  │  (escuta OnHeightChanged do IsoEntityHeight, │               │
│  │   altera layer de todos os objetos filhos)   │               │
│  │  depende de: IsoEntityHeight  ◄── IsoSorting │               │
│  └──────────────────────────────────────────────┘               │
└─────────────────────────────────────────────────────────────────┘

  EXTERNO: IsoEntityHeight (módulo IsoSorting)
           Rigidbody2D (componente Unity no filho do objeto)
           Animator (componente Unity no filho do objeto)
           Unity New Input System (PlayerInput)
```

---

## 4. Componentes Principais

| Script | Tipo | Responsabilidade Principal |
|---|---|---|
| `InputController` | `MonoBehaviour` | Captura e expõe input do jogador via New Input System |
| `MovementController` | `MonoBehaviour` | Processa input e aplica movimento físico ao `Rigidbody2D` |
| `AnimationController` | `MonoBehaviour` | Controla parâmetros do `Animator` com direção isométrica de 8 eixos |
| `CharacterController` | `MonoBehaviour` | Armazena e gerencia stats do personagem (vida, velocidade) |
| `ClimbController` | `MonoBehaviour` | Executa transição de altura com arco visual e animação dedicada |
| `PhysicsLayerController` | `MonoBehaviour` | Sincroniza a camada física do personagem com sua altura lógica |

---

## 5. Estrutura de Classes

### Enum `IsoDirection8`

Definido dentro de `AnimationController.cs`. Representa as 8 direções cardeais e diagonais do plano isométrico.

```
S=0, SW=1, W=2, NW=3, N=4, NE=5, E=6, SE=7
```

### Dependências por `[RequireComponent]`

```
ClimbController
  └── [RequireComponent] MovementController
  └── [RequireComponent] IsoEntityHeight

InputController
  └── [RequireComponent] PlayerInput
```

Todas as outras referências são obtidas via `GetComponent` ou injeção serializada no Inspector.

---

## 6. Fluxo de Execução

### Ordem de execução por fase do Unity

```
Awake
  │
  ├─ InputController.Awake()       → obtém PlayerInput, resolve Actions
  ├─ MovementController.Awake()    → resolve referências (input, character, anim, rb)
  ├─ AnimationController.Awake()   → resolve Animator, configura estado inicial
  ├─ CharacterController.Awake()   → valida vida, resolve AnimationController
  ├─ ClimbController.Awake()       → resolve movement, anim, heightEntity, rb
  └─ PhysicsLayerController.Awake()→ coleta filhos, inscreve em OnHeightChanged

OnEnable
  └─ InputController.OnEnable()    → inscreve callbacks de moveAction e jumpAction

Update (a cada frame)
  └─ MovementController.Update()
       ├─ lê input.MoveInput
       ├─ normaliza diagonal se necessário
       └─ chama anim.SetMovement(move)

FixedUpdate (taxa física)
  └─ MovementController.FixedUpdate()
       └─ aplica rb.linearVelocity = move * character.RunSpeed

OnDisable
  └─ InputController.OnDisable()   → desincreve callbacks

OnDestroy
  └─ PhysicsLayerController.OnDestroy() → desincreve OnHeightChanged
```

---

## 7. Integração entre Scripts

### Tabela de Comunicação Direta

| Emissor | Receptor | Mecanismo | Dado Transmitido |
|---|---|---|---|
| `InputController` | `MovementController` | Leitura de propriedade | `MoveInput` (Vector2) |
| `MovementController` | `AnimationController` | Chamada de método | `SetMovement(Vector2)` |
| `MovementController` | `CharacterController` | Leitura de propriedade | `RunSpeed` |
| `MovementController` | `Rigidbody2D` | Atribuição direta | `linearVelocity` |
| `ClimbController` | `MovementController` | Chamada de método | `SetClimbLock(bool)` |
| `ClimbController` | `AnimationController` | Chamada de método | `SetClimb(bool, Vector2, IsoDirection8)` |
| `ClimbController` | `IsoEntityHeight` | Chamada de método | `Ascend()` / `Descend()` |
| `IsoEntityHeight` | `PhysicsLayerController` | Evento C# | `OnHeightChanged(int, int)` |

### Chamador Externo

O `ClimbController` é iniciado por um objeto externo (presumivelmente um `ClimbTrigger` ou `StairZone` — módulo IsoSorting), que invoca diretamente:

```csharp
climbController.StartClimb(destination, ascending, climbDirection, animDir);
```

> **Observação:** O script que aciona `StartClimb` não está presente neste módulo. A invocação é inferida pelo comentário `// Chamado pelo ClimbTrigger.` presente na documentação XML do método.

---

## 8. Campos, Propriedades e Métodos — Detalhamento por Classe

---

### 8.1 `InputController`

**Tipo:** `MonoBehaviour`  
**Requer:** `PlayerInput`  
**Responsabilidade:** Captura e expõe os inputs do jogador. Atua como camada de abstração entre o Unity Input System e o resto do sistema de controle.

#### Campos Privados

| Campo | Tipo | Descrição |
|---|---|---|
| `playerInput` | `PlayerInput` | Referência ao componente PlayerInput do objeto |
| `moveAction` | `InputAction` | Ação de movimento ("Move") do mapa de ações |
| `jumpAction` | `InputAction` | Ação de pulo ("Jump") do mapa de ações |
| `_jumpTimestamp` | `float` | Marca o momento em que o jump foi pressionado; `-1f` indica inativo |
| `JUMP_BUFFER_TIME` | `const float` | Janela de buffer do jump: `0.1f` segundos |

#### Propriedades Públicas

| Propriedade | Tipo | Descrição |
|---|---|---|
| `MoveInput` | `Vector2` | Input de movimento atual (lido pelo MovementController) |
| `JumpPressed` | `bool` | `true` se o jump foi pressionado dentro da janela de buffer |
| `DebugMode` | `bool` | Campo serializado público, sem uso explícito de lógica no script atual |

#### Métodos

| Método | Retorno | Descrição |
|---|---|---|
| `ConsumeJump()` | `bool` | Verifica e invalida o jump buffered; retorna `true` se havia jump válido |
| `OnMove(ctx)` | `void` | Callback interno; atualiza `MoveInput` via `ctx.ReadValue<Vector2>()` |
| `OnJump(ctx)` | `void` | Callback interno; registra o timestamp atual em `_jumpTimestamp` |

#### Mecanismo de Jump Buffer

O sistema implementa um **jump buffer** simples: ao pressionar Jump, o timestamp é salvo. A propriedade `JumpPressed` retorna `true` enquanto `Time.time - _jumpTimestamp <= 0.1f`. `ConsumeJump()` invalida o buffer imediatamente ao ser chamado, evitando duplo consumo.

> **Observação:** O jump buffer está implementado e disponível, mas não há evidência nos scripts deste módulo de quem o consome. Presumivelmente, um sistema de ação/pulo externo (ainda não enviado) chamará `ConsumeJump()`.

#### Riscos e Cuidados

- O campo `DebugMode` está serializado mas sem uso — pode ser um resquício de desenvolvimento ou ponto de expansão futuro.
- O mapa de Input precisa conter obrigatoriamente as actions `"Move"` e `"Jump"` com os nomes exatos, caso contrário o `Awake` lançará `NullReferenceException`.

---

### 8.2 `MovementController`

**Tipo:** `MonoBehaviour`  
**Responsabilidade:** Lê o input de movimento, aplica normalização diagonal e executa a movimentação física via `Rigidbody2D`. É o controlador central do loop de movimento.

#### Campos Serializados

| Campo | Tipo | Descrição |
|---|---|---|
| `input` | `InputController` | Fonte dos inputs de direção |
| `character` | `CharacterController` | Fonte do `RunSpeed` |
| `anim` | `AnimationController` | Receptor das notificações de movimento para animação |
| `rb` | `Rigidbody2D` | Componente físico onde a velocidade é aplicada |
| `normalizeDiagonal` | `bool` | Quando `true`, normaliza o vetor de movimento se `sqrMagnitude > 1` |

#### Campos Privados

| Campo | Tipo | Descrição |
|---|---|---|
| `move` | `Vector2` | Vetor de movimento calculado no `Update`, aplicado no `FixedUpdate` |
| `_climbLocked` | `bool` | Quando `true`, todo movimento é bloqueado |

#### Métodos

| Método | Retorno | Descrição |
|---|---|---|
| `SetClimbLock(bool)` | `void` | Bloqueia/desbloqueia o movimento; zera velocidade imediatamente ao travar |
| `Update()` | `void` | Lê input, normaliza diagonal, notifica `AnimationController` |
| `FixedUpdate()` | `void` | Aplica `rb.linearVelocity = move * RunSpeed` |

#### Fluxo Interno

```
Update()
  if _climbLocked → return
  move = input.MoveInput
  if normalizeDiagonal && sqrMagnitude > 1 → move = move.normalized
  anim.SetMovement(move)

FixedUpdate()
  if _climbLocked || rb == null || character == null → return
  rb.linearVelocity = move * character.RunSpeed
```

#### Normalização Diagonal

A flag `normalizeDiagonal` resolve um problema clássico de input analógico: sem normalização, mover-se na diagonal resulta em velocidade maior (~1.41x) do que mover-se em linha reta. Com `normalizeDiagonal = true`, o vetor é normalizado quando ultrapassa magnitude 1, garantindo velocidade uniforme em todas as direções.

#### Riscos e Cuidados

- O `Rigidbody2D` é buscado com `GetComponentInChildren(true)`, o que implica que ele está em um **filho** do objeto `Mylena`, não no próprio objeto raiz.
- O campo `anim` também usa `GetComponent` no próprio objeto, mas o `AnimationController` está no mesmo objeto raiz — válido.
- Se `character` for `null` no `FixedUpdate`, o personagem para completamente. A lógica de fallback não atribui um valor padrão de velocidade.

---

### 8.3 `AnimationController`

**Tipo:** `MonoBehaviour`  
**Responsabilidade:** Traduz o vetor de movimento e estados do jogo (moving, climbing) em parâmetros do `Animator`, implementando direção isométrica de 8 eixos.

#### Campos Serializados

| Campo | Tipo | Descrição |
|---|---|---|
| `anim` | `Animator` | Referência ao componente Animator (pode ser um filho) |
| `currentDir` | `IsoDirection8` | Direção atual (somente leitura no Inspector — debug) |
| `isMoving` | `bool` | Estado de movimento atual (somente leitura no Inspector — debug) |
| `lastDir` | `Vector2` | Última direção de movimento (persiste quando parado) |

#### Hashes de Parâmetros do Animator

| Hash | Nome no Animator | Tipo | Uso |
|---|---|---|---|
| `DIR` | `"DIR"` | Comentado | Sem uso ativo (linha comentada) |
| `DIRX` | `"DIRX"` | Float | Componente X da direção (usado no climb) |
| `DIRY` | `"DIRY"` | Float | Componente Y da direção (usado no climb) |
| `MOVING` | `"MOVING"` | Bool | Indica se o personagem está em movimento |
| `MOVEX` | `"MoveX"` | Float | Componente X da direção (via SetDirection) |
| `MOVEY` | `"MoveY"` | Float | Componente Y da direção (via SetDirection) |
| `CLIMB` | `"CLIMB"` | Bool | Indica se o personagem está em modo climb |

> **Observação:** O parâmetro `DIR` (inteiro com valor do enum) está comentado na lógica de `SetMovement`. O sistema atual usa `MoveX`/`MoveY` como blendtree 2D ao invés de um índice inteiro. Isso é uma decisão arquitetural que favorece blend trees do tipo `2D Simple Directional` ou `2D Freeform`.

#### Métodos

| Método | Retorno | Descrição |
|---|---|---|
| `SetMovement(Vector2)` | `void` | Atualiza `MOVING`, `MoveX`, `MoveY` e persiste `lastDir` ao parar |
| `SetClimb(bool, Vector2, IsoDirection8)` | `void` | Ativa/desativa `CLIMB`, força direção da animação no início do climb |
| `GetDirection8(Vector2)` | `IsoDirection8` | Converte vetor normalizado em direção de 8 eixos via ângulo |
| `SetDirection(Vector2)` | `void` | (privado) Aplica `MoveX`/`MoveY` diretamente no Animator |

#### Mapeamento de Ângulos para IsoDirection8

```
  Ângulo (0° = direita, sentido anti-horário)

  337.5° – 22.5°  →  E   (Leste)
   22.5° – 67.5°  →  NE  (Nordeste)
   67.5° – 112.5° →  N   (Norte)
  112.5° – 157.5° →  NW  (Noroeste)
  157.5° – 202.5° →  W   (Oeste)
  202.5° – 247.5° →  SW  (Sudoeste)
  247.5° – 292.5° →  S   (Sul)
  292.5° – 337.5° →  SE  (Sudeste)
```

#### Comportamento ao Parar

Quando o vetor de input é zero (`sqrMagnitude <= 0.001f`), `SetMovement` chama `SetDirection(lastDir)`, mantendo `MoveX`/`MoveY` na última direção conhecida. Isso garante que a animação de idle seja exibida na direção correta, não revertendo para o padrão `Vector2.down`.

#### Riscos e Cuidados

- A linha `anim.SetFloat(DIR, ...)` está comentada. Se o `Animator` tiver transições condicionadas por `DIR`, elas não funcionarão.
- Os parâmetros `DIRX`/`DIRY` só são escritos durante climb. Em movimento normal, apenas `MoveX`/`MoveY` são utilizados. O Animator deve usar o par correto dependendo do estado.

---

### 8.4 `CharacterController`

**Tipo:** `MonoBehaviour`  
**Responsabilidade:** Centraliza os atributos e parâmetros do personagem. Gerencia vida, cura, dano e estado de morte. Expõe velocidades de movimento como propriedades somente-leitura.

#### Campos Serializados

| Campo | Tipo | Header | Descrição |
|---|---|---|---|
| `dashSpeed` | `float` | Movements Params | Velocidade de dash (exposta via propriedade) |
| `runSpeed` | `float` | Movements Params | Velocidade de corrida usada pelo `MovementController` |
| `maxLife` | `float` | Life Params | Vida máxima do personagem |
| `currentLife` | `float` | Life Params | Vida atual (inicializada em `maxLife`) |
| `invunerableTime` | `float` | Damage Params | Duração da janela de invulnerabilidade após dano |

#### Propriedades Públicas

| Propriedade | Tipo | Descrição |
|---|---|---|
| `MaxLife` | `float` | Getter e setter de `maxLife` |
| `RunSpeed` | `float` | Getter somente-leitura de `runSpeed` |
| `DashSpeed` | `float` | Getter somente-leitura de `dashSpeed` |
| `InvunerableTime` | `float` | Getter somente-leitura de `invunerableTime` |
| `CurrentLife` | `float` | Getter somente-leitura de `currentLife` |
| `isDead` | `bool` | Propriedade pública com set privado |

#### Métodos

| Método | Retorno | Descrição |
|---|---|---|
| `Heal(float)` | `void` | Adiciona vida; ignora se morto ou valor ≤ 0; clampeia em `maxLife` |
| `TakeDamage(float)` | `void` | Remove vida; ignora se morto ou valor ≤ 0; chama `Die()` se vida zerar |
| `Die()` | `void` | Define `isDead = true`; contém hook comentado para animação de morte |

#### Observações

- A referência a `AnimationController` (campo `anim`) é obtida em `Awake` mas **não é utilizada em nenhum método ativo**. O hook `anim?.PlayDeath()` está comentado, indicando que a integração de animação de morte ainda não foi implementada.
- Não há lógica de invulnerabilidade implementada neste script — `invunerableTime` é apenas armazenado como dado, sem temporizador ou flag. O sistema que aplica esse tempo presumivelmente existirá em um sistema de combate externo.

> **Observação:** As lógicas de invulnerabilidade e morte animada são inferidas como pontos de expansão futura com base nos hooks comentados e nos campos presentes.

---

### 8.5 `ClimbController`

**Tipo:** `MonoBehaviour`  
**Requer:** `MovementController`, `IsoEntityHeight`  
**Responsabilidade:** Executa a transição física e visual do personagem entre níveis de altura diferentes. Gerencia um arco parabólico de movimento, delay de antecipação, troca de altura lógica e cooldown anti-respawn.

#### Campos Serializados

| Campo | Tipo | Descrição |
|---|---|---|
| `climbDuration` | `float` | Duração total do arco de climb (padrão: `0.4f`) |
| `climbArcHeight` | `float` | Altura do arco visual durante o climb (padrão: `0.5f`) |
| `climbDelay` | `float` | Delay de antecipação antes do movimento iniciar (padrão: `0.2f`) |
| `timeHeight` | `float` | Fração do tempo total em que a altura lógica muda (padrão: `0.5f`) |
| `climbCooldown` | `float` | Tempo mínimo entre dois climbs consecutivos (padrão: `1f`) |

#### Campos Privados

| Campo | Tipo | Descrição |
|---|---|---|
| `_lastClimbEndTime` | `float` | Timestamp do último fim de climb; `-99f` no início |

#### Propriedades Públicas

| Propriedade | Tipo | Descrição |
|---|---|---|
| `IsClimbing` | `bool` | `true` durante execução do climb |
| `IsOnCooldown` | `bool` | `true` se `Time.time - _lastClimbEndTime < climbCooldown` |
| `CanClimb` | `bool` | `true` somente se `!IsClimbing && !IsOnCooldown` |

#### Métodos

| Método | Retorno | Descrição |
|---|---|---|
| `StartClimb(Vector2, bool, Vector2, IsoDirection8)` | `void` | Ponto de entrada público; inicia coroutine se não estiver em climb |
| `ClimbRoutine(...)` | `IEnumerator` | Coroutine que executa todo o arco visual, delay e mudança de altura |

#### Fluxo Interno da Coroutine `ClimbRoutine`

```
1. IsClimbing = true
2. movement.SetClimbLock(true)       → trava movimentação livre
3. rb.linearVelocity = Vector2.zero  → para qualquer inércia residual
4. anim.SetClimb(true, dir, animDir) → ativa animação de climb com direção
5. yield WaitForSeconds(climbDelay)  → delay de antecipação (0.2s)
6. Loop interpolação (elapsed < climbDuration):
   ├─ t = elapsed / climbDuration (0→1)
   ├─ arc = Sin(t * π) * climbArcHeight    → curva senoidal parabólica
   ├─ flatPos = Lerp(startPos, destination, t)
   ├─ transform.position = (flatPos.x, flatPos.y + arc, 0)
   └─ if t >= timeHeight && !heightChanged:
        ├─ IsoEntityHeight.Ascend() ou Descend()  → troca altura lógica
        └─ heightChanged = true
7. transform.position = destination  → snap final para evitar drift
8. anim.SetClimb(false, ...)         → desativa animação
9. movement.SetClimbLock(false)      → libera movimentação
10. _lastClimbEndTime = Time.time    → inicia cooldown
11. IsClimbing = false
```

#### Arco Visual de Climb

O arco é gerado por uma curva **senoidal** (`Sin(t * π)`), que produz um arco suave que começa e termina em zero. A altura máxima do arco ocorre em `t = 0.5` (meio do percurso).

```
Altura do arco ao longo do tempo:

  0.5 │    ╭──────╮
      │  ╭╯        ╰╮
  0.0 │──╯            ╰──
      └──────────────────
         t=0   t=0.5  t=1
```

#### Mudança de Altura no Ponto Correto

A mudança de altura lógica (via `IsoEntityHeight`) ocorre quando `t >= timeHeight` (padrão: `0.5`), exatamente no pico do arco. Isso significa que o sorting visual e a layer física mudam no momento em que o personagem visualmente está mais "acima", o que minimiza artefatos de sobreposição visual.

#### Riscos e Cuidados

- **Dependência com IsoEntityHeight:** O `ClimbController` cruza a fronteira de módulos ao chamar `_heightEntity.Ascend()` / `_heightEntity.Descend()`. Qualquer refatoração em `IsoEntityHeight` impacta diretamente este script.
- **Snap final:** O `transform.position = destination` ao final garante posicionamento exato, evitando drift de ponto flutuante acumulado durante o Lerp.
- **Cooldown:** Sem o cooldown, zonas de trigger sobrepostas poderiam re-ativar o climb imediatamente. O valor padrão de `1f` segundo é generoso — pode ser ajustado se o level design precisar de transições mais rápidas.
- O `rb` referenciado em `ClimbController` é obtido com `GetComponentInChildren<Rigidbody2D>()`, confirmando que o `Rigidbody2D` está em um filho do objeto raiz.

---

### 8.6 `PhysicsLayerController`

**Tipo:** `MonoBehaviour`  
**Responsabilidade:** Sincroniza automaticamente a camada de física (Physics Layer) de todos os objetos filhos do personagem com a altura lógica atual. Garante que colisões entre personagens e objetos de cena ocorram apenas dentro do mesmo nível de altura.

#### Campos Serializados

| Campo | Tipo | Descrição |
|---|---|---|
| `managedObjects` | `List<GameObject>` | Lista de objetos que terão a layer alterada (preenchida automaticamente no Awake) |
| `layerPrefix` | `string` | Prefixo da nomenclatura de layers. Padrão: `"Entities"` |

#### Convenção de Layers

```
Entities_H0  → objetos no nível de altura 0 (chão)
Entities_H1  → objetos no nível de altura 1 (primeiro andar)
Entities_H2  → objetos no nível de altura 2 (segundo andar)
...e assim por diante
```

As layers devem ser criadas manualmente em `Edit → Project Settings → Tags and Layers`.

#### Métodos

| Método | Retorno | Descrição |
|---|---|---|
| `HandleHeightChanged(int, int)` | `void` | Callback do evento; calcula nome da layer e aplica a todos os managed objects |
| `GetAllChildren(GameObject)` | `List<GameObject>` | Coleta recursivamente todos os filhos + o próprio pai usando `GetComponentsInChildren<Transform>` |

#### Fluxo de Evento

```
IsoEntityHeight.OnHeightChanged (evento C#)
        │
        ▼
PhysicsLayerController.HandleHeightChanged(oldHeight, newHeight)
        │
        ├─ layerName = $"{layerPrefix}_H{newHeight}"
        ├─ layerIndex = LayerMask.NameToLayer(layerName)
        └─ foreach obj in managedObjects → obj.layer = layerIndex
```

#### Coleta de Objetos Gerenciados

O `GetAllChildren` usa `GetComponentsInChildren<Transform>(true)` (incluindo objetos desativados), filtra o próprio pai, e **então re-adiciona o pai à lista**. Ou seja, todos os objetos da hierarquia — incluindo o próprio `Mylena` — terão sua layer alterada.

#### Riscos e Cuidados

- **Layers obrigatórias:** Se a layer `Entities_H{n}` não existir no projeto, um `LogError` é emitido e nenhuma alteração ocorre para aquela transição. Isso pode causar bugs de colisão silenciosos.
- **Layer de objetos especiais:** Como o `GetAllChildren` coleta **toda** a hierarquia filho (incluindo, por exemplo, `AnimationController` ou objetos de VFX), todos passarão a ter a mesma layer. Se algum filho precisar de uma layer diferente (ex: um objeto de UI ou trigger especial), ele seria sobrescrito incorretamente. Avaliar se uma lista de exclusão seria necessária.
- **Inscrição/desincrição segura:** O script corretamente desincreve do evento em `OnDestroy`, evitando referências pendentes.

---

## 9. Enum `IsoDirection8`

**Definido em:** `AnimationController.cs`  
**Visibilidade:** `public`

O enum representa as 8 direções possíveis no espaço isométrico. É utilizado internamente pelo `AnimationController` e passado como parâmetro pelo `ClimbController` para garantir que a animação de climb ocorra na direção correta.

| Valor | Int | Direção |
|---|---|---|
| `S` | 0 | Sul |
| `SW` | 1 | Sudoeste |
| `W` | 2 | Oeste |
| `NW` | 3 | Noroeste |
| `N` | 4 | Norte |
| `NE` | 5 | Nordeste |
| `E` | 6 | Leste |
| `SE` | 7 | Sudeste |

> **Observação de Design:** Posicionar este enum dentro do arquivo `AnimationController.cs` funciona, mas pode gerar dificuldades de localização em projetos maiores. Mover para um arquivo próprio `IsoDirection8.cs` em uma pasta `Enums/` seria uma prática mais escalável.

---

## 10. Regras de Negócio

1. **Movimento bloqueado durante climb:** Qualquer input de movimento é ignorado enquanto `_climbLocked = true`. O `MovementController` respeita esse estado via `SetClimbLock`.

2. **Cooldown de climb:** Após qualquer climb, um cooldown de `climbCooldown` segundos impede que o climb seja ativado novamente. Isso evita loops de trigger em escadas ou zonas sobrepostas.

3. **Persist de direção ao parar:** O `AnimationController` mantém a última direção de movimento quando o personagem para, garantindo idle direcional correto.

4. **Altura lógica muda no pico do arco:** A mudança de altura via `IsoEntityHeight` ocorre em `t >= timeHeight` (padrão: metade do percurso), sincronizando a troca de sorting e layer com o momento visual mais adequado.

5. **Layers físicas por altura:** Cada altura lógica corresponde a uma Physics Layer. Colisões entre entidades em alturas diferentes são controladas pela Matrix de Colisão do Unity (`Physics2D Settings`).

6. **Vida não vai abaixo de zero nem acima do máximo:** `TakeDamage` e `Heal` usam `Mathf.Max`/`Mathf.Min` para garantir isso.

7. **Jump buffer de 100ms:** O input de pulo permanece válido por `0.1f` segundos após ser pressionado, tolerando leve dessincronização entre o input e a janela de ação disponível.

---

## 11. Fluxos Importantes

### 11.1 Fluxo de Movimentação Normal

```
[Jogador pressiona WASD ou analógico]
        │
        ▼
InputController.OnMove(ctx)
  → MoveInput = ctx.ReadValue<Vector2>()
        │
        ▼
MovementController.Update()
  → move = input.MoveInput
  → if normalizeDiagonal → move = move.normalized
  → anim.SetMovement(move)
        │
        ├──────────────────────────────────────────┐
        ▼                                          ▼
AnimationController.SetMovement(move)     MovementController.FixedUpdate()
  → isMoving = move.sqrMagnitude > 0.001f   → rb.linearVelocity = move * RunSpeed
  → if moving: atualiza lastDir, currentDir
  → SetDirection(lastDir)
  → anim.SetBool(MOVING, isMoving)
```

### 11.2 Fluxo de Climb

```
[Personagem entra em trigger de escada / ClimbTrigger]
        │
        ▼
ClimbTrigger (objeto externo)
  → climbController.StartClimb(destination, ascending, dir, animDir)
        │
        ▼
ClimbController.ClimbRoutine() [Coroutine]
  1. Trava MovementController (SetClimbLock = true)
  2. Para Rigidbody2D
  3. Ativa animação de climb (anim.SetClimb(true, ...))
  4. Aguarda climbDelay (0.2s)
  5. Loop: move em arco senoidal de startPos → destination
  6. Em t >= 0.5: IsoEntityHeight.Ascend() ou Descend()
        │
        ├──── Dispara OnHeightChanged ────────────────────────────┐
        │                                                          ▼
        │                                         PhysicsLayerController
        │                                           → atualiza layer de todos os filhos
        │
  7. Snap para destination exato
  8. Desativa animação de climb
  9. Libera MovementController (SetClimbLock = false)
  10. Registra timestamp de fim (inicia cooldown)
```

### 11.3 Fluxo de Mudança de Altura e Camada Física

```
IsoEntityHeight.Ascend() ou Descend()
  → dispara OnHeightChanged(oldHeight, newHeight)
        │
        ▼
PhysicsLayerController.HandleHeightChanged(old, new)
  → layerName = "Entities_H{new}"
  → layerIndex = LayerMask.NameToLayer(layerName)
  → foreach obj in managedObjects:
       obj.layer = layerIndex
```

---

## 12. Dependências e Relações com Outros Módulos

| Dependência | Módulo | Como é usada |
|---|---|---|
| `IsoEntityHeight` | IsoSorting | `ClimbController` chama `Ascend()`/`Descend()`; `PhysicsLayerController` escuta `OnHeightChanged` |
| `Rigidbody2D` | Unity Physics2D | Presente em filho do objeto `Mylena`; manipulado por `MovementController` e `ClimbController` |
| `Animator` | Unity Animation | Presente em filho do objeto `Mylena`; manipulado pelo `AnimationController` |
| `PlayerInput` | Unity New Input System | Presente no objeto `Mylena`; gerenciado pelo `InputController` |

---

## 13. Setup na Cena / Inspector

### Hierarquia Esperada

```
Mylena (objeto raiz)
  ├── [Componentes no raiz]:
  │     AnimationController
  │     CharacterController
  │     ClimbController
  │     InputController (+ PlayerInput)
  │     MovementController
  │     PhysicsLayerController
  │     IsoEntityHeight   ← módulo IsoSorting
  │
  └── [Filho com física]:
        Rigidbody2D
        Collider2D (corpo)
        [outros triggers/colliders]
```

### Checklist de Configuração

| Item | Onde configurar | Observação |
|---|---|---|
| Input Action Asset | `PlayerInput` → Actions | Deve conter actions `"Move"` e `"Jump"` |
| `layerPrefix` | `PhysicsLayerController` | Padrão: `"Entities"` |
| Layers no projeto | `Edit → Project Settings → Tags and Layers` | Criar `Entities_H0`, `Entities_H1`, etc. |
| Physics Matrix | `Edit → Project Settings → Physics 2D` | Desabilitar colisão entre layers de alturas diferentes |
| `normalizeDiagonal` | `MovementController` | Ativar para evitar velocidade maior na diagonal |
| `climbDuration`, `climbArcHeight`, `climbDelay` | `ClimbController` | Tuning visual do arco de climb |
| `runSpeed`, `dashSpeed` | `CharacterController` | Valores base de movimentação |

---

## 14. Boas Práticas e Manutenção

- **Separação clara de responsabilidades:** Cada script tem uma responsabilidade bem definida e não há sobreposição funcional. O `MovementController` move, o `AnimationController` anima, o `InputController` captura input. Esta é uma boa decisão arquitetural.

- **Uso correto de FixedUpdate para física:** A velocidade do `Rigidbody2D` é aplicada exclusivamente no `FixedUpdate`, separado da leitura de input no `Update`. Esta é a forma correta de trabalhar com física no Unity.

- **Cooldown via timestamp:** O sistema de cooldown em `ClimbController` usa `Time.time - _lastClimbEndTime`, uma abordagem mais robusta que decrementar um timer em `Update`, pois não depende de execução contínua.

- **Desincrição de eventos:** `PhysicsLayerController` desinscreve corretamente de `OnHeightChanged` em `OnDestroy`, evitando memory leaks ou NullReferenceExceptions em cenas com destroy de objetos.

- **Hashes pré-computados no Animator:** O `AnimationController` usa `Animator.StringToHash` para todos os parâmetros, evitando lookups por string a cada frame. Boa prática de performance.

---

## 15. Pontos de Atenção / Troubleshooting

| Problema | Causa Provável | Solução |
|---|---|---|
| Personagem não se move | `rb` nulo; `character` nulo; `_climbLocked = true` | Verificar atribuições no Inspector; confirmar que Rigidbody2D está em filho |
| Animação não atualiza | `Animator` não encontrado no `AnimationController` | Confirmar que o `Animator` está em um filho do objeto `Mylena` |
| Climb não inicia | `CanClimb = false` (IsClimbing ou IsOnCooldown) | Aguardar cooldown expirar; verificar se coroutine anterior travou |
| Layer não muda após climb | Layer `Entities_H{n}` não existe no projeto | Criar as layers em `Project Settings → Tags and Layers` |
| Input não responde | Action `"Move"` ou `"Jump"` com nome errado no InputActionAsset | Verificar nomes exatos das actions no Input Action Asset |
| Movimento diagonal mais rápido | `normalizeDiagonal = false` | Ativar a flag no Inspector do `MovementController` |
| Personagem "congela" após climb | `SetClimbLock(false)` não foi chamado (crash na coroutine) | Envolver `ClimbRoutine` em try/finally para garantir desbloqueio |
| Objetos filhos com layer errada | `GetAllChildren` coleta toda hierarquia | Avaliar adição de lista de exclusão no `PhysicsLayerController` |

---

## 16. Conclusão

O módulo **PlayerControllers / Inputs** do projeto Mylena apresenta uma arquitetura bem estruturada, com separação clara de responsabilidades entre os seis componentes. A cadeia `InputController → MovementController → Rigidbody2D` é limpa e idiomática para Unity. O `ClimbController` resolve elegantemente a transição entre alturas com um arco senoidal suave e integração direta com o módulo de sorting isométrico.

Os principais pontos de expansão futura identificados são:

1. **Sistema de invulnerabilidade** — o campo `invunerableTime` está pronto em `CharacterController` mas sem implementação.
2. **Animação de morte** — o hook `anim?.PlayDeath()` está comentado, aguardando implementação.
3. **Consumidor do jump buffer** — `ConsumeJump()` está disponível mas sem consumidor visível neste módulo.
4. **Enum `IsoDirection8`** — candidato a arquivo próprio conforme o projeto crescer.

O módulo está pronto para servir como base sólida de manutenção e extensão.

---

*Documentação gerada com base na análise dos scripts: `InputController.cs`, `MovementController.cs`, `AnimationController.cs`, `CharacterController.cs`, `ClimbController.cs`, `PhysicsLayerController.cs` — Projeto Mylena.*