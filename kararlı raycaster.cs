using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using UnityEngine;

public class VisibleVerticesRaycaster : MonoBehaviour
{
    private Camera cam;
    private List<Vector3> visibleVertices = new List<Vector3>();
    private Vector3 lastCamPos;
    private Quaternion lastCamRot;
    private float logTimer = 0f;
    private string lastInfo, output = "";    

    void Start()
    {
        cam = GetComponent<Camera>();
        lastCamPos = cam.transform.position;
        lastCamRot = cam.transform.rotation;
    }



    void Update()
    {
        logTimer += Time.deltaTime;

        if (logTimer >= 1f)
        {
            if (IsCameraStateChanged())
            {
                visibleVertices.Clear();
                
                HashSet<Vector3> uniqueVertices = new HashSet<Vector3>();

                foreach (MeshRenderer meshRenderer in GameObject.FindObjectsOfType<MeshRenderer>())
                {
                    Mesh mesh = null;
                    GameObject obj = meshRenderer.gameObject;

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
                        foreach (Vector3 vertex in mesh.vertices)
                        {

                            Vector3 vertexWorld = obj.transform.TransformPoint(vertex);
                            uniqueVertices.Add(vertexWorld); // Mükerrer köşeleri önlemek için köşeleri tekilleştirdik.
                        }
                    }
                }

                foreach (Vector3 vertexWorld in uniqueVertices)
                {
                    Vector3 vertexScreenPos = cam.WorldToScreenPoint(vertexWorld);
                    if (IsVertexVisible(vertexScreenPos, vertexWorld))
                    {
                        visibleVertices.Add(vertexWorld);
                    }
                }

                lastInfo = "Visible Vertices Count: " + visibleVertices.Count + "\nVisible Vertices: ";

                logTimer = 0f;
            }
        }

        HandleKeyPress();
    }

    // Unique bir hash değeri oluşturmak için bir method
    int GetVertexHash(int objectId, Vector3 vertex)
    {
        // String üzerinde GetHashcode'u kullanabiliriz
        string uniqueKey = string.Format("{0}_{1}_{2}_{3}", objectId, vertex.x, vertex.y, vertex.z);
        return uniqueKey.GetHashCode();
    }

    bool IsCameraStateChanged()
    {
        return cam.transform.position != lastCamPos || cam.transform.rotation != lastCamRot || visibleVertices.Count > 0;
    }

    float sphereRadius = 0.1f; // Kürenin yarıçapını burada ayarlayabiliriz.

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


    bool IsVertexNotObstructed(Vector3 vertexWorld, Transform objTransform)
    {
        Vector3 dir = vertexWorld - cam.transform.position;
        RaycastHit hit;

        return !Physics.Raycast(cam.transform.position, dir, out hit)
            || hit.transform == objTransform && hit.distance >= dir.magnitude;
    }

    void HandleKeyPress()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter))
        {
            string timestamp = DateTime.Now.ToString("yyyyMMddHHmmss");
            string folderPath = @"C:\Users\Osman\My project\TrainingData\";

            // Use the timestamp as the filename
            string filename = "honolulu_" + $"log_{timestamp}.txt";

            // Create the full file path
            string filePath = Path.Combine(folderPath, filename);

            // Write the object string to a new file
            File.WriteAllText(filePath, output);

            UnityEngine.Debug.Log(lastInfo);
        }
    }
}
