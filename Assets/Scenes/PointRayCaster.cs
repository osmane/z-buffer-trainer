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
    // Köþeleri ve onlarýn bilgilerini saklayan yapý
    Dictionary<Vector3, VertexInfo> vertexInfoMap = new Dictionary<Vector3, VertexInfo>();
    private HashSet<TriangleInfo> triangleInfos = new HashSet<TriangleInfo>();
    float sphereRadius = 0.1f; // Kürenin yarýçapýný burada ayarlayabiliriz.

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
        public bool behindAMesh { get; set; }
        public bool outOfFrame { get; set; }
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

        // Unique bir hash deðeri oluþturmak için bir method
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
                // Üçgenler, köþelerinin sýra numaralarýna bakýlarak karþýlaþtýrýlýr
                return VertexOrders.SequenceEqual(other.VertexOrders);
            }

            return false;
        }

        public override int GetHashCode()
        {
            int hash = 17;
            // Sýra numaralarýna göre benzersiz bir hash deðeri üret
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
            vertexInfoMap[vertexWorld].outOfFrame = true;  // Görünmez köþenin kamera kadrajýnýn dýþýnda olduðunu iþaretleyin
            return false;
        }

        if (!IsSphereVisible(vertexWorld))
        {
            lastInfo += "\nSphere around vertex " + vertexWorld.ToString("F2") + " is not visible.";
            vertexInfoMap[vertexWorld].behindAMesh = true; // Görünmez köþenin bir mesh'in arkasýnda olduðunu iþaretleyin
            return false;
        }

        return true;
    }


    bool IsSphereVisible(Vector3 vertexWorld)
    {
        bool isVisible = false;
        float offset = 0.01f; // Offset miktarýný buradan ayarlayabilirsiniz.

        // Köþe etrafýndaki tüm noktalarý kontrol ediyoruz.
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

                    // Offset'i raycast baþlangýç noktasýna ekliyoruz.
                    Vector3 rayStart = cam.transform.position + dir * offset;

                    if (pointScreenPos.z > 0 // Kürenin arkasýndaki noktalarý kontrol etmiyoruz.
                        && !(Physics.Raycast(rayStart, dir, out hit, dist)))
                    {
                        isVisible = true; // Kürenin herhangi bir noktasý engellenmiyorsa ve kamera tarafýndan görülebiliyorsa, köþe görünür.
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
                            UnityEngine.Debug.LogWarning("Bir veya daha fazla köþe vertexInfoMap içinde bulunamadý. Bu üçgen atlanýyor.");
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
            Vector3 frustumApex = cam.transform.position; // Frustum'un tepe noktasý

            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");            

            // Use the timestamp as the filename but change the extension to .csv
            string filename = "honolulu_" + $"log_{timestamp}.csv";

            // For the TrainingControl folder
            string controlFolderPath = @"C:\Users\Osman\My project\TrainingControl\";
            string controlFilePath = Path.Combine(controlFolderPath, filename);

            // Create a new StringBuilder to build the output for the control file
            StringBuilder controlSb = new StringBuilder();

            // Write the column headers for the control file
            string columns = "Order," +
                "Position_x," +
                "Position_y," +
                "Position_z," +
                "Owner_ID," +
                "Distance_to_Camera," +
                "Connected_Vertices," +
                "Bottom_Left_x," +
                "Bottom_Left_y," +
                "Bottom_Left_z," +
                "Top_Left_x," +
                "Top_Left_y," +
                "Top_Left_z," +
                "Top_Right_x," +
                "Top_Right_y," +
                "Top_Right_z," +
                "Bottom_Right_x," +
                "Bottom_Right_y," +
                "Bottom_Right_z," +
                "Frustum_Apex_x," +
                "Frustum_Apex_y," +
                "Frustum_Apex_z," +
                "Is_Visible," +
                "Out_Of_Frame," +
                "BehindAMesh";

            controlSb.AppendLine(columns);

            foreach (var vertexInfo in vertexInfoMap.Values)
            {
                string connectedVertices = string.Join(" ", vertexInfo.ConnectedVertices);

                controlSb.AppendLine(
                     $"{vertexInfo.Order.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{vertexInfo.Vertex.x.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{vertexInfo.Vertex.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{vertexInfo.Vertex.z.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{vertexInfo.Owner.GetInstanceID()}," +
                    $"{vertexInfo.DistanceToCamera.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{connectedVertices}," +
                    $"{bottomLeft.x.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{bottomLeft.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{bottomLeft.z.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{topLeft.x.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{topLeft.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{topLeft.z.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{topRight.x.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{topRight.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{topRight.z.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{bottomRight.x.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{bottomRight.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{bottomRight.z.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{frustumApex.x.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{frustumApex.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{frustumApex.z.ToString(System.Globalization.CultureInfo.InvariantCulture)},"                    
                    );
            }

            // Write the built string to a new file in the control folder
            File.WriteAllText(controlFilePath, controlSb.ToString());


            string filePath = @"C:\Users\Osman\My project\LearningData";
            string learnFilePath = Path.Combine(filePath, filename);
            // Create a new StringBuilder to build the output
            StringBuilder sb = new StringBuilder();

            // Write the column headers with IsVisible moved to the end
            sb.AppendLine(columns);

            foreach (var vertexInfo in vertexInfoMap.Values)
            {
                string connectedVertices = string.Join(" ", vertexInfo.ConnectedVertices);
                string isVisible = vertexInfo.IsVisible ? "1" : "0";
                string outOfFrame = vertexInfo.outOfFrame ? "1" : "0";
                string behindAMesh = vertexInfo.behindAMesh ? "1" : "0";

                sb.AppendLine(
                    $"{vertexInfo.Order.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{vertexInfo.Vertex.x.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{vertexInfo.Vertex.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{vertexInfo.Vertex.z.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{vertexInfo.Owner.GetInstanceID()}," +
                    $"{vertexInfo.DistanceToCamera.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{connectedVertices}," +
                    $"{bottomLeft.x.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{bottomLeft.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{bottomLeft.z.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{topLeft.x.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{topLeft.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{topLeft.z.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{topRight.x.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{topRight.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{topRight.z.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{bottomRight.x.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{bottomRight.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{bottomRight.z.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{frustumApex.x.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{frustumApex.y.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{frustumApex.z.ToString(System.Globalization.CultureInfo.InvariantCulture)}," +
                    $"{isVisible}," +
                    $"{outOfFrame}," +
                    $"{behindAMesh}"
                );
            }

            // Overwrite the original file with the modified data
            File.WriteAllText(learnFilePath, sb.ToString());


            UnityEngine.Debug.Log(lastInfo);

        }
    }
}

