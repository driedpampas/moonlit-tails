#if HAS_PROBUILDER
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.ProBuilder;

// Blocks out a basic bar room with ProBuilder shapes. Runs once automatically,
// or again any time from Tools > Moonlit Tails > Build Bar Room.
[InitializeOnLoad]
static class BuildBarRoom
{
    const string ScenePath = "Assets/Scenes/BarRoom.unity";

    static BuildBarRoom()
    {
        if (!File.Exists(ScenePath)) EditorApplication.delayCall += Build;
    }

    [MenuItem("Tools/Moonlit Tails/Build Bar Room")]
    static void Build()
    {
        var scene = EditorSceneManager.NewScene(NewSceneSetup.DefaultGameObjects, NewSceneMode.Single);
        var root = new GameObject("BarRoom").transform;

        var floor = Mat("Floor", new Color(0.66f, 0.44f, 0.29f));
        var wall = Mat("Wall", new Color(0.18f, 0.25f, 0.45f));
        var wood = Mat("Wood", new Color(0.72f, 0.47f, 0.29f));
        var dark = Mat("DarkWood", new Color(0.43f, 0.26f, 0.18f));
        var teal = Mat("Teal", new Color(0.24f, 0.70f, 0.68f));
        var gold = Mat("Gold", new Color(0.91f, 0.72f, 0.31f));

        // floor, raised customer platform, ceiling
        Box(root, "Floor", new Vector3(9, 0.2f, 7), new Vector3(0, -0.1f, 1.8f), floor);
        Box(root, "CustomerPlatform", new Vector3(9, 0.5f, 3.9f), new Vector3(0, 0.25f, 3.35f), dark);
        Box(root, "Ceiling", new Vector3(9, 0.2f, 7), new Vector3(0, 3.6f, 1.8f), dark);

        // walls, with a doorway in each side wall
        Box(root, "Wall_Back", new Vector3(9, 3.6f, 0.2f), new Vector3(0, 1.8f, -1.7f), wall);
        Box(root, "Wall_Front", new Vector3(9, 3.6f, 0.2f), new Vector3(0, 1.8f, 5.3f), wall);
        foreach (var x in new[] { -4.6f, 4.6f })
        {
            var s = x < 0 ? "L" : "R";
            Box(root, "Wall_" + s + "_A", new Vector3(0.2f, 3.6f, 3.9f), new Vector3(x, 1.8f, 0.25f), wall);
            Box(root, "Wall_" + s + "_B", new Vector3(0.2f, 3.6f, 1.6f), new Vector3(x, 1.8f, 4.5f), wall);
            Box(root, "Wall_" + s + "_Top", new Vector3(0.2f, 1.1f, 1.0f), new Vector3(x, 3.05f, 3.2f), wall);
        }

        // U-shaped bar around the player (player stands at the origin, facing +Z)
        Box(root, "Counter_Front", new Vector3(3.6f, 0.96f, 0.65f), new Vector3(0, 0.48f, 0.75f), dark);
        Box(root, "Counter_Front_Top", new Vector3(3.7f, 0.06f, 0.78f), new Vector3(0, 0.99f, 0.75f), wood);
        Box(root, "Counter_Front_Trim", new Vector3(3.6f, 0.4f, 0.04f), new Vector3(0, 0.72f, 1.1f), teal);
        foreach (var x in new[] { -1.48f, 1.48f })
        {
            var s = x < 0 ? "L" : "R";
            Box(root, "Counter_" + s, new Vector3(0.65f, 0.96f, 1.4f), new Vector3(x, 0.48f, -0.3f), dark);
            Box(root, "Counter_" + s + "_Top", new Vector3(0.75f, 0.06f, 1.4f), new Vector3(x, 0.99f, -0.3f), wood);
        }
        Box(root, "Counter_Back", new Vector3(3.6f, 0.9f, 0.45f), new Vector3(0, 0.45f, -1.35f), dark);
        Box(root, "Counter_Back_Top", new Vector3(3.7f, 0.06f, 0.5f), new Vector3(0, 0.93f, -1.35f), wood);
        for (int i = 0; i < 3; i++)
            Box(root, "Shelf_" + (i + 1), new Vector3(3.2f, 0.05f, 0.25f), new Vector3(0, 1.5f + i * 0.38f, -1.47f), wood);

        // stools and lounge tables on the customer side
        foreach (var x in new[] { -1.0f, 1.0f })
            Cyl(root, "Stool", 0.2f, 0.45f, new Vector3(x, 0.5f, 1.5f), teal);
        foreach (var x in new[] { -2.9f, 2.9f })
        {
            Cyl(root, "Table_Leg", 0.06f, 0.5f, new Vector3(x, 0.5f, 3.6f), gold);
            Cyl(root, "Table_Top", 0.45f, 0.05f, new Vector3(x, 1.0f, 3.6f), wood);
        }

        // lights
        foreach (var x in new[] { -1f, 0f, 1f })
            Light(root, "Pendant", new Vector3(x, 2.2f, 0.75f), new Color(1f, 0.83f, 0.6f), 2.5f, 4f);
        Light(root, "Lounge_L", new Vector3(-2.9f, 2.6f, 3.6f), new Color(1f, 0.7f, 0.42f), 2f, 5f);
        Light(root, "Lounge_R", new Vector3(2.9f, 2.6f, 3.6f), new Color(1f, 0.7f, 0.42f), 2f, 5f);
        var sun = Object.FindFirstObjectByType<Light>();
        if (sun != null && sun.type == LightType.Directional) { sun.intensity = 0.25f; sun.color = new Color(0.6f, 0.68f, 1f); }
        RenderSettings.ambientLight = new Color(0.25f, 0.27f, 0.4f);

        // camera where the player's head would be
        var cam = Camera.main;
        if (cam != null) { cam.transform.position = new Vector3(0, 1.6f, -0.3f); cam.transform.rotation = Quaternion.Euler(10, 0, 0); }

        Directory.CreateDirectory("Assets/Scenes");
        EditorSceneManager.SaveScene(scene, ScenePath);
        Selection.activeTransform = root;
        Debug.Log("Bar room built: " + ScenePath);
    }

    static void Box(Transform parent, string name, Vector3 size, Vector3 pos, Material m)
    {
        var pb = ShapeGenerator.GenerateCube(PivotLocation.Center, size);
        Place(pb, parent, name, pos, m);
    }

    static void Cyl(Transform parent, string name, float radius, float height, Vector3 basePos, Material m)
    {
        var pb = ShapeGenerator.GenerateCylinder(PivotLocation.FirstVertex, 16, radius, height, 0);
        pb.transform.position = Vector3.zero;
        var center = pb.GetComponent<MeshRenderer>().bounds.center;
        Place(pb, parent, name, basePos - new Vector3(center.x, 0, center.z) + new Vector3(0, 0, 0), m);
        // re-centre on basePos after placement (pivot is the first vertex, not the middle)
        var b = pb.GetComponent<MeshRenderer>().bounds;
        pb.transform.position += new Vector3(basePos.x - b.center.x, basePos.y - b.min.y, basePos.z - b.center.z);
    }

    static void Place(ProBuilderMesh pb, Transform parent, string name, Vector3 pos, Material m)
    {
        pb.gameObject.name = name;
        pb.transform.SetParent(parent);
        pb.transform.position = pos;
        pb.GetComponent<MeshRenderer>().sharedMaterial = m;
        pb.ToMesh();
        pb.Refresh();
        pb.gameObject.isStatic = true;
    }

    static Material Mat(string name, Color c)
    {
        Directory.CreateDirectory("Assets/Materials");
        var path = "Assets/Materials/" + name + ".mat";
        var m = AssetDatabase.LoadAssetAtPath<Material>(path);
        if (m == null)
        {
            var sh = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            m = new Material(sh);
            AssetDatabase.CreateAsset(m, path);
        }
        m.color = c;
        if (m.HasProperty("_BaseColor")) m.SetColor("_BaseColor", c);
        EditorUtility.SetDirty(m);
        return m;
    }

    static void Light(Transform parent, string name, Vector3 pos, Color c, float intensity, float range)
    {
        var go = new GameObject("Light_" + name);
        go.transform.SetParent(parent);
        go.transform.position = pos;
        var l = go.AddComponent<Light>();
        l.type = LightType.Point; l.color = c; l.intensity = intensity; l.range = range; l.shadows = LightShadows.Soft;
    }
}
#endif
