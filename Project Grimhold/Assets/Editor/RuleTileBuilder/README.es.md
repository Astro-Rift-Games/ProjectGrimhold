# Guía de uso: Rule Tile Builder

[English guide](README.md)

Crea un RuleTile seleccionando sprites y asignándolos a funciones visuales. No necesitas escribir código ni configurar matrices para utilizar las plantillas.

Abre **Tools > Grimhold > Rule Tile Builder** en Unity. La interfaz conserva sus nombres en inglés; esta guía indica el nombre exacto de cada control y explica qué hace.

**Ayuda al pasar el cursor:** mantén el puntero sobre botones, pestañas, plantillas, posiciones del escenario, pinceles o campos para ver un tooltip en inglés, igual que en el Inspector nativo. También hay ayudas sobre las celdas de la matriz avanzada y las opciones nativas de variantes, animación y collider. Los botones **Clear** explican qué vacían antes de utilizarlos. Esta guía permanece en español.

> **Borrador primero, asset después.** La herramienta no modifica el RuleTile original hasta que guardas. Tampoco corta imágenes, crea arte ni cambia escenas, importadores o colisiones existentes.

## 1. Añadir los sprites

En **1 Sprites**, utiliza el panel izquierdo **Sprite library**:

- Arrastra sprites individuales o una textura ya cortada a **Drop sprites / sliced textures here**.
- También puedes elegir una fuente con **Add source**, o seleccionarla en la ventana Project y pulsar **Add selected assets**.
- Busca piezas por nombre con **Search**.
- Haz clic para seleccionar un sprite. Mantén **Ctrl** en Windows o **Cmd** en macOS mientras haces clic para seleccionar varias variantes.

Si la textura no contiene sprites importados, la herramienta lo indica. Debes cortarla previamente con el Sprite Editor de Unity; el Builder no modifica su configuración de importación.

### Elegir el sprite de respaldo

El **Fallback sprite** aparece cuando no existe una regla para una combinación de conexiones. Es obligatorio: evita guardar un tile con una salida predeterminada ausente.

Selecciona una pieza neutra en la biblioteca y pulsa **Use selected sprite as fallback**, o asígnala directamente al campo **Fallback sprite**.

**Default collider** define el collider predeterminado del asset generado:

| Opción | Uso |
| --- | --- |
| `None` | Sin collider. Es la opción inicial y la utilizada en el ejemplo de suelo. |
| `Sprite` | Solicita el collider basado en el sprite. |
| `Grid` | Solicita el collider de la celda. |

Esto no añade componentes a escenas ni modifica colliders existentes. La previsualización no simula colisiones físicas. Las reglas pueden tener su propio collider en el panel avanzado.

## 2. Elegir cómo se conecta la superficie

En **2 Template**, elige según las piezas que realmente tengas:

| Plantilla | Qué permite | Cuándo empezar por ella |
| --- | --- | --- |
| **Continuous floor** | Una superficie continua; una pieza, con variantes opcionales. | Suelos que no necesitan cambiar de dibujo en los límites. |
| **Orthogonal connections (16)** | Conexiones arriba, abajo, izquierda y derecha. Incluye bordes, extremos y uniones. | Tilesets sin distinción de esquinas interiores diagonales. |
| **Full contour & inner corners (47)** | También distingue diagonales cuando ambos lados adyacentes conectan. | Contornos con esquinas interiores, exteriores y sus combinaciones. |

**No necesitas disponer de las 16 o 47 piezas para empezar.** Puedes dejar funciones opcionales sin asignar; esas conexiones utilizarán el respaldo y generarán advertencias.

> Cambiar de plantilla puede reemplazar todas las reglas del borrador, incluidas las personalizadas. La herramienta pide confirmación. No cambies de plantilla solamente para sustituir un sprite.

## 3. Asignar sprites a funciones visuales

En **3 Assign pieces** eliges dónde aparecería el dibujo dentro de un escenario ilustrativo. No necesitas interpretar matrices: pulsa, por ejemplo, el borde superior de una habitación para elegir **Top edge**.

Para asignar una pieza:

1. Selecciona su sprite en la biblioteca izquierda.
2. En **Example**, elige una habitación, pasillo o unión y haz clic en la posición donde debe aparecer ese dibujo.
3. Revisa el nombre y la función de la pieza seleccionada y pulsa **Assign selected**. También puedes arrastrar sprites desde la biblioteca o Project directamente sobre una celda sólida para asignarlos.

Si seleccionas varios sprites para una regla nueva, se crea una salida **Random**: Unity elige variantes según la posición. En una regla que ya existe se conserva su configuración Single, Random o Animation; puedes cambiarla desde **Details**. El escenario ilustrativo muestra el primer sprite para identificar el arte, no simula variantes ni animaciones.

- **Clear**, en la pieza seleccionada, elimina su regla y vuelve al respaldo para todas las posiciones equivalentes.
- **Details** abre su configuración avanzada.
- **Clear**, en la biblioteca izquierda, vacía la biblioteca y la selección; no elimina reglas ya asignadas ni sprites del proyecto.

### Reconocer las posiciones

| Señal | Significado |
| --- | --- |
| Punto naranja | Pieza pendiente: muestra el respaldo, o una celda plana si todavía no lo elegiste. |
| Contorno cian | Todas las posiciones que usan la misma regla seleccionada. Cambian juntas al asignar, reemplazar o borrar. |
| Contorno blanco interior | Posición exacta que elegiste. |
| Números en los estudios de patios | Posiciones destacadas para combinaciones especiales; cualquier celda sólida se puede seleccionar. |

**Example** incluye habitación rectangular, pasillos horizontales/verticales, extremos, pieza aislada, uniones en cruz/T, habitación en L y habitaciones con huecos. **Courtyards & recesses** aporta estudios de terreno para las combinaciones restantes: entre todos los ejemplos puedes acceder a las 16 o 47 piezas.

Pulsa **Next unassigned piece** para ir directamente a una función pendiente, sin recorrer listas de matrices. **Tile size** amplía o reduce el dibujo con filtrado puntual, sin modificar importadores. El escenario tiene su propio desplazamiento para mantener accesibles los controles de asignación. Despliega **Fallback & collider** cuando necesites editar el respaldo o collider desde este paso.

**Asignar no es probar:** este escenario sirve para ubicar el arte. No representa la evaluación de prioridad, transformaciones, Random o Animation del RuleTile. **4 Paint & test** sigue siendo el Tilemap real de Unity y se refresca al cambiar las asignaciones. Comprueba allí el resultado final, especialmente después de editar Advanced.

La herramienta no reconoce automáticamente la función de un sprite, no completa geometría ausente y no gira ni refleja el arte salvo que lo habilites expresamente en Advanced.

## 4. Pintar una habitación de prueba

En **4 Paint & test**, utiliza los ejemplos **Rectangle**, **L**, **Hole**, **Corridor** e **Isolated**. **Clear** vacía únicamente la grilla temporal.

| Control | Acción |
| --- | --- |
| **Paint surface** + clic o arrastre izquierdo | Pinta con el RuleTile del borrador. |
| **Erase** + clic o arrastre izquierdo | Borra celdas. |
| Clic o arrastre derecho | Borra, independientemente del pincel seleccionado. |
| **Other tile** + clic izquierdo | Coloca otra superficie, representada por un patrón cuadriculado. |
| **Highlight fallback cells** | Marca en naranja las celdas que utilizan el respaldo. |

Al pasar el cursor por una celda se indican su regla visual y, si corresponde, su regla de animación.

Comprueba especialmente los bordes, las esquinas, los huecos y los pasillos estrechos. Si una celda aparece con respaldo, vuelve a **Assign pieces** para completar la función que falta, o acepta deliberadamente ese resultado.

La grilla utiliza un **Tilemap real y aislado** con el borrador. No modifica el Tilemap de tu escena. Si existen errores bloqueantes, como un respaldo ausente, debes resolverlos antes de previsualizar.

## 5. Generar o guardar

En **5 Save**, revisa los mensajes:

- **Errores:** bloquean el guardado. Ejemplos: respaldo ausente, referencias inválidas, reglas activas sin sprites o parámetros inválidos. Debes corregirlos; confirmar no los ignora.
- **Advertencias:** permiten guardar tras una confirmación explícita. Ejemplos: piezas opcionales sin asignar, conexiones que usan respaldo, reglas duplicadas o parcialmente ocultas por prioridad.

Para crear un asset nuevo, introduce **Asset name**, pulsa **Generate native RuleTile…** y elige un nombre y carpeta dentro de Assets. Una ruta ocupada no se sustituye.

Para actualizar uno abierto, pulsa **Save changes to original asset**. Se conserva su GUID y las referencias que otros objetos tengan a ese asset.

Después de guardar, puedes usar **Select saved asset in Project** y arrastrar el asset a la **Tile Palette** estándar de Unity. Pintar desde esa paleta sobre una escena ya es una operación tuya en Unity, no una modificación automática del Builder.

## Ejemplo rápido con Catacombs

Las rutas de esta sección son relativas a la raíz del proyecto Unity.

### Crear un suelo desde cero

1. Abre el Builder y pulsa **New** si necesitas un borrador nuevo.
2. Añade `Assets/Art/Tilesets/Catacombs/Catacombs-FloorVariantsTileset.png` a la biblioteca. Ya está cortado.
3. Selecciona `Catacombs-FloorVariantsTileset_4` y úsalo como respaldo. Mantén **Default collider = None** para reproducir el ejemplo.
4. En **Template**, elige **Continuous floor**.
5. Selecciona los sprites `Catacombs-FloorVariantsTileset_0` y `_1` con Ctrl/Cmd-clic. Haz clic en cualquier celda de **Room** y pulsa **Assign selected** para la pieza **Continuous floor**.
6. Prueba **Rectangle** y **Hole** en la grilla. Verás variaciones de suelo, no paredes ni bordes decorativos automáticos.
7. Genera un asset con un nombre nuevo, por ejemplo `MiSueloCatacombs.asset`.

También puedes abrir el ejemplo existente en **Open existing**:

`Assets/Art/Tilesets/Catacombs/RuleTiles/CatacombsFloorRuleTile.asset`

> Las paredes y los arcos de Catacombs contienen varias piezas. Un RuleTile elige un sprite por celda; no construye por sí solo una pared de varias filas. Los sufijos numéricos de los sprites no identifican automáticamente sus conexiones.

## Reabrir, completar y descartar cambios

- **Open existing:** abre un RuleTile para editar una copia temporal. Las reglas personalizadas conservan su contenido y orden.
- **Reload:** vuelve a cargar el original. Si el borrador tiene cambios, elige **Save**, **Cancel** o **Discard**. Discard descarta el trabajo pendiente.
- **New:** inicia otro borrador, con la misma protección para cambios sin guardar.
- **Undo/Redo:** permite revertir o rehacer ediciones. Después de deshacer una actualización del asset, persiste ese estado mediante el guardado estándar de Unity.

Al reabrir se intenta reconocer la plantilla a partir de las reglas nativas. Si hay pocas reglas, varias plantillas pueden ser compatibles y se elige la más simple. Esto no elimina reglas: **Advanced** permite ver todas las almacenadas.

Si el original cambia fuera del Builder, su actualización se bloquea para no sobrescribirlo. **Reload no combina versiones**: antes de descartar el borrador, revisa qué cambios necesitas rehacer sobre la versión actual.

Si el RuleTile es utilizado por un **RuleOverrideTile**, el Builder bloquea la actualización para no regenerar dependientes silenciosamente. Utiliza el Inspector estándar para ese caso.

Guarda antes de reiniciar Unity. El asset guardado contiene la configuración; la habitación de prueba es temporal y no se guarda como escena.

## Advanced: solo si necesitas personalizar reglas

No es necesario para el primer suelo. Permite añadir, duplicar, eliminar y reordenar reglas, editar su matriz y configurar sus salidas nativas:

| Salida | Resultado |
| --- | --- |
| **Single** | Un sprite para esa regla. |
| **Random** | Variantes de sprites según la posición. |
| **Animation** | Una secuencia de sprites con velocidades mínima y máxima. |

En la matriz, verde significa misma superficie (`This`), rojo otra superficie o vacío (`Not This`) y en blanco cualquiera (`Don't Care`). El centro controla las transformaciones permitidas. Activa rotación o reflejo únicamente si tu arte los admite.

**Move earlier / Move later** cambia la prioridad visual: Unity prueba las reglas de arriba abajo. Las animaciones se buscan de manera independiente; una regla visualmente oculta puede seguir aportando animación.

La herramienta admite el RuleTile estándar de grilla cuadrada con vecindad 3×3. Rechaza tipos derivados o vecindades no representables en lugar de eliminar información al convertirlas.

## Si algo no sale como esperabas

| Situación | Qué revisar |
| --- | --- |
| La textura no muestra piezas | Comprueba que ya esté importada y cortada como sprites. El Builder no la corta. |
| No puedo generar | Revisa los errores en Save, especialmente el respaldo y los sprites de reglas activas. |
| Aparecen celdas naranjas | Esa combinación usa respaldo. Asigna la pieza correspondiente desde un ejemplo o confirma que aceptas ese resultado. |
| Un sprite nunca aparece | Revisa si una regla anterior cubre sus conexiones; consulta las advertencias de prioridad. |
| Una esquina está mal | Selecciona esa posición en el ejemplo y comprueba la pieza elegida. Para esquinas interiores diagonales necesitas la plantilla completa y arte compatible. |
| Hay animación aunque esperaba otra regla | Unity busca animaciones independientemente de la prioridad del sprite visual. Revisa las reglas Animation. |
| El guardado informa de cambios externos | No sobrescribas a ciegas. Revisa el original y decide si vas a descartar y reconstruir el borrador desde Reload. |

## Alcance de las verificaciones

En la implementación del 19 de octubre de 2026 se observaron compilación correcta, **43 tests EditMode aprobados**, comprobaciones de persistencia/GUID/Undo y una prueba de 48 celdas con el GridBrush nativo usando el ejemplo Catacombs.

Siguen siendo comprobaciones manuales: reinicio real del Editor, recorrido completo por ratón en Tile Palette, interacción con los diálogos y aprobación visual/de usabilidad. Los tests y la recarga del asset no sustituyen esas verificaciones.

Tras añadir los tooltips se aprobaron **104 tests EditMode**: los 43 existentes y 61 casos de ayuda contextual y conservación de etiquetas. La aparición visual de los tooltips al mantener el cursor sigue siendo una comprobación manual.

Con la asignación sobre escenarios se aprobaron **119 tests EditMode** (0 omitidos): cobertura de las 16/47 funciones, correspondencia de cada celda con un Tilemap nativo, asignación/reemplazo/borrado, variantes, navegación de pendientes, persistencia y Undo/Redo. Se ejercitaron clic y drop mediante eventos reales de EditorWindow con Catacombs, en ventanas de 860×660 y 1220×1000, sin cambios en el importador, la escena o la selección. Se inspeccionaron capturas nativas de los ejemplos; esto no sustituye una evaluación de usabilidad por una persona principiante.
