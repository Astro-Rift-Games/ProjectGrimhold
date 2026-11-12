# FIX-52 — Solicitud a Game Design: actualizar Game Flow §13

HacknPlan FIX-52 (ID 732), historia FIX-US-08 (ID 727).
Estado: **borrador listo para enviar**. No se envió ni se registró en ningún canal; el responsable de arquitectura lo envía y deja constancia (enlace al mensaje o comentario de HacknPlan) antes de cerrar FIX-52.
Fecha del borrador: 2026-11-12. Destinatario: responsable de Game Design. Remitente: responsable de arquitectura.

## 1. Pedido

Actualizar **Game Flow §13** para que refleje la decisión de producto del 2026-11-01 y lo que ya está implementado en las tareas FIX-48 a FIX-53:

1. La salida del Host, voluntaria o no, traspasa el rol de Host a otro jugador activo dentro de la Dungeon.
2. No existe la acción "Cancelar raid".
3. La Host Migration pasa a ser un requisito del MVP, no una intención de diseño.

Esta solicitud no modifica ningún documento de Game Design. La edición de Game Design es de su responsable (FIX-52 la deja fuera de alcance).

## 2. Decisión textual (FIX-US-08, 2026-11-01)

> "si el host se desconecta deberia pasar el host a otro jugador activo dentro de la dungeon, la raid y el equipamiento/loot de otros jugadores no puede depender de una sola persona."

## 3. Qué dice hoy Game Flow §13

**No pude leerlo.** Esta sesión no tuvo acceso a `grimhold-docs` (no hay herramienta de ese servidor disponible y no se usaron credenciales de Drive), así que no se transcribe ni se resume su contenido.

Antes de enviar, quien lo registre debe pegar aquí el texto vigente de §13 y confirmar la numeración: el repositorio cita "01 Game Flow Principal" y también "02 Estados del Juego" con un §13 propio (`Plan-US-52-Continuidad-y-Reconexion.md`), y esta solicitud asume que "Game Flow §13" es el de "01 Game Flow Principal".

Lo único que se sabe con certeza es lo que afirmaban los contratos de arquitectura aprobados antes de FIX-52 y que ahora se corrigieron (`Project Grimhold/Docs/Architecture/HostMigrationRecoveryArchitecture.md`): que Return, Abandon, Return tras extracción, cancelación y apagado voluntario nunca inician Host Migration, y que un Host conectado no puede salir de la raid por esas vías. Si §13 dice algo equivalente, es lo que hay que reemplazar.

## 4. Cambio propuesto (texto sugerido para §13)

Redacción propuesta; Game Design decide forma y ubicación final.

> **Salida del Host.** El Host no tiene una salida propia ni una acción de cancelar la raid. Mientras vive, abandona como cualquier participante (confirmación en dos pasos, pierde su equipamiento e inventario, que quedan en su cadáver) y ve los Resultados de pérdida; si muere o extrae, ve sus Resultados igual que el resto. Al pulsar *Volver*, su participación ya está resuelta (cadáver, botín, resultados y progreso guardados) y el rol de Host pasa a otro jugador que siga activo y conectado dentro de la Dungeon. El Host saliente vuelve al pueblo sin esperar a los demás y la raid continúa sin pérdidas para quienes quedan.
>
> **Sin jugadores activos.** Si no queda ningún participante activo y conectado, la raid se cierra normalmente una vez completados los guardados pendientes.
>
> **Pérdida inesperada del Host.** Si el Host se desconecta de forma abrupta, el rol pasa igualmente a otro jugador activo dentro de la Dungeon. La raid, el equipamiento y el botín de los demás no dependen de una sola persona.
>
> **Aviso.** Los jugadores que quedan ven el aviso "{Nombre} salió. {Nuevo} es el nuevo Host."
>
> **Sin "Cancelar raid".** Ningún jugador puede cancelar ni abortar la raid de los demás.

## 5. Estado implementado (para que Game Design valide contra algo concreto)

| Regla | Dónde vive | Evidencia |
|---|---|---|
| Menú del Host: Abandonar con confirmación en dos pasos; sin Cancelar raid | FIX-49 | Pruebas automáticas; corrida manual informada por el usuario |
| El Host resuelve su participación y luego traspasa el rol (`HostHandoffPolicy`, `HostHandoffService`) | FIX-50 | Corrida multi-proceso con logs: Host derrotado → Resultados → *Volver* → traspaso, Town, la raid sigue |
| Pérdida abrupta del Host: se conserva la recuperación existente; el Host histórico se finaliza como `Aborted` si su salida no estaba resuelta | FIX-50 | Corrida multi-proceso con logs (Host + 2 Clientes, proceso del Host finalizado a la fuerza) |
| Aviso "{Nombre} salió. {Nuevo} es el nuevo Host." | FIX-50 | Visto por el usuario en la corrida |
| Compañero desconectado con presupuesto de reconexión lo conserva tras el traspaso; el presupuesto se pausa durante la ventana de recuperación | FIX-51 | Corrida multi-proceso con logs |
| Extraído con commit pendiente lo completa tras el traspaso | FIX-51 | Solo pruebas automáticas; **sin corrida en vivo** (bloqueada por un defecto previo de extracción multijugador, no relacionado) |
| Traspaso tras una extracción completada del Host | FIX-50 | **Sin corrida multi-proceso registrada** |
| Solo: Abandonar genera cadáver, muestra Resultados de pérdida y vuelve a Town; aviso de pérdida ante fallos técnicos | FIX-53 | Corrida Solo en el Editor con logs |

Referencias: `docs/fix-48_reporte_traspaso_host_salida_voluntaria.md` y los trackers `odd/tasks/fix-49-remove-cancel-raid.md`, `odd/tasks/fix-50-host-handoff.md`, `odd/tasks/fix-51-handoff-disconnected-pending.md`, `odd/tasks/fix-53-solo-abandon.md`.

## 6. Decisiones y preguntas para Game Design

Cada una indica el supuesto con el que se implementó. Se pide confirmar o corregir.

1. **Host Migration como requisito MVP.** ¿Se confirma que deja de ser "intención de diseño" y pasa a ser requisito del MVP? (Contratos de arquitectura anteriores la marcaban como no garantizada.)
2. **Abandono del Host.** Supuesto: mismas consecuencias que cualquier participante, sin penalización ni excepción propia. ¿Se confirma?
3. **Sin participantes activos.** Supuesto aceptado por el usuario en FIX-50 (tracker, L3): no hay traspaso y la raid cierra normalmente tras los guardados pendientes. La confirmación de Game Design sigue pendiente. ¿Es el comportamiento deseado, o el Host debería quedarse hasta que cierre la raid?
4. **Elección del nuevo Host.** Según el reporte FIX-48, la elección la hace Photon Fusion entre los clientes conectados; el proyecto no define un orden de prioridad (por ejemplo, el miembro de la Party del Host). ¿Se necesita un criterio de diseño?
5. **Host que pierde la conexión.** El Host histórico no tiene presupuesto de reconexión: si cae de forma abrupta se finaliza como `Aborted` y no puede volver a la raid, a diferencia de cualquier otro participante. ¿Debe tener el mismo derecho de reconexión que el resto? (Fuera del alcance de FIX-US-08; hoy no está soportado.)
6. **Aviso al resto.** ¿Se aprueba el texto "{Nombre} salió. {Nuevo} es el nuevo Host." y su duración actual (5 s por defecto, configurable)? El aviso se muestra en cualquier traspaso, incluida la pérdida abrupta.
7. **Presupuesto de reconexión durante la migración.** Decisión tomada en FIX-51: el presupuesto de un compañero desconectado se pausa mientras dura la ventana de recuperación y se reanuda en el nuevo Host. ¿Se confirma que el presupuesto no se consume durante el traspaso?
8. **Sala abierta mientras alguien puede reconectar.** Tras el traspaso la sala se mantiene abierta mientras un participante retenido pueda volver; luego se cierra. ¿Es coherente con las reglas de reconexión de Game Design?
9. **Asunto relacionado de FIX-53 que necesita a Game Design** (no es parte de §13): la ventana (valor N) en la que se devolvería el equipo si la raid falla antes de empezar. Quedó fuera de FIX-53 como seguimiento.

## 7. Criterio de cierre de la solicitud

Se considera registrada cuando el responsable de arquitectura la envía y anota en FIX-52 el canal, la fecha y el enlace, junto con esta decisión textual. La respuesta de Game Design no bloquea el cierre de FIX-52; sí condiciona cualquier cambio posterior a los contratos de arquitectura.
