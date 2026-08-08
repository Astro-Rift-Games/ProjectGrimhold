# Arquitectura del Sistema de Visibilidad (Fog of War)

## Contexto y Objetivo
El sistema provee un mecanismo de visibilidad dinámica (estilo *Among Us* / *Line of Sight*) para un juego multijugador 2D Top-Down desarrollado en Unity (Photon Fusion 2.1).
Su objetivo es oscurecer el mapa fuera del campo de visión del jugador local y ocultar entidades, manteniéndose totalmente desacoplado de la simulación de red autoritativa.

## Principio de Diseño Principal: Separación Dual
La arquitectura asume que la **representación matemática** de la visibilidad y su **representación estética/gráfica** deben vivir en capas separadas para garantizar determinismo lógico, alto rendimiento y flexibilidad visual (como sombras suaves).

Existen dos fuentes de verdad consultables en el sistema:
1. **Lógica (CPU):** Polígono matemático exacto con bordes duros.
2. **Gráfica (GPU):** Textura global suavizada que representa la luz en el mundo.

---

## 1. Capa Lógica (Cálculo Matemático)

Encargada de resolver con precisión milimétrica qué puntos del espacio son visibles desde un origen dado, evadiendo colisionadores. Se ejecuta íntegramente en CPU sin alocación de memoria por frame.

### Componentes:
* **`VisibilitySettings` (ScriptableObject):**
  Almacena los parámetros compartidos como el `ViewRadius`, la capa física de obstáculos (`ObstacleLayer`) y la cantidad de rayos para bordes redondeados (`BorderRays`).
  
* **`VisibilityObstacleCache`:**
  Índice estático de obstáculos. En `Awake`, escanea la escena buscando Box, Polygon y Composite Colliders en el layer de obstáculos. Extrae todos sus vértices a espacio de mundo y los almacena en una única lista plana. Provee el método ultrarrápido `GetVerticesInRange` mediante chequeo de distancia cuadrática.
  
* **`VisibilityCalculator`:**
  El motor core. Realiza los siguientes pasos:
  1. Consulta al caché los vértices que están dentro del radio de visión.
  2. Calcula el ángulo hacia cada vértice.
  3. Dispara 3 rayos por cada vértice (`ángulo`, `ángulo - 0.0001`, `ángulo + 0.0001`) y rayos adicionales para el borde periférico.
  4. Normaliza todos los ángulos (de `-PI` a `PI`) para evitar superposiciones y cruces al ordenar.
  5. Ordena los puntos de impacto angularmente para garantizar un polígono *Star-Shaped* (Estrella convexa respecto al origen).

---

## 2. Capa Visual (Generación de la Máscara)

Toma los datos puros generados por la Capa Lógica y los transforma en información consumible por la tarjeta gráfica (GPU).

### Componentes:
* **`VisibilityMeshBuilder`:**
  Genera un polígono (Unity `Mesh`) en tiempo real uniendo los vértices ordenados entregados por el Calculator, utilizando una triangulación tipo abanico (*Triangle Fan*) alrededor del índice 0 (origen).
  
* **`VisibilityMaskRenderer`:**
  Una cámara ortográfica independiente (Z: -100) acoplada al sistema visual que **solo** renderiza el layer `VisibilityMask`. Dibuja el Mesh generado sobre una `RenderTexture` (ARGB32) con fondo negro. Expone globalmente dos variables al pipeline de renderizado:
  * `_GlobalVisibilityMask`: La textura dinámica.
  * `_GlobalVisibilityParams`: Vector con la posición X/Y de la cámara y su `OrthographicSize` para permitir el mapeo de coordenadas UV (World to Texture) en los shaders.

* **Shader de Emisión (`VisibilityMesh.shader`):**
  Un shader *Unlit* básico utilizado por el MeshBuilder para imprimir un color blanco sólido en la RenderTexture sin verse afectado por luces 3D.

---

## Decisiones Arquitectónicas Justificadas

1. **Raycasts dirigidos vs Raycasts radiales:**
   En lugar de disparar 360 rayos uniformemente, se disparan rayos **exclusivamente a los vértices de los obstáculos** presentes en el caché. Esto reduce exponencialmente las llamadas físicas conservando precisión absoluta en las esquinas.

2. **RenderTexture vs Stencil Buffer:**
   Se eligió utilizar una Render Texture intermedia para almacenar la máscara en lugar de escribir directamente al Stencil Buffer. El Stencil solo soporta valores booleanos (blanco/negro), impidiendo aplicar un difuminado (Falloff/Penumbra) orgánico a la luz.

3. **Culling mediante Polígono vs Raycasts Individuales (Próxima fase):**
   Las entidades que decidan ocultarse (Loot, Enemigos) no lanzarán `Linecasts` en su propio código. Consultarán al `VisibilityCalculator` (Polígono) si su posición está iluminada. Al ser un polígono radial ordenado, esto permite resolver la visibilidad mediante una **Búsqueda Binaria de Ángulos** en `O(log N)`, garantizando sincronía visual-lógica sin impacto en el rendimiento.

4. **Desacoplamiento GPU/CPU:**
   Las decisiones de *Gameplay* no deben depender de *AsyncGPUReadback* ni lectura de texturas para evitar lags y cuellos de botella. La representación visual (Shader) y la lógica (Mesh/Raycast) operan en paralelo utilizando el mismo set de datos de origen.
