/*
The MIT License(MIT)
Copyright(c) mxgmn 2016.
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
The software is provided "as is", without warranty of any kind, express or implied, including but not limited to the warranties of merchantability, fitness for a particular purpose and noninfringement. In no event shall the authors or copyright holders be liable for any claim, damages or other liability, whether in an action of contract, tort or otherwise, arising from, out of or in connection with the software or the use or other dealings in the software.
*/

using System;
using System.Collections.Generic;

internal class OverlappingModel : Model {
    public List<byte> colors;
    private readonly int ground;

    private readonly int N;

    public byte[][] patterns;

    public OverlappingModel(byte[,] sample, int N, int width, int height, bool periodicInput, bool periodicOutput, int symmetry, int ground)
        : base(width, height) {
        this.N = N;
        periodic = periodicOutput;

        int SMX = sample.GetLength(0), SMY = sample.GetLength(1);

        colors = new List<byte>();
        colors.Add(0);
        for (var y = 0; y < SMY; y++)
        for (var x = 0; x < SMX; x++) {
            var color = sample[x, y];

            var i = 0;
            foreach (var c in colors) {
                if (c == color) break;
                i++;
            }

            if (i == colors.Count) colors.Add(color);
        }

        var C = colors.Count;
        var W = Stuff.Power(C, N * N);

        Func<Func<int, int, byte>, byte[]> pattern = f => {
            var result = new byte[N * N];
            for (var y = 0; y < N; y++)
            for (var x = 0; x < N; x++)
                result[x + y * N] = f(x, y);
            return result;
        };

        Func<int, int, byte[]> patternFromSample = (x, y) => { return pattern((dx, dy) => { return sample[(x + dx) % SMX, (y + dy) % SMY]; }); };
        Func<byte[], byte[]> rotate = p => { return pattern((x, y) => { return p[N - 1 - y + x * N]; }); };
        Func<byte[], byte[]> reflect = p => { return pattern((x, y) => { return p[N - 1 - x + y * N]; }); };

        Func<byte[], long> index = p => {
            long result = 0, power = 1;
            for (var i = 0; i < p.Length; i++) {
                result += p[p.Length - 1 - i] * power;
                power *= C;
            }

            return result;
        };

        Func<long, byte[]> patternFromIndex = ind => {
            long residue = ind, power = W;
            var result = new byte[N * N];

            for (var i = 0; i < result.Length; i++) {
                power /= C;
                var count = 0;

                while (residue >= power) {
                    residue -= power;
                    count++;
                }

                result[i] = (byte)count;
            }

            return result;
        };

        var weights = new Dictionary<long, int>();
        var ordering = new List<long>();

        for (var y = 0; y < (periodicInput ? SMY : SMY - N + 1); y++)
        for (var x = 0; x < (periodicInput ? SMX : SMX - N + 1); x++) {
            var ps = new byte[8][];

            ps[0] = patternFromSample(x, y);
            ps[1] = reflect(ps[0]);
            ps[2] = rotate(ps[0]);
            ps[3] = reflect(ps[2]);
            ps[4] = rotate(ps[2]);
            ps[5] = reflect(ps[4]);
            ps[6] = rotate(ps[4]);
            ps[7] = reflect(ps[6]);

            for (var k = 0; k < symmetry; k++) {
                var ind = index(ps[k]);
                if (weights.ContainsKey(ind)) {
                    weights[ind]++;
                }
                else {
                    weights.Add(ind, 1);
                    ordering.Add(ind);
                }
            }
        }

        T = weights.Count;
        this.ground = (ground + T) % T;

        patterns = new byte[T][];
        this.weights = new double[T];

        var counter = 0;
        foreach (var w in ordering) {
            patterns[counter] = patternFromIndex(w);
            this.weights[counter] = weights[w];
            counter++;
        }


        Func<byte[], byte[], int, int, bool> agrees = (p1, p2, dx, dy) => {
            int xmin = dx < 0 ? 0 : dx, xmax = dx < 0 ? dx + N : N, ymin = dy < 0 ? 0 : dy, ymax = dy < 0 ? dy + N : N;
            for (var y = ymin; y < ymax; y++)
            for (var x = xmin; x < xmax; x++)
                if (p1[x + N * y] != p2[x - dx + N * (y - dy)])
                    return false;
            return true;
        };

        propagator = new int[4][][];
        for (var d = 0; d < 4; d++) {
            propagator[d] = new int[T][];
            for (var t = 0; t < T; t++) {
                var list = new List<int>();
                for (var t2 = 0; t2 < T; t2++)
                    if (agrees(patterns[t], patterns[t2], DX[d], DY[d]))
                        list.Add(t2);
                propagator[d][t] = new int[list.Count];
                for (var c = 0; c < list.Count; c++) propagator[d][t][c] = list[c];
            }
        }
    }

    protected override bool OnBoundary(int x, int y) {
        return !periodic && (x + N > FMX || y + N > FMY || x < 0 || y < 0);
    }


    public byte Sample(int x, int y) {
        var found = false;
        byte res = 99;
        for (var t = 0; t < T; t++)
            if (wave[x + y * FMX][t]) {
                if (found) return 99;
                found = true;
                res = patterns[t][0];
            }

        return res;
    }

    protected override void Clear() {
        base.Clear();
        //here we could actually set ground as all 4 sides possibly, think i need to query which ngram is made from the ground tile index, instead of just using that index into the ngrams
        if (ground != 0) {
            for (var x = 0; x < FMX; x++)
                //top
                //for (int t = 0; t < T; t++) if (t != ground) Ban(x + (FMY - 1) * FMX, t);
                //bottom
            for (var t = 0; t < T; t++)
                if (t != ground)
                    Ban(x, t);

            /* for (int y = 0; y < FMY; y++)
            {
                //right
                for (int t = 0; t < T; t++) if (t != ground) Ban((y * FMX) + (FMX - 1), t);
                
                //left
                for (int t = 0; t < T; t++) if (t != ground) Ban(y * FMX, t);
            }*/

            Propagate();
        }
    }
}