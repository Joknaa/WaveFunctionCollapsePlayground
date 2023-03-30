using System.Collections.Generic;
using System.IO;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

internal class Training : MonoBehaviour {
    public int gridsize = 1;
    public int width = 12;
    public int depth = 12;
    public Object[] tiles = new Object[0];
    public int[] RS = new int[0];
    private Dictionary<string, int[]> neighbors;
    public byte[,] sample;
    public Dictionary<string, byte> str_tile;

    private void OnDrawGizmos() {
        Gizmos.matrix = transform.localToWorldMatrix;
        Gizmos.color = Color.magenta;
        Gizmos.DrawWireCube(new Vector3(width * gridsize / 2f - gridsize * 0.5f, depth * gridsize / 2f - gridsize * 0.5f, 0f),
            new Vector3(width * gridsize, depth * gridsize, gridsize));
        Gizmos.color = Color.cyan;
        for (var i = 0; i < transform.childCount; i++) {
            var tile = transform.GetChild(i).gameObject;
            var tilepos = tile.transform.localPosition;
            if (tilepos.x > -0.55f && tilepos.x <= width * gridsize - 0.55f &&
                tilepos.y > -0.55f && tilepos.y <= depth * gridsize - 0.55f)
                Gizmos.DrawSphere(tilepos, gridsize * 0.2f);
        }
    }

    public static byte Get2DByte(byte[,] ar, int x, int y) {
        return ar[x, y];
    }

    public int Card(int n) {
        return (n % 4 + 4) % 4;
    }

    public void RecordNeighbors() {
        Compile();
        neighbors = new Dictionary<string, int[]>();
        for (var y = 0; y < depth; y++)
        for (var x = 0; x < width; x++)
        for (var r = 0; r < 2; r++) {
            int idx = sample[x, y];
            var rot = Card(RS[idx] + r);
            var rx = x + 1 - r;
            var ry = y + r;
            if (rx < width && ry < depth) {
                int ridx = sample[rx, ry];
                var rrot = Card(RS[ridx] + r);
                var key = "" + idx + "." + rot + "|" + ridx + "." + rrot;
                if (!neighbors.ContainsKey(key) && tiles[idx] && tiles[ridx]) {
                    neighbors.Add(key, new[] { idx, rot, ridx, rrot });
                    Debug.DrawLine(
                        transform.TransformPoint(new Vector3((x + 0f) * gridsize, (y + 0f) * gridsize, 1f)),
                        transform.TransformPoint(new Vector3((x + 1f - r) * gridsize, (y + 0f + r) * gridsize, 1f)), Color.red, 9.0f, false);
                }
            }
        }

        File.WriteAllText(Application.dataPath + "/" + gameObject.name + ".xml", NeighborXML());
    }

    public string AssetPath(Object o) {
#if UNITY_EDITOR
        return AssetDatabase.GetAssetPath(o).Trim().Replace("Assets/Resources/", "").Replace(".prefab", "");
#else
		return "";
#endif
    }

    public string NeighborXML() {
        var counts = new Dictionary<Object, int>();
        var res = "<set>\n  <tiles>\n";
        for (var i = 0; i < tiles.Length; i++) {
            var o = tiles[i];
            if (o && !counts.ContainsKey(o)) {
                counts[o] = 1;
                var assetpath = AssetPath(o);
                var sym = "X";
                var last = assetpath.Substring(assetpath.Length - 1);
                if (last == "X" || last == "I" || last == "L" || last == "T" || last == "D") sym = last;
                res += "<tile name=\"" + assetpath + "\" symmetry=\"" + sym + "\" weight=\"1.0\"/>\n";
            }
        }

        res += "	</tiles>\n<neighbors>";
        var v = neighbors.Values;
        foreach (var link in v)
            res += "  <neighbor left=\"" + AssetPath(tiles[link[0]]) + " " + link[1] +
                   "\" right=\"" + AssetPath(tiles[link[2]]) + " " + link[3] + "\"/>\n";
        return res + "	</neighbors>\n</set>";
    }

    public bool hasWhitespace() {
        byte ws = 0;
        for (var y = 0; y < depth - 1; y++)
        for (var x = 0; x < width - 1; x++)
            if (sample[x, y] == ws)
                return true;
        return false;
    }

    public void Compile() {
        str_tile = new Dictionary<string, byte>();
        sample = new byte[width, depth];
        var cnt = transform.childCount;
        tiles = new Object[500];
        RS = new int[1000];
        tiles[0] = null;
        RS[0] = 0;
        for (var i = 0; i < cnt; i++) {
            var tile = transform.GetChild(i).gameObject;
            var tilepos = tile.transform.localPosition;

            if (tilepos.x > -0.55f && tilepos.x <= width * gridsize - 0.55f &&
                tilepos.y > -0.55f && tilepos.y <= depth * gridsize - 0.55f) {
                Object fab = tile;
#if UNITY_EDITOR
                fab = PrefabUtility.GetCorrespondingObjectFromSource(tile);
                // if (fab == null){
                // 	PrefabUtility.RevertPrefabInstance(tile);
                // 	fab = PrefabUtility.GetCorrespondingObjectFromSource(tile);
                // }
                if (fab == null) {
                    fab = (GameObject)Resources.Load(tile.name);
                    if (!fab) fab = tile;
                }

                tile.name = fab.name;
#endif
                var X = (int)tilepos.x / gridsize;
                var Y = (int)tilepos.y / gridsize;
                var R = (int)((360 - tile.transform.localEulerAngles.z) / 90);
                if (R == 4) R = 0;
                ;
                if (!str_tile.ContainsKey(fab.name + R)) {
                    var index = str_tile.Count + 1;
                    str_tile.Add(fab.name + R, (byte)index);
                    tiles[index] = fab;
                    RS[index] = R;
                    sample[X, Y] = str_tile[fab.name + R];
                }
                else {
                    sample[X, Y] = str_tile[fab.name + R];
                }
            }
        }

        tiles = tiles.SubArray(0, str_tile.Count + 1);
        RS = RS.SubArray(0, str_tile.Count + 1);
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(Training))]
public class TrainingEditor : Editor {
    public override void OnInspectorGUI() {
        var training = (Training)target;
        if (GUILayout.Button("compile")) training.Compile();
        if (GUILayout.Button("record neighbors")) training.RecordNeighbors();
        DrawDefaultInspector();
    }
}
#endif