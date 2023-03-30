using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[ExecuteInEditMode]
public class SimpleTiledWFC : MonoBehaviour {
    public TextAsset xml;

    public int gridsize = 1;
    public int width = 20;
    public int depth = 20;

    public int seed;
    public bool periodic;
    public int iterations;
    public bool incremental;
    public GameObject output;
    private Transform group;

    public SimpleTiledModel model;
    public Dictionary<string, GameObject> obmap = new();
    public GameObject[,] rendering;
    private readonly string subset = "";
    private bool undrawn = true;

    private void Start() {
        Generate();
        Run();
    }

    private void Update() {
        if (incremental) Run();
    }

    public void OnDrawGizmos() {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(new Vector3(width * gridsize / 2f - gridsize * 0.5f, depth * gridsize / 2f - gridsize * 0.5f, 0f), new Vector3(width * gridsize, depth * gridsize, gridsize));
    }

    public void destroyChildren() {
        foreach (Transform child in transform) DestroyImmediate(child.gameObject);
    }


    public void Run() {
        if (model == null) return;

        if (undrawn == false) return;

        if (model.Run(seed, iterations)) Draw();
    }

    public void Generate() {
        obmap = new Dictionary<string, GameObject>();

        if (output == null) {
            var ot = transform.Find("output-tiled");
            if (ot != null) output = ot.gameObject;
        }

        if (output == null) {
            output = new GameObject("output-tiled");
            output.transform.parent = transform;
            output.transform.position = gameObject.transform.position;
            output.transform.rotation = gameObject.transform.rotation;
        }

        for (var i = 0; i < output.transform.childCount; i++) {
            var go = output.transform.GetChild(i).gameObject;
            if (Application.isPlaying)
                Destroy(go);
            else
                DestroyImmediate(go);
        }

        group = new GameObject(xml.name).transform;
        group.parent = output.transform;
        group.position = output.transform.position;
        group.rotation = output.transform.rotation;
        group.localScale = new Vector3(1f, 1f, 1f);
        rendering = new GameObject[width, depth];
        model = new SimpleTiledModel(xml.text, subset, width, depth, periodic);
        undrawn = true;
    }

    public void Draw() {
        if (output == null) return;

        if (group == null) return;

        undrawn = false;
        for (var y = 0; y < depth; y++)
        for (var x = 0; x < width; x++)
            if (rendering[x, y] == null) {
                var v = model.Sample(x, y);
                var rot = 0;
                GameObject fab = null;
                if (v != "?") {
                    rot = int.Parse(v.Substring(0, 1));
                    v = v.Substring(1);
                    if (!obmap.ContainsKey(v)) {
                        fab = (GameObject)Resources.Load(v, typeof(GameObject));
                        obmap[v] = fab;
                    }
                    else {
                        fab = obmap[v];
                    }

                    if (fab == null) continue;

                    var pos = new Vector3(x * gridsize, y * gridsize, 0f);
                    var tile = Instantiate(fab, new Vector3(), Quaternion.identity);
                    var fscale = tile.transform.localScale;
                    tile.transform.parent = group;
                    tile.transform.localPosition = pos;
                    tile.transform.localEulerAngles = new Vector3(0, 0, 360 - rot * 90);
                    tile.transform.localScale = fscale;
                    rendering[x, y] = tile;
                }
                else {
                    undrawn = true;
                }
            }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(SimpleTiledWFC))]
public class TileSetEditor : Editor {
    public override void OnInspectorGUI() {
        var me = (SimpleTiledWFC)target;
        if (me.xml != null) {
            if (GUILayout.Button("generate")) me.Generate();

            if (me.model != null)
                if (GUILayout.Button("RUN")) {
                    me.model.Run(me.seed, me.iterations);
                    me.Draw();
                }
        }

        DrawDefaultInspector();
    }
}
#endif