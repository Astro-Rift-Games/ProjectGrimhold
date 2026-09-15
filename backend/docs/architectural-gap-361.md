# Gap Arquitectónico: Producción de Resultados Autoritativos en Raids (Issue #361)

## Contexto Actual

A partir de la resolución del ticket #361, el flujo de extracción en el backend se ha endurecido para **no confiar** en los datos enviados por el cliente (`items`, `progression`, `preparedEquipment`). El servidor ahora requiere la existencia de un `AuthoritativeExtractionResult` generado de forma independiente para permitir una extracción válida.

## El Gap

Actualmente, las raids de Project Grimhold corren utilizando **Photon Fusion** en modo `GameMode.Host`. Esto significa que uno de los jugadores actúa como servidor (Host) de la partida. 

**No hay ningún proceso de servidor dedicado (Server-Side) controlado por Astro Rift Games simulando o validando la raid en tiempo real.**

Por lo tanto, hoy **no existe una fuente de verdad "del lado servidor" genuinamente confiable** para producir el `AuthoritativeExtractionResult`. Cualquier productor que intente enviar estos datos basándose en los eventos de la partida estará, en última instancia, recibiendo información de la máquina de un jugador (el Host), la cual es inherentemente manipulable por el usuario.

El único productor de resultados de extracción que existe actualmente en el backend es el endpoint `/debug/mock-fusion-result`, el cual es un mock puro diseñado para propósitos de prueba en desarrollo/staging y se encuentra **inhabilitado en producción**.

## Opciones para cerrar la brecha

Para resolver este problema arquitectónico y lograr verdadera autoridad del servidor, se presentan las siguientes opciones a evaluar en futuras iniciativas:

### 1. Migrar a Servidor Dedicado (Dedicated Server)
- **Implementación:** Migrar las raids a `GameMode.Server` de Photon Fusion, desplegando instancias de servidores dedicados (headless) que el equipo controla.
- **Ventajas:** Es la solución más robusta y estándar en la industria para prevenir trampas. El servidor dedicado será el único responsable de compilar el botín y la experiencia de los jugadores al terminar la raid e informar directamente al backend.
- **Desventajas:** Alto costo de infraestructura y complejidad de orquestación (spawning de servidores bajo demanda).

### 2. Validación Híbrida / Simulación Paralela (Replay)
- **Implementación:** Mantener el modo `GameMode.Host`, pero hacer que los clientes envíen un registro detallado de las entradas/eventos. Un microservicio asíncrono simula la partida rápidamente para verificar que el resultado reportado por el Host es lógicamente posible antes de emitir el `AuthoritativeExtractionResult`.
- **Ventajas:** Menor costo de infraestructura en tiempo real comparado con servidores dedicados para todas las partidas.
- **Desventajas:** Extremadamente costoso y complejo de construir y mantener (requiere determinismo perfecto y reconciliación compleja).

### 3. Aceptar el Riesgo Temporal (MVP)
- **Implementación:** Utilizar el modo Host y permitir que el Host genere un payload firmado que actúe temporalmente como un `AuthoritativeExtractionResult` (creando un productor "semi-confiable" que valida la firma del Host).
- **Ventajas:** Bajo esfuerzo de implementación, permite lanzar el juego rápido.
- **Desventajas:** Se asume el riesgo consciente de que los jugadores con conocimientos técnicos puedan manipular la memoria de su cliente Host para inyectar resultados falsos.

## Conclusión

Por el momento, la infraestructura actual no permite una resolución definitiva a las vulnerabilidades de extracción debido a la topología de red elegida para las raids (Host-Client). Cualquier cambio futuro requerirá una decisión a nivel de arquitectura y red que excede la lógica de validación de APIs del backend.
