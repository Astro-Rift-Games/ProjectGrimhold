using UnityEngine;
using UnityEngine.Rendering;

namespace ProjectGrimhold.Gameplay.Visibility
{
    /// <summary>
    /// Administra una cámara auxiliar encargada de renderizar los polígonos de visibilidad
    /// hacia una RenderTexture, exponiéndola globalmente para los Shaders de consumo.
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public sealed class VisibilityMaskRenderer : MonoBehaviour
    {
        [Tooltip("Resolución de la RenderTexture de la máscara (ej. 512).")]
        [SerializeField] private int _textureResolution = 512;
        
        [Tooltip("Layer exclusivo para dibujar el mesh de visión (ej. 'VisibilityMask').")]
        [SerializeField] private LayerMask _maskLayer;

        [Tooltip("Tamaño de la cámara ortográfica que captura la visión (debe cubrir el ViewRadius).")]
        [SerializeField] private float _orthographicSize = 15f;

        private Camera _camera;
        private RenderTexture _renderTexture;

        public static RTHandle GlobalMaskRTHandle { get; private set; }

        private void Awake()
        {
            _camera = GetComponent<Camera>();
            
            // Configuración estricta de la cámara para la máscara
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Color.black;
            _camera.cullingMask = _maskLayer;
            _camera.orthographic = true;
            _camera.orthographicSize = _orthographicSize;
            _camera.depth = -100; // Queremos que renderice antes que las cámaras principales
            _camera.allowHDR = false;
            _camera.allowMSAA = false;
            _camera.nearClipPlane = -50f;
            _camera.farClipPlane = 50f;

            // Restauramos el Depth a 24 bits. El Renderer2D Pass de URP 2D lo requiere para funcionar,
            // de lo contrario tira error de "Fake or uninitialized surface".
            _renderTexture = new RenderTexture(_textureResolution, _textureResolution, 24, RenderTextureFormat.ARGB32);
            _renderTexture.name = "GlobalVisibilityMaskRT";
            _renderTexture.filterMode = FilterMode.Bilinear;
            _renderTexture.wrapMode = TextureWrapMode.Clamp;
            _renderTexture.Create();

            _camera.targetTexture = _renderTexture;
            GlobalMaskRTHandle = RTHandles.Alloc(_renderTexture);
        }

        private void LateUpdate()
        {
            if (_renderTexture != null)
            {
                // Exponer la textura para cualquier shader global (legacy pass)
                Shader.SetGlobalTexture("_GlobalVisibilityMask", _renderTexture);
                
                // Exponer parámetros de la cámara (Posición XY y Tamaño) para que los
                // shaders sepan cómo mapear sus World Position a UVs de esta textura.
                // UV = (WorldPos.xy - CameraPos.xy) / (OrthographicSize * 2) + 0.5
                Shader.SetGlobalVector("_GlobalVisibilityParams", new Vector4(
                    transform.position.x, 
                    transform.position.y, 
                    _camera.orthographicSize, 
                    0f));
            }
        }

        private void OnDestroy()
        {
            GlobalMaskRTHandle?.Release();
            GlobalMaskRTHandle = null;

            if (_renderTexture != null)
            {
                _renderTexture.Release();
                Destroy(_renderTexture);
            }
        }

        private void OnGUI()
        {
            if (_renderTexture == null) return;

            // Dibuja la textura cruda en la esquina superior izquierda de la pantalla
            // Esto permite verificar si la máscara se dibuja correctamente ignorando el shader y RenderGraph.
            GUI.Box(new Rect(10, 10, 256, 20), "Diagnostic: Mask RT");
            GUI.DrawTexture(new Rect(10, 30, 256, 256), _renderTexture, ScaleMode.ScaleToFit, false);
        }
    }
}
