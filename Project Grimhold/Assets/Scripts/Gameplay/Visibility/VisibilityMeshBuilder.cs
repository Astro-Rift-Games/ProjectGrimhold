using System.Collections.Generic;
using UnityEngine;

namespace ProjectGrimhold.Gameplay.Visibility
{
    /// <summary>
    /// Consume los datos matemáticos generados por VisibilityCalculator
    /// y los convierte en un Mesh y lo dibuja a modo de debug visual en el MVP.
    /// </summary>
    [RequireComponent(typeof(MeshFilter), typeof(MeshRenderer))]
    public sealed class VisibilityMeshBuilder : MonoBehaviour
    {
        [SerializeField] private VisibilityCalculator _calculator;
        
        [Tooltip("Transform utilizado como punto de emisión (ej. el jugador).")]
        [SerializeField] private Transform _originTransform;

        [Header("2D Sorting")]
        [SerializeField] private string _sortingLayerName = "Default";
        [SerializeField] private int _sortingOrder = 50;

        /// <summary>
        /// Provee acceso a la consulta geométrica del polígono calculado en este frame.
        /// Su ciclo de vida y actualización es gestionado internamente por este componente.
        /// </summary>
        public LosPolygonHandle LosHandle { get; } = new LosPolygonHandle();

        private Mesh _mesh;
        private MeshRenderer _meshRenderer;
        private readonly List<Vector3> _vertices = new List<Vector3>();
        private readonly List<int> _triangles = new List<int>();
        private readonly List<Vector2> _uvs = new List<Vector2>();

        private void Awake()
        {
            _mesh = new Mesh { name = "VisibilityMesh" };
            _mesh.MarkDynamic();
            GetComponent<MeshFilter>().mesh = _mesh;

            _meshRenderer = GetComponent<MeshRenderer>();
            _meshRenderer.sortingLayerName = _sortingLayerName;
            _meshRenderer.sortingOrder = _sortingOrder;
        }

        private void LateUpdate()
        {
            if (_calculator == null || _originTransform == null)
            {
                return;
            }

            Shader.SetGlobalVector("_GlobalVisibilityOrigin", (Vector4)_originTransform.position);

            // En la Fase 1 actualizamos por frame a modo de MVP.
            // En la Fase 5 esto se limitará por tiempo (ej. 30hz).
            _calculator.Calculate(_originTransform.position);
            
            // Actualizamos la abstracción geométrica para consumo de sistemas externos (ocultamiento de entidades)
            LosHandle.UpdatePolygon(_calculator.PolygonVertices, 1, _calculator.PolygonVertices.Count - 1);

            BuildMesh(_calculator.PolygonVertices);
        }

        private void BuildMesh(List<Vector3> polygonVertices)
        {
            if (polygonVertices == null || polygonVertices.Count < 3) 
                return;

            _vertices.Clear();
            _triangles.Clear();
            _uvs.Clear();

            // polygonVertices[0] es siempre el origen en espacio de mundo.
            Vector3 originWorld = polygonVertices[0];

            // Convertimos los vértices de mundo a espacio local
            for (int i = 0; i < polygonVertices.Count; i++)
            {
                Vector3 localPos = transform.InverseTransformPoint(polygonVertices[i]);
                _vertices.Add(localPos);

                // Setup de UVs para futuro Falloff: 'x' almacena la distancia al origen
                float distance = Vector3.Distance(originWorld, polygonVertices[i]);
                _uvs.Add(new Vector2(distance, 0f));
            }

            int vertexCount = _vertices.Count;
            // Triangulación tipo abanico (Triangle Fan) desde el índice 0
            for (int i = 1; i < vertexCount - 1; i++)
            {
                _triangles.Add(0);
                // Winding horario estándar en Unity
                _triangles.Add(i + 1);
                _triangles.Add(i);
            }
            
            // Cerrar el polígono conectando el último punto con el primero
            _triangles.Add(0);
            _triangles.Add(1);
            _triangles.Add(vertexCount - 1);

            _mesh.Clear();
            _mesh.SetVertices(_vertices);
            _mesh.SetTriangles(_triangles, 0);
            _mesh.SetUVs(0, _uvs);
            
            // Recalculamos bounds para que Unity no oculte el mesh por Frustum Culling
            // aunque el objeto en sí no se mueva.
            _mesh.RecalculateBounds();
        }
    }
}
