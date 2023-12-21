using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using UnityEngine;

public class VisibleVerticesRaycaster : MonoBehaviour
{
    private Camera cam;    
    private Vector3 lastCamPos;
    private Quaternion lastCamRot;
    private float logTimer = 0f;
    private string lastInfo, output = "";
    // Köşeleri ve onların bilgilerini saklayan yapı
    Dictionary<Vector3, VertexInfo> vertexInfoMap = new Dictionary<Vector3, VertexInfo>();
    private HashSet<TriangleInfo> triangleInfos = new HashSet<TriangleInfo>();
    float sphereRadius = 0.1f; // Kürenin yarıçapını burada ayarlayabiliriz.

    void Start()
    {
        cam = GetComponent<Camera>();
        lastCamPos = cam.transform.position;
        lastCamRot = cam.transform.rotation;
    }

    public class VertexInfo
    {
        public Vector3 Vertex { get; set; }
        public GameObject Owner { get; set; }
        public int Order { get; set; }
        public float DistanceToCamera { get; set; }
        public bool IsVisible { get; set; }
        public List<int> ConnectedVertices { get; set; } = new List<int>();
        // ... Daha fazla özellik ekleyebilirsiniz.

        // Hash için gereken metotlar
        public override bool Equals(object obj)
        {
            if (obj is VertexInfo other)
            {
                return Owner.GetInstanceID() == other.Owner.GetInstanceID() && Vertex == other.Vertex;
            }

            return false;
        }

        public override int GetHashCode()
        {
            return GetVertexHash(Owner.GetInstanceID(), Vertex);
        }

        // Unique bir hash değeri oluşturmak için bir method
        private int GetVertexHash(int objectId, Vector3 vertex)
        {
            // String üzerinde GetHashcode'u kullanabiliriz
            string uniqueKey = string.Format("{0}_{1}_{2}_{3}", objectId, vertex.x, vertex.y, vertex.z);
            return uniqueKey.GetHashCode();
        }
    }

    public class TriangleInfo
    {
        public int[] VertexOrders { get; set; } = new int[3];
        public int Order { get; set; }

        // Hash için gereken metotlar
        public override bool Equals(object obj)
        {
            if (obj is TriangleInfo other)
            {
                // Üçgenler, köşelerinin sıra numaralarına bakılarak karşılaştırılır
                return VertexOrders.SequenceEqual(other.VertexOrders);
            }

            return false;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            // Sıra numaralarına göre benzersiz bir hash değeri üret
            hash = hash * 31 + VertexOrders[0];
            hash = hash * 31 + VertexOrders[1];
            hash = hash * 31 + VertexOrders[2];
            return hash;
        }
    }


    void Update()
    {
        HandleKeyPress();
    }

    bool IsVertexVisible(Vector3 vertexScreenPos, Vector3 vertexWorld)
    {
        if (vertexScreenPos.z <= 0)
        {
            lastInfo += "\nVertex " + vertexWorld.ToString("F2") + " is behind the camera.";
            return false;
        }

        if (vertexScreenPos.x < 0 || vertexScreenPos.y < 0 || vertexScreenPos.x > cam.pixelWidth || vertexScreenPos.y > cam.pixelHeight)
        {
            lastInfo += "\nVertex " + vertexWorld.ToString("F2") + " is outside the screen.";
            return false;
        }

        if (!IsSphereVisible(vertexWorld))
        {
            lastInfo += "\nSphere around vertex " + vertexWorld.ToString("F2") + " is not visible.";
            return false;
        }

        return true;
    }


    bool IsSphereVisible(Vector3 vertexWorld)
    {
        bool isVisible = false;
        float offset = 0.01f; // Offset miktarını buradan ayarlayabilirsiniz.

        // Köşe etrafındaki tüm noktaları kontrol ediyoruz.
        for (float x = -sphereRadius; x <= sphereRadius; x += sphereRadius / 2f)
        {
            for (float y = -sphereRadius; y <= sphereRadius; y += sphereRadius / 2f)
            {
                for (float z = -sphereRadius; z <= sphereRadius; z += sphereRadius / 2f)
                {
                    Vector3 spherePoint = new Vector3(x, y, z);
                    Vector3 pointWorld = vertexWorld + spherePoint;
                    Vector3 pointScreenPos = cam.WorldToScreenPoint(pointWorld);
                    Vector3 dir = pointWorld - cam.transform.position;
                    float dist = dir.magnitude;
                    dir /= dist;
                    RaycastHit hit;

                    // Offset'i raycast başlangıç noktasına ekliyoruz.
                    Vector3 rayStart = cam.transform.position + dir * offset;

                    if (pointScreenPos.z > 0 // Kürenin arkasındaki noktaları kontrol etmiyoruz.
                        && !(Physics.Raycast(rayStart, dir, out hit, dist)))
                    {
                        isVisible = true; // Kürenin herhangi bir noktası engellenmiyorsa ve kamera tarafından görülebiliyorsa, köşe görünür.
                    }
                }
            }
        }

        return isVisible;
    }

    void HandleKeyPress()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            // Collect information for each mesh in the scene
            vertexInfoMap.Clear();            
            triangleInfos.Clear();
            HashSet<VertexInfo> uniqueVertices = new HashSet<VertexInfo>();
            int vertexOrder = 0;
            int triangleOrder = 0;
            int visibleVertexCount = 0;

            // Here we first go through each mesh and collect all the vertices
            foreach (MeshRenderer meshRenderer in GameObject.FindObjectsOfType<MeshRenderer>())
            {
                GameObject obj = meshRenderer.gameObject;
                Mesh mesh = null;

                if (obj.TryGetComponent<MeshFilter>(out MeshFilter meshFilter))
                {
                    mesh = meshFilter.mesh;
                }
                else if (obj.TryGetComponent<MeshCollider>(out MeshCollider meshCollider))
                {
                    mesh = meshCollider.sharedMesh;
                }

                if (mesh != null)
                {
                    Vector3[] vertices = mesh.vertices;
                    foreach (Vector3 vertex in vertices)
                    {
                        Vector3 vertexWorld = obj.transform.TransformPoint(vertex);
                        float distanceToCamera = Vector3.Distance(vertexWorld, cam.transform.position);

                        // Check if the vertex is already in the map
                        if (!vertexInfoMap.ContainsKey(vertexWorld))
                        {
                            VertexInfo vertexInfo = new VertexInfo
                            {
                                Vertex = vertexWorld,
                                Owner = obj,
                                Order = vertexOrder++,
                                DistanceToCamera = distanceToCamera,
                            };
                            uniqueVertices.Add(vertexInfo);
                            vertexInfoMap.Add(vertexWorld, vertexInfo);
                        }
                    }

                    // Check visibility of vertices                    
                    foreach (var vertexInfo in vertexInfoMap.Values)
                    {
                        Vector3 vertexScreenPos = cam.WorldToScreenPoint(vertexInfo.Vertex);
                        vertexInfo.IsVisible = IsVertexVisible(vertexScreenPos, vertexInfo.Vertex);
                    }
                }
            }

            // Then we go through each mesh again to create the triangles and fill ConnectedVertices
            foreach (MeshRenderer meshRenderer in GameObject.FindObjectsOfType<MeshRenderer>())
            {
                GameObject obj = meshRenderer.gameObject;
                Mesh mesh = null;

                if (obj.TryGetComponent<MeshFilter>(out MeshFilter meshFilter))
                {
                    mesh = meshFilter.mesh;
                }
                else if (obj.TryGetComponent<MeshCollider>(out MeshCollider meshCollider))
                {
                    mesh = meshCollider.sharedMesh;
                }

                if (mesh != null)
                {
                    for (int i = 0; i < mesh.triangles.Length; i += 3)
                    {
                        Vector3 v1 = obj.transform.TransformPoint(mesh.vertices[mesh.triangles[i]]);
                        Vector3 v2 = obj.transform.TransformPoint(mesh.vertices[mesh.triangles[i + 1]]);
                        Vector3 v3 = obj.transform.TransformPoint(mesh.vertices[mesh.triangles[i + 2]]);

                        if (vertexInfoMap.ContainsKey(v1) && vertexInfoMap.ContainsKey(v2) && vertexInfoMap.ContainsKey(v3))
                        {
                            // Add the triangle
                            TriangleInfo triangleInfo = new TriangleInfo
                            {
                                VertexOrders = new int[]
                                {
                                vertexInfoMap[v1].Order,
                                vertexInfoMap[v2].Order,
                                vertexInfoMap[v3].Order
                                },
                                Order = triangleOrder++
                            };
                            triangleInfos.Add(triangleInfo);

                            // Now that we have all the VertexInfo instances, we can fill ConnectedVertices
                            VertexInfo vertexInfo1 = vertexInfoMap[v1];
                            VertexInfo vertexInfo2 = vertexInfoMap[v2];
                            VertexInfo vertexInfo3 = vertexInfoMap[v3];

                            if (!vertexInfo1.ConnectedVertices.Contains(vertexInfo2.Order))
                                vertexInfo1.ConnectedVertices.Add(vertexInfo2.Order);
                            if (!vertexInfo1.ConnectedVertices.Contains(vertexInfo3.Order))
                                vertexInfo1.ConnectedVertices.Add(vertexInfo3.Order);
                            if (!vertexInfo2.ConnectedVertices.Contains(vertexInfo1.Order))
                                vertexInfo2.ConnectedVertices.Add(vertexInfo1.Order);
                            if (!vertexInfo2.ConnectedVertices.Contains(vertexInfo3.Order))
                                vertexInfo2.ConnectedVertices.Add(vertexInfo3.Order);
                            if (!vertexInfo3.ConnectedVertices.Contains(vertexInfo1.Order))
                                vertexInfo3.ConnectedVertices.Add(vertexInfo1.Order);
                            if (!vertexInfo3.ConnectedVertices.Contains(vertexInfo2.Order))
                                vertexInfo3.ConnectedVertices.Add(vertexInfo2.Order);
                        }
                        else
                        {
                            UnityEngine.Debug.LogWarning("Bir veya daha fazla köşe vertexInfoMap içinde bulunamadı. Bu üçgen atlanıyor.");
                        }
                    }

                }

            }

            visibleVertexCount = vertexInfoMap.Values.Count(vertexInfo => vertexInfo.IsVisible);

            lastInfo = "Visible Vertices: " + visibleVertexCount + "\n";
            foreach (var vertexInfo in vertexInfoMap.Values)
            {
                if (vertexInfo.IsVisible)
                {
                    lastInfo += $"Order: {vertexInfo.Order}, Position: {vertexInfo.Vertex}\n";
                }
            }

            Vector3 bottomLeft = cam.ViewportToWorldPoint(new Vector3(0, 0, cam.nearClipPlane));
            Vector3 topLeft = cam.ViewportToWorldPoint(new Vector3(0, 1, cam.nearClipPlane));
            Vector3 topRight = cam.ViewportToWorldPoint(new Vector3(1, 1, cam.nearClipPlane));
            Vector3 bottomRight = cam.ViewportToWorldPoint(new Vector3(1, 0, cam.nearClipPlane));
            Vector3 frustumApex = cam.transform.position; // Frustum'un tepe noktası

            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string folderPath = @"C:\Users\Osman\My project\TrainingData\";

            // Use the timestamp as the filename
            string filename = "honolulu_" + $"log_{timestamp}.txt";

            // Create the full file path
            string filePath = Path.Combine(folderPath, filename);

            // Create a new StringBuilder to build the output
            StringBuilder sb = new StringBuilder();

            // Write camera info
            sb.AppendLine("Camera Info:");
            sb.AppendLine($"Bottom Left: {bottomLeft}");
            sb.AppendLine($"Top Left: {topLeft}");
            sb.AppendLine($"Top Right: {topRight}");
            sb.AppendLine($"Bottom Right: {bottomRight}");
            sb.AppendLine($"Frustum Apex: {frustumApex}"); // Frustum'un tepe noktası

            // Write vertices info
            sb.AppendLine("All Vertices:");            
            foreach (var vertexInfo in vertexInfoMap.Values)
            {
                sb.AppendLine(
                    $"Order: {vertexInfo.Order}, " +
                    $"Position: {vertexInfo.Vertex}, " +
                    $"Owner ID: {vertexInfo.Owner.GetInstanceID()}, " +
                    $"Distance to Camera: {vertexInfo.DistanceToCamera}, " +
                    $"Connected Vertices: {string.Join(", ", vertexInfo.ConnectedVertices)}");
            }

            // Write visible vertices info
            sb.AppendLine("Visible Vertices:");
            foreach (var vertexInfo in vertexInfoMap.Values)
            {
                if (vertexInfo.IsVisible)
                {
                    sb.AppendLine($"Order: {vertexInfo.Order}, Position: {vertexInfo.Vertex}");
                }
            }

            // Write the built string to a new file
            File.WriteAllText(filePath, sb.ToString());

            UnityEngine.Debug.Log(lastInfo);

        }
    }
}

