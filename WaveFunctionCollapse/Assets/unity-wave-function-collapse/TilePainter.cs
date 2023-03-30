using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

[RequireComponent(typeof(BoxCollider))]
public class TilePainter : MonoBehaviour {
    public int gridsize = 1;
    public int width = 20;
    public int height = 20;
    public GameObject tiles;
    public Vector3 cursor;
    public bool focused;
    public List<Object> palette = new();
    public Object color;
    private bool _changed = true;


    private int colidx;
    private Quaternion color_rotation;
    public GameObject[,] tileobs;


#if UNITY_EDITOR

    private static bool IsAssetAFolder(Object obj) {
        var path = "";
        if (obj == null) return false;

        path = AssetDatabase.GetAssetPath(obj.GetInstanceID());
        if (path.Length > 0) {
            if (Directory.Exists(path))
                return true;
            return false;
        }

        return false;
    }


    public void Encode() {
    }

    private static GameObject CreatePrefab(Object fab, Vector3 pos, Quaternion rot) {
        var o = PrefabUtility.InstantiatePrefab(fab as GameObject) as GameObject;
        if (o == null) {
            Debug.Log(IsAssetAFolder(fab));
            return o;
        }

        o.transform.position = pos;
        o.transform.rotation = rot;
        return o;
    }

    public void Restore() {
        var palt = transform.Find("palette");
        if (palt != null) DestroyImmediate(palt.gameObject);

        var pal = new GameObject("palette");
        pal.hideFlags = HideFlags.HideInHierarchy;
        var bc = pal.AddComponent<BoxCollider>();
        bc.size = new Vector3(palette.Count * gridsize, gridsize, 0f);
        bc.center = new Vector3((palette.Count - 1f) * gridsize * 0.5f, 0f, 0f);

        pal.transform.parent = gameObject.transform;
        pal.transform.localPosition = new Vector3(0f, -gridsize * 2, 0f);
        pal.transform.rotation = transform.rotation;


        var palette_folder = -1;

        for (var i = 0; i < palette.Count; i++) {
            var o = palette[i];
            if (IsAssetAFolder(o)) {
                palette_folder = i;
            }
            else {
                if (o != null) {
                    var g = CreatePrefab(o, new Vector3(), transform.rotation);
                    g.transform.parent = pal.transform;
                    g.transform.localPosition = new Vector3(i * gridsize, 0f, 0f);
                }
            }
        }

        if (palette_folder != -1) {
            var path = AssetDatabase.GetAssetPath(palette[palette_folder].GetInstanceID());
            path = path.Trim().Replace("Assets/Resources/", "");
            palette.RemoveAt(palette_folder);
            var contents = Resources.LoadAll(path);
            foreach (var o in contents)
                if (!palette.Contains(o))
                    palette.Add(o);
            Restore();
        }

        tileobs = new GameObject[width, height];
        if (tiles == null) {
            tiles = new GameObject("tiles");
            tiles.transform.parent = gameObject.transform;
            tiles.transform.localPosition = new Vector3();
        }

        var cnt = tiles.transform.childCount;
        var trash = new List<GameObject>();
        for (var i = 0; i < cnt; i++) {
            var tile = tiles.transform.GetChild(i).gameObject;
            var tilepos = tile.transform.localPosition;
            var X = (int)(tilepos.x / gridsize);
            var Y = (int)(tilepos.y / gridsize);
            if (ValidCoords(X, Y))
                tileobs[X, Y] = tile;
            else
                trash.Add(tile);
        }

        for (var i = 0; i < trash.Count; i++)
            if (Application.isPlaying)
                Destroy(trash[i]);
            else
                DestroyImmediate(trash[i]);

        if (color == null)
            if (palette.Count > 0)
                color = palette[0];
    }

    public void Resize() {
        transform.localScale = new Vector3(1, 1, 1);
        if (_changed) {
            _changed = false;
            Restore();
        }
    }

    public void Awake() {
        Restore();
    }

    public void OnEnable() {
        Restore();
    }

    private void OnValidate() {
        _changed = true;
        var bounds = GetComponent<BoxCollider>();
        bounds.center = new Vector3(width * gridsize * 0.5f - gridsize * 0.5f, height * gridsize * 0.5f - gridsize * 0.5f, 0f);
        bounds.size = new Vector3(width * gridsize, height * gridsize, 0f);
    }

    public Vector3 GridV3(Vector3 pos) {
        var p = transform.InverseTransformPoint(pos) + new Vector3(gridsize * 0.5f, gridsize * 0.5f, 0f);
        return new Vector3((int)(p.x / gridsize), (int)(p.y / gridsize), 0);
    }

    public bool ValidCoords(int x, int y) {
        if (tileobs == null) return false;

        return x >= 0 && y >= 0 && x < tileobs.GetLength(0) && y < tileobs.GetLength(1);
    }


    public void CycleColor() {
        colidx += 1;
        if (colidx >= palette.Count) colidx = 0;

        color = palette[colidx];
    }

    public void Turn() {
        if (ValidCoords((int)cursor.x, (int)cursor.y)) {
            var o = tileobs[(int)cursor.x, (int)cursor.y];
            if (o != null) o.transform.Rotate(0f, 0f, 90f);
        }
    }

    public Vector3 Local(Vector3 p) {
        return transform.TransformPoint(p);
    }

    public Object PrefabSource(GameObject o) {
        if (o == null) return null;

        Object fab = PrefabUtility.GetCorrespondingObjectFromSource(o);
        if (fab == null) fab = Resources.Load(o.name);

        if (fab == null) fab = palette[0];

        return fab;
    }

    public void Drag(Vector3 mouse, TileLayerEditor.TileOperation op) {
        Resize();
        if (tileobs == null) Restore();

        if (ValidCoords((int)cursor.x, (int)cursor.y)) {
            if (op == TileLayerEditor.TileOperation.Sampling) {
                var s = PrefabSource(tileobs[(int)cursor.x, (int)cursor.y]);
                Debug.Log(s);
                if (s != null) {
                    color = s;
                    color_rotation = tileobs[(int)cursor.x, (int)cursor.y].transform.localRotation;
                }
            }
            else {
                DestroyImmediate(tileobs[(int)cursor.x, (int)cursor.y]);
                if (op == TileLayerEditor.TileOperation.Drawing) {
                    if (color == null) return;

                    var o = CreatePrefab(color, new Vector3(), color_rotation);
                    o.transform.parent = tiles.transform;
                    o.transform.localPosition = cursor * gridsize;
                    o.transform.localRotation = color_rotation;
                    tileobs[(int)cursor.x, (int)cursor.y] = o;
                }
            }
        }
        else {
            if (op == TileLayerEditor.TileOperation.Sampling)
                if (cursor.y == -1 && cursor.x >= 0 && cursor.x < palette.Count) {
                    color = palette[(int)cursor.x];
                    color_rotation = Quaternion.identity;
                }
        }
    }

    public void Clear() {
        tileobs = new GameObject[width, height];
        DestroyImmediate(tiles);
        tiles = new GameObject("tiles");
        tiles.transform.parent = gameObject.transform;
        tiles.transform.localPosition = new Vector3();
    }

    public void OnDrawGizmos() {
        Gizmos.color = Color.white;
        Gizmos.matrix = transform.localToWorldMatrix;
        if (focused) {
            Gizmos.color = new Color(1f, 0f, 0f, 0.6f);
            Gizmos.DrawRay(cursor * gridsize + Vector3.forward * -49999f, Vector3.forward * 99999f);
            Gizmos.DrawRay(cursor * gridsize + Vector3.right * -49999f, Vector3.right * 99999f);
            Gizmos.DrawRay(cursor * gridsize + Vector3.up * -49999f, Vector3.up * 99999f);
            Gizmos.color = Color.yellow;
        }

        Gizmos.DrawWireCube(new Vector3(width * gridsize * 0.5f - gridsize * 0.5f, height * gridsize * 0.5f - gridsize * 0.5f, 0f),
            new Vector3(width * gridsize, height * gridsize, 0f));
    }
#endif
}


#if UNITY_EDITOR
[CustomEditor(typeof(TilePainter))]
public class TileLayerEditor : Editor {
    public enum TileOperation {
        None,
        Drawing,
        Erasing,
        Sampling
    }

    private TileOperation operation;

    private void OnSceneGUI() {
        ProcessEvents();
    }

    public override void OnInspectorGUI() {
        var me = (TilePainter)target;
        GUILayout.Label("Assign a prefab to the color property");
        GUILayout.Label("or the pallete array.");
        GUILayout.Label("drag        : paint tiles");
        GUILayout.Label("[s]+click  : sample tile color");
        GUILayout.Label("[x]+drag  : erase tiles");
        GUILayout.Label("[space]    : rotate tile");
        GUILayout.Label("[b]          : cycle color");
        if (GUILayout.Button("CLEAR")) me.Clear();

        DrawDefaultInspector();
    }

    private bool AmHovering(Event e) {
        var me = (TilePainter)target;
        RaycastHit hit;
        if (Physics.Raycast(HandleUtility.GUIPointToWorldRay(Event.current.mousePosition), out hit, Mathf.Infinity) &&
            hit.collider.GetComponentInParent<TilePainter>() == me) {
            me.cursor = me.GridV3(hit.point);
            me.focused = true;

            var rend = me.gameObject.GetComponentInChildren<Renderer>();
            if (rend) EditorUtility.SetSelectedRenderState(rend, EditorSelectedRenderState.Wireframe);
            return true;
        }

        me.focused = false;
        return false;
    }

    public void ProcessEvents() {
        var me = (TilePainter)target;
        var controlID = GUIUtility.GetControlID(1778, FocusType.Passive);
        var currentWindow = EditorWindow.mouseOverWindow;
        if (currentWindow && AmHovering(Event.current)) {
            var current = Event.current;
            var leftbutton = current.button == 0;
            switch (current.type) {
                case EventType.KeyDown:

                    if (current.keyCode == KeyCode.S) operation = TileOperation.Sampling;
                    if (current.keyCode == KeyCode.X) operation = TileOperation.Erasing;
                    current.Use();
                    return;
                case EventType.KeyUp:
                    operation = TileOperation.None;
                    if (current.keyCode == KeyCode.Space) me.Turn();
                    if (current.keyCode == KeyCode.B) me.CycleColor();
                    current.Use();
                    return;
                case EventType.MouseDown:
                    if (leftbutton) {
                        if (operation == TileOperation.None) operation = TileOperation.Drawing;

                        me.Drag(current.mousePosition, operation);

                        current.Use();
                    }

                    break;
                case EventType.MouseDrag:
                    if (leftbutton)
                        if (operation != TileOperation.None) {
                            me.Drag(current.mousePosition, operation);
                            current.Use();
                        }

                    break;
                case EventType.MouseUp:
                    if (leftbutton) {
                        operation = TileOperation.None;
                        current.Use();
                    }

                    break;
                case EventType.MouseMove:
                    me.Resize();
                    current.Use();
                    break;
                case EventType.Repaint:
                    break;
                case EventType.Layout:
                    HandleUtility.AddDefaultControl(controlID);
                    break;
            }
        }
    }

    private void DrawEvents() {
        Handles.BeginGUI();
        Handles.EndGUI();
    }
}
#endif