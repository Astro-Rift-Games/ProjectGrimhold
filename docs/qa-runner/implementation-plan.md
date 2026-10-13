# QA Runner autónomo de Project Grimhold

## Estado y propósito

**Estado:** propuesta de implementación pendiente de aprobación. Este documento no autoriza implementar, instalar dependencias, modificar arquitectura o Game Design, ejecutar builds/input/capturas, usar producción, publicar issues ni crear commits/worktrees.

**Baseline de la investigación:** rama `feat/qa-runner`, commit `dff293a2` para QA-001/QA-002, con QA-003 presente sin commit. Las referencias al estado actual son una fotografía de la investigación, no garantías sobre futuras builds. Antes de implementar, volver a comprobar código, branch, working tree, arquitectura y Game Design vigente.

**Revisión de orden (2026-10-12):** E0 se reduce a contrato mínimo y requisitos; el roadmap prioriza un primer recorrido real de punta a punta (E4) y divide la ejecución en una línea de cliente y otra de ambiente. E2 se pospone hasta antes de la primera ejecución sin supervisión, y E10 se divide en E10a (solo imagen) y E10b (con oráculos de estado). Los IDs existentes se conservan.

**Objetivo final:** recibir una feature concreta, preparar un ambiente reproducible, ejecutar una prueba sobre una build real, evaluar sus resultados y entregar evidencia de los fallos encontrados, sin intervención humana durante el recorrido soportado.

**Recomendación:** automatización determinista más observación confiable y una capa opcional de IA para interpretación, composición limitada de planes y triage. No construir inicialmente un bot general que juega mirando screenshots.

La dificultad principal es preparar condiciones reproducibles, definir resultados esperados y demostrar qué ocurrió. Enviar inputs es solamente una parte.

## Índice

1. [Alcance y fuentes](#1-alcance-y-fuentes)
2. [Estado actual](#2-estado-actual)
3. [Arquitectura objetivo](#3-arquitectura-objetivo)
4. [Contratos de ejecución y evidencia](#4-contratos-de-ejecución-y-evidencia)
5. [Roadmap incremental](#5-roadmap-incremental)
6. [Estrategia de validación](#6-estrategia-de-validación)
7. [IA y automatización convencional](#7-ia-y-automatización-convencional)
8. [Riesgos y decisiones](#8-riesgos-y-decisiones)
9. [Organización y costos](#9-organización-y-costos)
10. [Definición de terminado](#10-definición-de-terminado)
11. [Mejoras posteriores](#11-mejoras-posteriores)
12. [Inicio y continuidad](#12-inicio-y-continuidad)
13. [Referencias](#13-referencias)

## 1. Alcance y fuentes

### Alcance del proyecto

- Windows como plataforma inicial de ejecución gráfica.
- Builds reales identificadas y reproducibles; builds de QA instrumentadas cuando se apruebe.
- Autenticación con cuentas de QA y preparación/reset de fixtures.
- Interfaces, gameplay, Host/Client, concurrencia, extracción y persistencia.
- Aserciones funcionales, fallos de ambiente y reportes con evidencia.
- Selección de features y composición de escenarios dentro de capacidades comprobadas.
- Ejecución autónoma acotada, cancelación y cleanup.

### No objetivos iniciales

- Jugar de manera general a cualquier videojuego o a cualquier feature desconocida.
- Entrenar agentes mediante reinforcement learning.
- Sustituir pruebas EditMode/PlayMode/backend existentes por E2E costosas.
- Reescribir persistencia, networking o seguridad del juego como parte implícita del runner.
- Plataforma web, clúster, marketplace de plugins o framework general de automatización.
- Automatización sobre cuentas, partidas o bases de producción.
- Publicación automática de bugs sin autorización.
- Pruebas exhaustivas de balance, rendimiento, localización o todas las configuraciones de hardware.

### Autoridad por responsabilidad

| Responsabilidad | Fuente |
|---|---|
| Estado técnico actual | Código, configuración y assets de la rama/build evaluada. |
| Contrato técnico | Arquitectura aprobada bajo `Project Grimhold/Docs/Architecture/`. |
| Comportamiento esperado | Game Design vigente, consultado mediante `grimhold-docs`. |
| Alcance de una tarea existente | HacknPlan y su parent, cuando se implemente una tarea identificada. |
| Contexto histórico | Engram y conversaciones, exclusivamente como apoyo. |

Este documento no reemplaza esas fuentes. Ante desacuerdo, registrar el conflicto y resolverlo con el responsable antes de crear una expectativa de prueba. Los IDs E0–E19 son referencias internas del roadmap, no IDs de HacknPlan ni registros remotos creados.

Para una feature nueva: localizar sus documentos propietarios, leer las secciones pertinentes, verificar implementación y separar lo implementado, lo intencional y lo no resuelto.

## 2. Estado actual

### Capacidades existentes y límites

| Componente | Capacidad actual | Límite |
|---|---|---|
| QA-001 | `Popen`, executable-parent cwd, `shell=False`, supervisión monotónica, detección de salida prematura, cleanup y JSON único. | Supervisa el proceso raíz; no demuestra funcionamiento del juego ni propiedad de todo un árbol de procesos. |
| QA-002 | Una tecla permitida, ventana por PID/HWND, foco exacto, guards, release y captura PNG. | Input global; sin mouse/texto/escenarios. Acción enviada no equivale a acción interpretada. |
| QA-003, sin commit | Readiness Pillow opt-in sobre área cliente; coincidencias consecutivas y diagnóstico histórico. | Misma escala, límites de imagen/candidatos; no evalúa gameplay. Sin comprobación real del gate registrada. |
| Tests | Última verificación registrada: 104 pruebas pasando, 76 anteriores intactas y 28 nuevas. | No se ejecutaron nuevamente para elaborar el plan. Predominan imágenes sintéticas y Win32/GUI simulados; QA-001 también usa procesos stdlib reales. |
| Reportes | Acciones y evidencia; `functional_validation: not_performed`, `gameplay_validated: false`. | Sin contrato de escenarios o aserciones; no convertir el éxito de proceso en éxito funcional. |

QA-002 requiere foco manual. La autonomía futura necesitará una política nueva y explícita de adquisición de foco exclusivamente dentro de un worker aislado. No cambiar silenciosamente el comportamiento actual.

Las operaciones síncronas de captura, IO y Win32 no pueden interrumpirse mediante polling. Detectar una demora permite bloquear inputs posteriores, no garantizar un límite duro para la llamada en curso. Hard kill puede impedir cleanup; overlays pueden contaminar screenshots.

### Evidencia local de QA-002

- Reporte `qa-7cd50939ba2a440ea0a47c9817fa342f.json`: captura del menú de 976×579, fecha reportada `2026-10-12T05:48:23.754531Z`.
- PNG correspondiente: `qa-7cd50939ba2a440ea0a47c9817fa342f-evidence-d7b58fc46eac47c5aa8b04e07a312103.png`.
- Captura anterior `qa-07dcc9275642429784be056089506427-evidence-cb55a66b01ba4c4d8c8bce0d04e1011c.png`: ventana oscura durante carga.
- Archivos bajo `tools/qa-runner/results/`, locales e ignorados. No son dependencias de la suite automatizada ni deben versionarse completos.
- La referencia versionada `references/main-menu-iniciar.png` es un recorte RGB de 88×30, coordenadas `(128, 253, 216, 283)` de la captura del menú.
- SHA256 de referencia: `93540e972bb47d2c0736d139762683344f7b9dbbb55675c40a5a0a45bbbe8ee3`.

Estas evidencias muestran acciones y capturas de QA-002. No prueban QA-003 ni respuestas funcionales del gameplay. El benchmark offline del detector no equivale a rendimiento de captura/Win32/build.

### Backend y juego

- El login actual obtiene identidad, perfil, inventario y progresión, valida coherencia de revisiones e inicializa el contexto del personaje.
- `MainMenuController` tiene un gate de login al crear sala cuando no hay autenticación. No asumir un botón/flujo de entrada distinto sin inspeccionar la build.
- No se encontró recuperación automática de sesión (GD UI §4.1). El menú principal muestra INICIAR, OPCIONES, CREDITOS y SALIR (`Prefabs/UI/MainMenu.prefab`), pero solo INICIAR tiene acción (`MainMenuController.CreateRoom`); OPCIONES, CREDITOS y SALIR no tienen acción conectada y Join permanece oculto (GD UI §4.2). Registrar en E0 como conflicto; no implementarlo como parte del runner.
- Existe hidratación y hay adaptadores remotos. Las afirmaciones heredadas de persistencia exclusivamente en memoria no describen todos los campos actuales.
- `LocalPlayerPersistenceArchitecture.md` documenta límites específicos, incluidos los Off Hands frente al DTO remoto de seis slots. Una mutación de UI/local no prueba durabilidad.
- `backend/scripts/seed-test-accounts.js` prepara dos cuentas, pero no aporta reset integral ni guard suficiente contra bases no descartables. Evitar passwords por argv/defaults en el futuro harness.
- El juego utiliza Host/Client; no suponer dedicated server ni composición multi-instance existente.
- No se encontró un bridge de QA estructurado y general para estado confirmado; los logs ordinarios no son por sí mismos un oráculo funcional.

**Versiones verificadas en `New-Testing`:** Unity `6000.5.1f1`, Fusion `2.1.1 Stable 2177` según `build_info.txt`, Input System `1.19.0`, Test Framework `1.7.0`. No usar metadata heredada de `Fusion/package.json` como reemplazo de la versión instalada.

### Protección del working tree

Al crear este documento hay cambios previos de QA-003 y tres assets de Unity modificados por fuera de este trabajo. No revertirlos, mezclarlos ni tratarlos como cambios del runner documental. Revalidar working tree antes de comenzar cualquier etapa.

## 3. Arquitectura objetivo

```text
Feature solicitada
    → Resolver contrato y capacidades
    → Seleccionar/componer escenario validado
    → Preparar workers, cuentas y fixtures
    → Ejecutar acciones sobre builds reales
    → Observar UI + estado confirmado + backend
    → Evaluar aserciones independientes
    → Reporte, evidencias y hallazgos
    → Cleanup y verificación del ambiente
```

### Componentes mínimos

| Componente | Responsabilidad | No debe hacer |
|---|---|---|
| Orquestador Python | Plan finito, deadlines, procesos, workers, cancelación, resultados. | Decidir reglas de gameplay o ejecutar código generado arbitrario. |
| Control de cliente | Keyboard/mouse/texto con guards y ownership. | Robar foco en escritorios ajenos o interpretar acción enviada como éxito. |
| Observadores | Imágenes, Player logs, snapshots y lecturas backend. | Mutar el dominio o inventar observaciones faltantes. |
| Evaluador | Aserciones exactas/temporales y clasificación. | Pedir al modelo que decida un PASS sin evidencia contractual. |
| Catálogo | Features, variantes, requisitos, contratos y cobertura. | Afirmar cobertura para features desconocidas. |
| Reportador | Evidencia correlacionada, reproducción y deduplicación. | Publicar secretos o issues sin permiso. |

Mantener composición explícita y pocas interfaces en límites reales: control, observación y preparación del ambiente. No agregar buses globales, service locators ni jerarquías de plugins sin necesidad.

### Observación desde Unity: propuesta sujeta a aprobación

Un adaptador de solo lectura, acotado por feature y compilado fuera de producción:

- Build/run/client/participant IDs, versión del contrato y perfil.
- Tick/revisión y timestamp de adquisición; observación vencida se rechaza.
- Rol, autoridad y estado confirmado relevante.
- Estado de presentación necesario para comprobar la UI.
- Respuestas limitadas, sin exploración arbitraria de objetos ni evaluación de código.

Elegir canal local sencillo con aislamiento de usuario/proceso y credencial efímera por ejecución; validar build y participante al conectarse. No exponer un puerto o API administrativa abierta. La elección exacta del transporte se realiza en E8 y se documenta.

No ofrecer setters de resultados, teleports durante la prueba ni invocaciones directas que eludan el comportamiento evaluado. Si se aprueba un adaptador de input, debe ingresar por el pipeline normal:

```text
PlayerInputReader → FusionInputProvider → PlayerNetworkInput → simulación
```

Separar preparación de fixtures, ejecución de acciones y observación. Un hook de fixture no puede utilizarse como sustituto de la acción bajo prueba.

### Oráculos independientes

- **Esperado:** contrato de Game Design/arquitectura, parámetros de balance identificados y fixture conocido.
- **Observado:** UI, estado autoritativo confirmado y backend según el caso.
- No calcular esperado llamando al mismo método que se pretende validar.
- Para replicación, comparar vistas/estado del Host y Client con límites temporales definidos.
- Para durabilidad, leer backend y reautenticar después de reiniciar. UI y `ProfileCommitted` no bastan.
- Un oracle ausente produce BLOCKED/INCONCLUSIVE; nunca una inferencia favorable.

### Ubicaciones candidatas, no archivos ya implementados

- `tools/qa-runner/`: orquestación, escenarios, catálogo, evaluación y reporting.
- `tools/qa-runner/tests/`: pruebas unitarias y fixtures sintéticos pequeños.
- `docs/qa-runner/`: decisiones y guía de operación.
- `Project Grimhold/Assets/Scripts/QA/`: posible observación de QA, solamente tras aprobación y siguiendo reglas Unity.
- `backend/`: provisioning/reset para ambiente de QA, solamente tras aprobación del contrato y superficie.
- Resultados, logs completos y credenciales: fuera de archivos versionados, con retención definida.

No crear todas esas estructuras de antemano. Derivar los archivos mínimos al autorizar cada etapa.

## 4. Contratos de ejecución y evidencia

### Tres dimensiones separadas

| Dimensión | Ejemplos |
|---|---|
| Ejecución | Inicio, duración, crash, interrupción, timeout, cleanup. |
| Feature | PASS, FAIL, BLOCKED, INCONCLUSIVE. |
| Evidencia | Completa, parcial, no disponible o descartada. |

- **PASS:** todas las aserciones obligatorias tienen evidencia válida.
- **FAIL:** una expectativa definida fue contradicha por observación confiable.
- **BLOCKED:** falta contrato, fixture, capacidad, ambiente o precondición.
- **INCONCLUSIVE:** se ejecutó, pero la evidencia no permite resolver.

Un timeout no es automáticamente un bug del juego. Puede violar un requisito del juego, o deberse al runner/ambiente. Registrar la causa conocida y las incertidumbres.

Una falla de cleanup o de evidencia obligatoria impide un PASS limpio; conservar, de todos modos, la evaluación funcional parcial. Mantener compatibilidad explícita con los campos de QA-001/QA-002; introducir un esquema versionado sin reinterpretar reportes viejos.

### Información mínima de escenario

- ID y versión, feature y variantes.
- Fuente/versión del resultado esperado y limitaciones.
- Fixture, participantes, roles y ambiente.
- Pasos allowlisted, precondiciones y postcondiciones.
- Aserciones, fuente de observación, tolerancias y deadlines.
- Presupuesto de duración/acciones; iteraciones finitas.
- Evidencia obligatoria y tratamiento de secretos.
- Cleanup, retry policy y resultado ante interrupción.

JSON es la primera opción. No diseñar un lenguaje de scripting completo. Los ejemplos/esquemas definitivos se crean en E6 tras observar los primeros casos.

### Información mínima de reporte

- `schema_version`, `run_id`, request y escenario resuelto.
- Build ID/commit, configuración, versiones, resolución/DPI y workers.
- Contratos usados y su fecha/revisión; fuente congelada del esperado para reproducción.
- Fixture verificado antes de comenzar, roles e identidades sanitizadas.
- Timeline monotónica por worker; UTC para correlación, ticks/revisiones cuando correspondan.
- Paso intentado/completado, esperado/observado, primera divergencia.
- Resultados separados de ejecución, feature y evidencia.
- Paths relativos/hashes de artefactos aceptados, razones de descarte y datos obsoletos.
- Cleanup, retry attempts, cobertura y exclusiones.

Nunca almacenar password/token, Authorization headers o secretos del backend. Sanitizar antes de persistir y antes de enviar datos a servicios externos. Para pantallas de login, omitir o redactar regiones sensibles. Hashes y timestamps no convierten automáticamente un artefacto en confiable.

### Reporte de bugs

Clasificar como bug confirmado, anomalía candidata, fallo del runner, fallo del ambiente o falta de cobertura. Incluir pasos mínimos, esperado/observado, build, fixture, participantes, criterio y evidencia correlacionada.

La severidad es sugerida; prioridad y aceptación del bug son decisiones del equipo. La deduplicación inicial usa feature/criterio/tipo de divergencia y contexto de build; no depende solamente del hash de una imagen cambiante.

Los reintentos no borran fallos. Definir cuándo son seguros, especialmente para mutaciones y persistencia. No repetir hasta lograr PASS; conservar el primer fallo y registrar intermitencia.

## 5. Roadmap incremental

### Dependencias y entregas útiles

```text
Línea cliente:   E0 → E3 → E4 (primer E2E: gate de login) → E5 → E6 ─┐
Línea ambiente:  E0 → E1 ────────────────────────────────────────────┤
                                                                     ↓
                                                   E7 → E10a (menú de Town, solo imagen)
                                                    │
                                                    ├→ E8 → E9 → E10b → E11
                                                    └→ E2 (worker aislado)
                                                                      ↓
                                   E11 + E2 → E12 → E13 → E14 → E15 → E16

E17 comienza con E10a y amplía su catálogo con E10b–E16.
E18 depende de E17 y es opcional si IDs/alias satisfacen los pedidos.
E19 requiere E2 y cierra operación autónoma sobre la cobertura acordada.
```

Orden de ejecución recomendado: E0 → E3 → E4 → E5 → E6, con E1 en paralelo; luego E7 → E10a → E8 → E9 → E10b → E11 → E2 → E12–E16. La primera prueba funcional real ocurre en E4 y la primera feature de Town se evalúa en E10a.

Este orden prioriza una unidad verificable por vez; no prohíbe investigación independiente. Las dos líneas pueden avanzar en paralelo solo con responsables distintos. No solapar escritores en el mismo working tree ni crear worktrees sin autorización.

Hasta E2, los pilotos son ejecuciones atendidas en el escritorio local, con los guards de QA-002 (foco exacto, ownership por PID/HWND, release y cleanup) y sin afirmar autonomía.

### Hito A — Contrato, ambiente y evidencia

#### E0. Primer contrato funcional (reducido)

- **Objetivo:** fijar la feature objetivo (abrir/cerrar el menú personal de Town) y derivar de ella los requisitos de E1–E7.
- **Dependencias:** ninguna.
- **Alcance/entrega:** expectativas con su fuente propietaria (GD, Arquitectura o código); precondiciones del recorrido (cuenta con personaje creado, Host en Town, menú conectado, sin otros paneles abiertos); matriz de capacidades existentes/faltantes con la etapa que las cubre; conflictos registrados; decisión sobre QA-003.
- **Exclusiones:** plantillas visuales, regiones, tiempos, formato de evidencia y esquema de escenario (se definen en E6/E10a sobre UI vigente); combate, persistencia de Equipment y exploración general.
- **Aceptación:** cada expectativa tiene fuente propietaria; precondiciones y resultado evaluable inequívocos; conflictos registrados; ningún detalle depende de UI provisoria pendiente de UI.2/UI.3.
- **Validación:** revisión contra código, arquitectura y documentos vigentes. QA-003 no es precondición de E0; con commit propio y comprobación real del gate registrada, es precondición de E4.

#### E1. Cuentas y fixtures mínimos

- **Objetivo:** preparar estado inicial repetible sin tocar producción.
- **Dependencias:** E0. Avanza en paralelo a E3–E6.
- **Alcance/entrega:** backend/DB descartables, cuentas QA con personaje creado, identidad/perfil/inventario inicial y reset idempotente; configuración Photon/Fusion del ambiente QA identificada; referencias a secretos fuera de repo/argv.
- **Exclusiones:** reset administrativo sobre datos reales, una plataforma general de provisioning.
- **Aceptación:** destino explícitamente autorizado; rechazo de otro destino antes de mutar; fixture verificado; cuentas y datos identificados por ejecución.
- **Validación:** preparar dos veces, comparar estado y probar negativa con un destino no autorizado. Destruir o restaurar únicamente recursos descartables propios.

#### E3. Reporte versionado y logs

- **Objetivo:** evidencia útil antes de agregar autonomía funcional.
- **Dependencias:** E0. Entregar el mínimo que necesita E4 y ampliarlo con los casos reales.
- **Alcance/entrega:** esquema versionado, Player log por ejecución mediante `-logFile`, timeline, artefactos y redacción/límites.
- **Exclusiones:** dashboard, almacenamiento distribuido, publicación remota.
- **Aceptación:** reconstruir secuencia y primera falla; compatibilidad explícita; secretos ausentes; errores de disco/evidencia no ocultos.
- **Validación:** reportes de ejecución sana, crash, interrupción, disco no disponible y secreto sintético. Verificar log del Player, no solamente streams heredados.

### Hito B — UI y autenticación reales

#### E4. Mouse seguro y primer recorrido de punta a punta

- **Objetivo:** click reproducible en el área cliente validada y primera prueba funcional real: el gate de login del menú principal.
- **Dependencias:** E3; QA-003 con commit propio y comprobación real del gate registrada.
- **Alcance/entrega:** click sobre coordenada local o anchor visual único, guard antes/después y release de botones. Recorrido: launch → menú principal listo (QA-003) → click en INICIAR (*Create Room*) sin sesión → panel de login visible → click en *Back* → menú principal restaurado. Sin backend ni credenciales.
- **Exclusiones:** drag-and-drop, multiscale general, localización fuera de la ventana, escritura de texto y login real.
- **Aceptación:** ningún click con foco/ownership inválidos, geometría cambiada o target ambiguo; nunca usar posición absoluta de escritorio sin conversión validada. El recorrido produce resultado de feature según §4, con esperado tomado de `MainMenuController` y GD UI §4.1, y evidencia antes/durante/después sin campos de credenciales.
- **Validación:** tests con mocks y ejecución atendida autorizada del recorrido; mover/redimensionar ventana y perder foco; control defectuoso sintético (captura sin panel de login) que falle por la aserción prevista. Si se implementa recuperación de sesión (conflicto de E0), el fixture debe garantizar estado sin sesión.

**Entrega temprana:** primer recorrido real de punta a punta, con resultado de feature y evidencia, sin backend.

#### E5. Texto y credenciales

- **Objetivo:** completar formularios sin exponer secretos.
- **Dependencias:** E4.
- **Alcance/entrega:** escritura en campos conocidos, charset/layout explícitos, referencias a secretos resueltas en memoria.
- **Exclusiones:** clipboard de secretos como default, Unicode arbitrario sin prueba de compatibilidad.
- **Aceptación:** panel y destino verificados antes de escribir; no publicar credenciales ni capturas sensibles; cancelar ante incertidumbre de campo/foco.
- **Validación:** campo incorrecto, pérdida de foco, caracteres usados por cuentas QA y auditoría de logs/reportes/imágenes.

#### E6. Escenarios finitos

- **Objetivo:** describir recorridos sin programar un script nuevo por prueba.
- **Dependencias:** E3–E5.
- **Alcance/entrega:** JSON pequeño con espera de estado, key/click/type, capture y assertions disponibles; validación antes del launch.
- **Exclusiones:** loops sin límites, código Python/shell generado, framework de plugins.
- **Aceptación:** allowlists, límites por paso/global, cancelación y cleanup; fallo de un paso impide acciones posteriores no autorizadas por el escenario.
- **Validación:** esquema inválido, acción desconocida, orden incorrecto, timeout, interrupción y regresión de QA-001/002/003.

#### E7. Login y contexto posterior

- **Objetivo:** primer recorrido autenticado real, sin saltar autenticación.
- **Dependencias:** E1, E3–E6. Ejecución atendida en escritorio local hasta E2. La precondición de sesión depende de resolver el conflicto de recuperación de sesión registrado en E0.
- **Alcance/entrega:** navegar al gate de login, ingresar cuenta y comprobar identidad/hidratación/contexto. Lectura backend independiente cuando esté disponible.
- **Exclusiones:** inyectar tokens para evitar el login probado; afirmar cobertura de gameplay por llegar a Town.
- **Aceptación:** CharacterId del fixture, datos coherentes, estado posterior alcanzado; errores visibles ante rechazo o backend inaccesible.
- **Validación:** credenciales válidas/incorrectas, backend indisponible y repeticiones desde proceso nuevo. Añadir expiración/token inválido como caso pequeño separado si el contrato lo exige.

#### E10a. Menú personal de Town, solo imagen

- **Objetivo:** evaluar el contrato de E0 sobre la build real usando solo observación visual.
- **Dependencias:** E6–E7.
- **Alcance/entrega:** Tab abre (ventana visible, Inventario y Equipamiento visibles, HUD oculto), Tab cierra (HUD restaurado) y Escape cierra. Referencias visuales creadas sobre la UI vigente al momento de ejecutar.
- **Exclusiones:** supresión de input de gameplay (excluida explícitamente de la cobertura hasta E10b), C/Atributos, Escape→Options, rechazo con otro panel abierto, durable Equipment/Off Hand.
- **Aceptación:** input real; el resultado cubre solo las aserciones visuales declaradas y el reporte lista las exclusiones; evidencia antes/durante/después y cleanup limpio.
- **Validación:** build sana y controles defectuosos sintéticos (capturas sin ventana o con HUD visible) que fallen por la aserción prevista; repetir desde proceso nuevo.

**Entrega útil A:** login/UI reproducibles y una primera feature de Town evaluada por imagen, con evidencia y diagnóstico, todavía sin afirmar pruebas completas de gameplay.

### Hito C — Features evaluadas por oráculos

#### E8. Observación QA mínima

- **Objetivo:** observar una feature sin mutarla.
- **Dependencias:** E7 y aprobación de instrumentación.
- **Alcance/entrega:** snapshot acotado, build/run/profile/role/tick/revisión, freshness y canal local protegido.
- **Exclusiones:** setters del dominio, lectura arbitraria del árbol de objetos y APIs administrativas remotas.
- **Aceptación:** observación correcta del participante; datos vencidos/build incorrecta rechazados; soporte ausente o inerte fuera de builds QA.
- **Validación:** contrato con fixtures deterministas, build/perfil equivocado, canal caído y artefacto de producción sin capacidad QA. Si se rechaza instrumentación, documentar cobertura black-box inferior y sus bloqueos.

#### E9. Aserciones independientes

- **Objetivo:** separar esperado de observado.
- **Dependencias:** E3 y E8.
- **Alcance/entrega:** comparaciones exactas y temporales, provenance, divergencias UI/modelo/backend.
- **Exclusiones:** PASS decidido por LLM, reutilizar el cálculo probado para producir esperado.
- **Aceptación:** dato ausente/vencido no pasa; tolerancias justificadas; cada assertion identifica fuente y contradicción.
- **Validación:** controles sanos y observaciones incorrectas deliberadas; demostrar que cada assertion falla por su causa específica.

#### E10b. Primera feature completa de Town

- **Objetivo:** completar el contrato de E0 con oráculos de estado, sin intervención durante el recorrido.
- **Dependencias:** E8–E9 y E10a.
- **Alcance/entrega:** sumar a E10a la supresión de input y la coherencia modelo/presentación al abrir y cerrar.
- **Exclusiones:** durable Equipment/Off Hand no resuelto, todas las pantallas simultáneamente.
- **Aceptación:** input real, modelo/presentación acordes, evidencia antes/durante/después y cleanup limpio.
- **Validación:** build sana y control defectuoso que no abra/cierre/restaure; repetir desde fixture nuevo. Después incorporar una operación de inventario por vez, por ejemplo split 13→5+8.

#### E11. Gameplay por primitivas

- **Objetivo:** probar acciones reales, no un bot de exploración general.
- **Dependencias:** E8–E9 y E10b.
- **Alcance/entrega:** cuatro unidades independientes: movimiento; interacción; una habilidad válida con coste/cooldown; intento sin recurso suficiente.
- **Exclusiones:** pathfinding general, todos los enemigos/habilidades, crear sistemas de gameplay ausentes.
- **Aceptación de cada unidad:** fixture de mapa/objetivo conocido, input normal, observación confirmada y ventana temporal contractual. Regla vigente: sin recurso suficiente no inicia ejecución ni cooldown.
- **Validación:** positivos, rechazos previstos y controles defectuosos sobre build real. Tomar magnitudes de configuración versionada; no inventar balance.

**Entrega útil B:** una feature individual evaluada autónomamente y pruebas básicas de gameplay ampliables.

### Hito D — Multijugador y durabilidad

#### E2. Worker Windows aislado

- **Objetivo:** escritorio gráfico estable y control seguro.
- **Dependencias:** E7. Requerido antes de E12 y de cualquier ejecución sin supervisión.
- **Alcance/entrega:** perfil de VM/máquina dedicada, build manifest, health check de sesión/captura, ownership de procesos y política de foco autónomo opt-in dentro del worker.
- **Exclusiones:** apropiarse del escritorio del desarrollador, matar procesos por nombre.
- **Aceptación:** detectar sesión bloqueada/foco perdido; stop de emergencia; cleanup bajo propiedad demostrada, incluidos descendientes relevantes; timeout visible.
- **Validación:** launch normal, exe inválido, crash, bloqueo de sesión, pérdida de foco y árbol de procesos de control. Validar GPU/rendering; un flag `vm-confirm` no prueba aislamiento por sí solo. Repetir sobre el worker los recorridos de E4, E7 y E10a.

#### E12. Host y Client coordinados

- **Objetivo:** dos participantes reales con roles y sesión inequívocos.
- **Dependencias:** E2, E7–E9; E11 para escenarios que usan esas primitivas.
- **Alcance/entrega:** workers/identidades distintos, barreras de login/conexión/spawn, artifacts correlacionados y cleanup conjunto.
- **Exclusiones:** input global simultáneo a dos ventanas del mismo escritorio, asumir dedicated server.
- **Aceptación:** roles y participantes comprobados; sesión aislada por run; timeout de un participante no bloquea cleanup de otros.
- **Validación:** conexión correcta, sesión incorrecta, no llegada de un participante y caída de worker. Primera opción: escritorios separados. Un input adapter futuro no reemplaza validación externa de UI.

#### E13. Features multijugador concretas

- **Objetivo:** demostrar efecto y consistencia desde múltiples vistas.
- **Dependencias:** E11–E12.
- **Alcance/entrega:** unidades separadas de PvE, PvP y concurrencia, una regla cada vez.
- **Exclusiones:** inventar semántica de combate, cubrir todas las combinaciones de autoridad de una vez.
- **Aceptación:** identificar State/Input Authority y proxies; efecto correcto y convergencia acotada; no duplicación.
- **Validación:** variantes Host/Client, controles defectuosos y snapshot de ambos lados. Ejemplo de GD: dos pickups simultáneos sobre diez unidades adjudican en total como máximo diez.

#### E14. Extracción y regreso

- **Objetivo:** verificar condiciones y resultado de extracción, primero Solo y luego Duo.
- **Dependencias:** E11–E13.
- **Alcance/entrega:** fixture con precondiciones del santuario/cuota cumplidas, acciones normales, cancelación/reinicio y retorno.
- **Exclusiones:** forzar completar extracción mediante un setter; equiparar countdown completo a persistencia confirmada.
- **Aceptación:** Solo fuera del área cancela/reinicia; daño por sí solo no cancela; Duo requiere integrantes pertinentes conectados y presentes; destino del loot conforme a contrato.
- **Validación:** variantes positivas/negativas con evidencia de participantes. Diferenciar countdown, resultado aceptado y persistencia confirmada. Preparar santuario/cuota pertenece al fixture, no a la acción evaluada.

#### E15. Persistencia e idempotencia

- **Objetivo:** demostrar resultados durables, no solamente estado local.
- **Dependencias:** E14 y contrato remoto disponible para los campos evaluados.
- **Alcance/entrega:** lectura backend, reinicio, reautenticación, comparación de identidades/cantidades/slots y duplicados.
- **Exclusiones:** reescribir backend para ocultar una limitación; declarar durable Off Hand sin contrato.
- **Aceptación:** confirmaciones repetidas no duplican, operaciones confirmadas sobreviven reinicio, fallos temporales no producen éxito prematuro.
- **Validación:** operación normal, interrupción y retry controlado, lectura tras reinicio. Separar persistencia de extracción, derrota/return y restauración en tareas pequeñas, con reglas de cada fuente propietaria.

#### E16. Fallos de conexión y recuperación

- **Objetivo:** validar continuidad y desenlaces bajo fallos controlados.
- **Dependencias:** E12–E15 y resolución de ambigüedades aplicables.
- **Alcance/entrega:** Client disconnect/reconnect, pérdida abrupta de Host, recuperación exitosa/fallida; abandono voluntario como caso independiente.
- **Exclusiones:** fallos sobre red/sesiones ajenas, confundir salida normal con crash.
- **Aceptación:** participación/inventario consistentes, budgets de reconexión respetados, sin doble adjudicación y cleanup visible.
- **Validación:** afectar solamente proceso/conexión/worker propios, guardar vistas de todos los participantes y backend antes/después. GD indica que el personaje desconectado permanece en la instancia y el presupuesto de reconexión es acumulativo; usar tiempos configurados, no números inventados.

**Entrega útil C:** features con participantes múltiples, resultados persistentes y manejo de fallos contractuales.

### Hito E — Pedido de feature y operación autónoma

#### E17. Catálogo y selección

- **Objetivo:** pedir una feature sin preparar manualmente el recorrido.
- **Dependencias:** E10a para iniciar; ampliar con E10b–E16.
- **Alcance/entrega:** features/aliases, variantes, contratos, capacidades, fixtures, escenarios y cobertura.
- **Exclusiones:** afirmar cobertura sobre features desconocidas.
- **Aceptación:** pedido resuelve plan relevante, prepara y evalúa; explica exclusiones y bloquea capacidades ausentes.
- **Validación:** features conocidas, alias, Solo/Duo, ambigüedades y feature no implementada. Un selector convencional ya puede satisfacer el objetivo sin IA.

#### E18. Interpretación/composición asistida

- **Objetivo:** aceptar pedidos más flexibles cuando aporte valor.
- **Dependencias:** E17. Opcional si IDs/aliases son suficientes.
- **Alcance/entrega:** extracción de intención y composición dentro de acciones/observadores comprobados, con fuentes y JSON validado.
- **Exclusiones:** shell/código generado, permisos creados por el modelo, contradicciones de GD resueltas silenciosamente.
- **Aceptación:** grounded plan, presupuesto y policy independiente; bloqueo si falta oracle/contrato; contenido de imágenes/logs/documentos tratado como datos no confiables, no como instrucciones.
- **Validación:** corpus de pedidos/paráfrasis/adversariales, comparar contra selector convencional y medir errores/costo. Una feature nueva puede componerse solamente si sus capacidades y esperado ya existen.

#### E19. Operación autónoma estable

- **Objetivo:** completar recorrido sin intervención y reportar de forma confiable.
- **Dependencias:** E2 y las etapas necesarias para la cobertura acordada; E18 solo si se adopta.
- **Alcance/entrega:** preflight→fixture→login→acciones→assertions→report→cleanup; repetición, clasificación local y guía de operación.
- **Exclusiones:** dashboard o CI distribuidas como requisito; publicación remota automática no autorizada.
- **Aceptación:** campaña sana repetible, controles defectuosos detectados por la razón esperada, resultados incompletos honestos y ambiente limpio.
- **Validación:** casos de UI, gameplay y multijugador desde ambiente recién preparado; entrada inválida, dependencia caída, cancelación y evidencia/cleanup fallidos. Registrar métricas y cumplir la definición de terminado.

## 6. Estrategia de validación

### Pirámide económica

1. Tests unitarios del runner, schemas, evaluator y detector.
2. Tests de contratos con clocks/procesos/GUI/backend simulados.
3. Tests Unity EditMode/PlayMode y backend existentes, relevantes al comportamiento.
4. Pruebas reales de worker/input/captura.
5. Pocos escenarios E2E críticos sobre builds reales.
6. Campaña multijugador/fallos solo para contratos que lo requieran.

Para cambios de comportamiento con tests deterministas aplicables, observar RED→GREEN y después regresiones. Documentación pasiva no tiene un RED funcional significativo; verificar estructura, enlaces y cobertura del contenido.

No reemplazar los niveles inferiores por escenarios lentos. Un build headless no renderiza/acepta el input de una prueba gráfica normal; usarlo únicamente para los tipos de test apropiados.

### Demostrar capacidad de detectar bugs

Cada assertion crítica necesita un control sano y un control defectuoso. Empezar por datos/observaciones sintéticos incorrectos; después una build de control deliberadamente defectuosa cuando sea necesario, aislada y aprobada. Nunca desplegar fault injection en producción.

Ejemplos: ignorar el cierre de panel; simular snapshot obsoleto; duplicar el resultado de pickup; devolver una confirmación sin cambio durable. La prueba debe fallar por la divergencia esperada, no por crash accidental del harness.

### Gates de estabilidad propuestos

- Diez ejecuciones sanas consecutivas por escenario crítico sobre baseline fija.
- Controles defectuosos que fallen por la assertion prevista.
- Cero PASS con evidencia obligatoria inválida o cleanup fallido.
- Escenarios inestables identificados y separados; conservar fallos intermitentes.
- Medir duración y costo, diferenciando fixture, launch, acción, observación y cleanup.

Estos son criterios iniciales propuestos, no garantía estadística de cobertura ni resultados ya observados.

### Comprobaciones existentes de referencia

Desde el root, cuando se autorice validar una implementación:

```powershell
python -m unittest discover -s tools/qa-runner/tests -v
& tools/qa-runner/.venv/Scripts/python.exe -m unittest discover -s tools/qa-runner/tests -v
& tools/qa-runner/.venv/Scripts/python.exe -m pip check
git diff --check
```

No ejecutan una validación de gameplay. Los nuevos checks Unity requieren el flujo y herramientas del proyecto. Un ejemplo de comando en documentación no autoriza interacción real con escritorio/build.

## 7. IA y automatización convencional

| Necesidad | Primera solución | IA |
|---|---|---|
| Procesos, guards, cancelación, cleanup | Código determinista. | No. |
| Cuentas, fixtures y reset | APIs/tools acotados y ambiente descartable. | No. |
| UI estable | Input controlado, anchors/templates versionados. | No inicialmente. |
| Health, cooldown, inventario, persistencia | Assertions sobre estado confirmado y backend. | No como juez. |
| Feature conocida | ID/alias/parámetros. | No necesariamente. |
| Pedidos complejos | Plan grounded y schema validado. | Opcional. |
| Resumen de logs/reportes | Reglas primero, síntesis sobre evidencia. | Opcional. |
| UI sin observación convencional viable | Benchmark OCR/visión dirigido. | Posible, no autoridad funcional. |
| Exploración de situaciones nuevas | Acciones limitadas y replay. | Posterior, como generador de candidatos. |

No llamar al modelo por frame/click, no entrenar RL y no permitir que ajuste expectativas para conseguir PASS. Definir límites de tokens, costo, acciones, tiempo y herramientas. La indisponibilidad del proveedor no puede saltarse validación o permisos.

## 8. Riesgos y decisiones

| Tema | Riesgo/decisión | Momento y recomendación |
|---|---|---|
| Backend | Seed actual no garantiza aislamiento/reset. | E1: allowlist de destino, recursos descartables y fixture verificado. |
| Foco | Carrera residual de input global. | E4–E11: ejecuciones atendidas con guards de QA-002, sin autonomía. E2, antes de E12/E19: worker dedicado; autonomía de foco explícita; fail closed. |
| Sesión remota | Bloqueo/RDP/GPU pueden invalidar render/captura. | E2: prueba real del perfil de worker y health check. |
| Instrumentación | Self-fulfilling test o exposición de control. | Antes de E8: observación mínima, producción deshabilitada, canal protegido. |
| Documentación heredada | Expected equivocado sobre persistencia. | E0 y cada feature: matriz por campo/contrato; no blanket memory-only/durable. |
| Off Hand | Contrato remoto incompleto. | E15: exclusión/bloqueo explícito hasta migración separada aprobada. |
| Migración | GD INV-AC-22/23 dice Host «abandona»; arquitectura excluye abandono voluntario. | Antes de esos casos E16: resolver con responsables; no elegir silenciosamente. |
| Identidad | PlayerRef no es identidad durable. | Desde E7/E8: CharacterId/ProfileId y run/client IDs, sanitizados. |
| Confianza del backend | Lectura backend no prueba por sí misma autenticidad/autoridad de origen de resultados. | E15/E16: aislar QA, registrar límite; seguridad/anti-cheat tiene alcance separado. |
| Escalado | Un desktop no soporta input global concurrente fiable a varios clientes. | E12: escritorios independientes o adapter aprobado; pruebas UI externas permanecen. |
| Recursos de red | Sesiones compartidas/Photon y cuotas pueden contaminar pruebas. | E12: IDs de sesión únicos, ambiente/config autorizados, medir cuotas y costo. |
| Fixtures | Mutaciones de setup podrían reemplazar la acción probada. | E1/E11: separar setup y ejecución, manifest de fixture y provenance. |
| IO/captura | Overlays y llamadas no interrumpibles. | E2/E3: datos inválidos descartados, deadlines y worker recovery; no prometer bound duro imposible. |
| IA | Alucinación, prompt injection, filtración/costo. | E18: grounding, schema/policy, redacción y budget; modelo no declara PASS. |
| Bugs remotos | Duplicados, datos sensibles y falsas denuncias. | Después de E19: autorización, dedup y revisión inicial. |

Las decisiones arquitectónicas y de Game Design deben aprobarse antes de cambiar sus contratos. No convertir una limitación del juego en trabajo implícito del runner.

## 9. Organización y costos

- Un responsable principal del runner; colaboración puntual de backend para fixtures y Unity para observación.
- Un responsable de producto/Game Design valida expected y conflictos; el equipo revisa bugs confirmados.
- Un cambio pequeño por unidad del roadmap; tests y documentación junto al comportamiento, con commits solamente conforme a autorización/política del repositorio.
- Primero un worker local, después dos para Host/Client; escalar solamente con métricas reales.
- No fijar cronograma preciso antes del piloto E7. Medir mantenimiento de localizadores, tiempo por escenario, fallos de ambiente, costos de máquinas/servicios y utilidad del reporte.
- No incorporar nuevas dependencias o SDK comerciales sin comparación concreta de costo/beneficio y aprobación.
- E4 entrega el primer recorrido real y E0–E10a entregan valor antes de invertir en un bridge amplio, un worker dedicado o IA. E8–E10b prueban que el diseño puede evaluar una feature con oráculos de estado, no solo automatizarla.

La automatización no elimina mantenimiento: actualizar contratos, fixtures y localizadores es parte del costo recurrente. Registrar propietario por escenario para evitar abandono.

## 10. Definición de terminado

Para la cobertura acordada, el runner debe poder recibir, por ejemplo:

> «Probá la transferencia concurrente de este objeto entre dos jugadores».

Y completar:

1. Resolver feature, variantes y contrato sin inventar reglas.
2. Preparar cuentas, fixtures y builds identificadas.
3. Ejecutar participantes y autenticar sin intervención durante el recorrido.
4. Realizar acciones por caminos normales del juego.
5. Observar resultados desde fuentes apropiadas, con authority/freshness/provenance.
6. Evaluar assertions y distinguir fallo funcional, tool, ambiente o cobertura ausente.
7. Detectar un defecto conocido y no denunciarlo en la build de control sana.
8. Entregar evidencia útil y pasos para reproducir la primera divergencia.
9. Terminar con cleanup verificado.
10. Informar cobertura/exclusiones y preservar el primer fallo aunque un retry pase.

### Checklist de cierre

- [ ] Primer caso completo de UI desde login, sin preparación manual durante la ejecución.
- [ ] Caso completo de gameplay, con positivo y rechazo esperado.
- [ ] Caso completo Host/Client con resultados correlacionados.
- [ ] Caso que demuestra persistencia tras reinicio, para campos dentro del contrato.
- [ ] Controles defectuosos detectados por la causa esperada.
- [ ] Campaña repetida conforme al gate de estabilidad acordado.
- [ ] Secretos ausentes y recursos de producción inaccesibles al harness.
- [ ] Cleanup/evidencia fallidos impiden PASS limpio.
- [ ] Features desconocidas o contratos ambiguos producen bloqueo explícito.
- [ ] Operación y recuperación documentadas; no hay intervención oculta necesaria.

Autonomía no equivale a cobertura ilimitada. Una feature nueva requiere un esperado definido y acciones/observaciones disponibles. Si falta una capacidad, el runner identifica el bloqueo y la incorporación necesaria, no finge una evaluación. No afirmar que «el juego está validado» por haber pasado una feature.

## 11. Mejoras posteriores

Estas propuestas comienzan **después de completar E19 y demostrar utilidad/estabilidad**. No son requisitos ocultos del plan base. Aplicar una mejora solo cuando una métrica o necesidad concreta justifique su costo.

| Prioridad | Mejora | Condición para invertir | Entrega y aceptación verificable |
|---|---|---|---|
| P1 | Smoke suite en CI sobre worker gráfico dedicado | Ejecución local estable y manual repetitiva. | Job con build/artifacts, sin secretos, bounded y recovery; reproduce resultados locales y clasifica fallos de infraestructura. |
| P1 | Exportación a HacknPlan/GitHub | Reportes locales útiles y permiso de publicación. | Draft/autopublish configurable, dedup e idempotencia; timeout ambiguo no duplica issues. |
| P1 | Selector de regresión por cambio | Catálogo con ownership y suite cuyo costo crece. | Selección por áreas/dependencias y smoke obligatorio; benchmark contra campaña completa sin omitir fallos conocidos del corpus. |
| P1 | Tendencias e intermitencia | Hay suficiente historial de ejecuciones. | Vista simple de duración/failure/flake por build y escenario; no esconder defectos en quarantine ni usar histórico como expected funcional. |
| P1 | Evidencia temporal de bajo costo | Capturas aisladas no explican bugs de timing. | Ring buffer/clip corto o secuencia acotada alrededor del fallo, redacción y límite de disco; mejora reproducción sin degradar tiempos del escenario. |
| P2 | Property-based y fuzzing acotado | Inventario/transferencias tienen invariantes estables. | Seeds guardadas, conservación de cantidades/identidad/origen y minimización del caso; replay reproduce divergencia. |
| P2 | Matriz de resolución/DPI/layout/idioma | Baseline estable y necesidad de compatibilidad. | Un perfil por incremento, localizadores/charset probados; fallos de captura separados de UI funcional. |
| P2 | Matriz de red y recuperación | Casos E16 estables y budgets de red definidos. | Latencia/jitter/pérdida controlados solo en QA, perfiles versionados y repeatable replay; expectativas respetan configuración y ticks. |
| P2 | Soak/performance dirigido | Hay targets de rendimiento aprobados y baseline. | Métricas CPU/GPU/memoria/frame/network según caso, warmup/budgets conocidos; alertas sobre regresiones reproducibles, no una screenshot de FPS. |
| P2 | Workers concurrentes y scheduling | Cola/tiempo de suite exige escalar. | Aislamiento por run/cuenta/sesión, locks/leasing, cuota y cleanup; ningún recurso cruzado y costo medido por feature. |
| P2 | Replay y reducción automática | Reportes complejos dificultan reproducir. | Fixture/seed/contrato/input trace versionados; reducir pasos solo si preserva la divergencia y no cambia expected. |
| P3 | OCR/VLM para localización difícil | Anchors/observación convencional no alcanzan a costo razonable. | Benchmark con falsos positivos/negativos, budget y abstención; VLM propone targets/anomalías y nunca reemplaza assertions funcionales. |
| P3 | Exploración guiada por IA | Regresión confiable ya entrega valor y se busca descubrir casos nuevos. | Acciones permitidas, sandbox, budget, novelty y replay; hallazgos exploratorios se confirman antes de ser bugs. |
| P3 | Pruebas sin input global mediante adapter | Costo/foco de varios escritorios limita cobertura. | Adapter que atraviesa pipeline normal y conserva authority/prediction; demostrar paridad con inputs externos y mantener smoke black-box sobre release. |
| P3 | Dashboard web | Reportes/tendencias simples resultan insuficientes para el equipo. | Navegación por build/feature/assertion/artifact, acceso y retención; justificar mantenimiento frente a HTML local. |

### Orden recomendado de mejoras

1. CI simple, exportación autorizada y tendencias, solo si reducen trabajo real.
2. Selección de regresión y fuzzing de invariantes de alto impacto.
3. Evidencia temporal, matrices de red/hardware y escala de workers según los bugs observados.
4. Visión/IA exploratoria al final, con benchmark y presupuesto.

No agrupar todas las mejoras en una segunda gran entrega. Cada una conserva objetivo, alcance, aceptación y validación independientes. AI/RL general y carga masiva siguen fuera de alcance salvo decisión nueva explícita.

## 12. Inicio y continuidad

### Próxima acción propuesta

Autorizar **E0 reducido**, no comenzar por IA. Fijar el caso de Town, sus expectativas, precondiciones y capacidades, y confirmar:

- Disponibilidad de una build identificada; backend QA descartable para E1/E7.
- Permiso para ejecuciones atendidas en el escritorio local desde E4; worker Windows gráfico dedicado recién para E2.
- Aprobación o rechazo de observación mínima en builds QA para E8.
- Responsables de contratos y resolución de conflictos.

No ejecutar E1/E2 por el solo hecho de aprobar el documento. Autorizar superficies y acciones correspondientes al siguiente work item.

### Antes de cada etapa

1. Releer instrucciones y work item si existe.
2. Comprobar branch/working tree; preservar cambios ajenos.
3. Revalidar documentos propietarios, versiones y capacidad real de la build.
4. Precisar archivos/assets/sistemas permitidos, exclusiones y checks disponibles.
5. Documentar decision gates y criterios de cierre.
6. Implementar y observar checks pertinentes, sin afirmar validaciones no ejecutadas.
7. Registrar resultado, fallos/skips, límites y siguiente etapa habilitada.

Este documento es el plan integral, no un segundo tracker de tareas existentes. Si el proyecto se registra en HacknPlan, mantener allí el estado de trabajo y usar el documento para contratos/dependencias; evitar mirrors duplicados de tareas sin solicitud explícita.

## 13. Referencias

### Repositorio

- [Reglas del repositorio](../../AGENTS.md).
- [Reglas Unity/Fusion](../../Project%20Grimhold/AGENTS.md).
- [Skill de ejecución](../../skills/grimhold-task-execution/SKILL.md).
- [README del runner](../../tools/qa-runner/README.md).
- [Runner](../../tools/qa-runner/runner.py), [control Windows](../../tools/qa-runner/windows_control.py), [readiness](../../tools/qa-runner/readiness.py).
- [LoginFlowController](../../Project%20Grimhold/Assets/Scripts/Backend/LoginFlowController.cs).
- [Identidad y autenticación](../../Project%20Grimhold/Docs/Architecture/RemoteIdentityAndAuthArchitecture.md).
- [Persistencia local e integración](../../Project%20Grimhold/Docs/Architecture/LocalPlayerPersistenceArchitecture.md).
- [Menú personal de Town](../../Project%20Grimhold/Docs/Architecture/TownPlayerMenuArchitecture.md).
- [Session lifecycle/spawning](../../Project%20Grimhold/Docs/Architecture/SessionLifecycleAndSpawning.md).
- [Host Migration](../../Project%20Grimhold/Docs/Architecture/HostMigrationRecoveryArchitecture.md).
- [Seed de cuentas](../../backend/scripts/seed-test-accounts.js).

### Game Design vigente consultado

Los enlaces identifican las fuentes encontradas durante la investigación. Al implementar, usar `search_documents` para confirmar dueño y revisión vigente; no enrutar futuros cambios exclusivamente por IDs/títulos guardados aquí.

- [01 - Game Flow Principal](https://docs.google.com/document/d/1Ne9bqHwtzn5_rpFEfxxNBrII75TkYyszyqSFXyKS108/edit): Pueblo/preparación, todos Listos y transición del Host.
- [02 - Estados del Juego](https://docs.google.com/document/d/1j-1yZ6eiJacTdVl4EOya570zt1pvVNYtWvXHDo2orDk/edit): desconexión en Dungeon y presupuesto acumulativo.
- [06 - Sistema de Extracción](https://docs.google.com/document/d/1C8DBIByc1HbzUdABqkXR2syLOKYaBbankMMHdaH4jD4/edit): secciones Solo y Duo.
- [11 - Diseño de Habilidades](https://docs.google.com/document/d/14pw5-NsV_lw4_YGj5AuPg5wJUXqg1TWm6YEozcr1sMY/edit): recursos/costes/cooldowns; leer targeting propietario al desarrollar un caso particular.
- [16 - Diseño de Inventario](https://docs.google.com/document/d/1V4TMz6qI4RYzotmf8jcTRxmhV2V9Bww8iyEmwyk-s0g/edit): persistencia/extracción y matriz INV-AC-01–25.

### Documentación externa

- [Unity Player command-line arguments](https://docs.unity3d.com/6000.0/Documentation/Manual/PlayerCommandLineArguments.html): `-logFile`, resolución y límites de `-batchmode`/`-nographics`. Verificar contra la versión instalada antes de implementar APIs sensibles a versión.
- [PyAutoGUI](https://pyautogui.readthedocs.io/en/latest/): input global, primary monitor y failsafe; no ofrece OCR integrado.

**No hay validación real nueva de build/gameplay asociada a este documento.** Los resultados previos se describen como evidencia histórica y las etapas futuras como propuestas.
