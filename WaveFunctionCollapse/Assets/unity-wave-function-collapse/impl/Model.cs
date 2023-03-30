/*
The MIT License(MIT)
Copyright(c) mxgmn 2016.
Permission is hereby granted, free of charge, to any person obtaining a copy of this software and associated documentation files (the "Software"), to deal in the Software without restriction, including without limitation the rights to use, copy, modify, merge, publish, distribute, sublicense, and/or sell copies of the Software, and to permit persons to whom the Software is furnished to do so, subject to the following conditions:
The above copyright notice and this permission notice shall be included in all copies or substantial portions of the Software.
The software is provided "as is", without warranty of any kind, express or implied, including but not limited to the warranties of merchantability, fitness for a particular purpose and noninfringement. In no event shall the authors or copyright holders be liable for any claim, damages or other liability, whether in an action of contract, tort or otherwise, arising from, out of or in connection with the software or the use or other dealings in the software.
*/

using System;

public abstract class Model {
    protected static int[] DX = { -1, 0, 1, 0 };
    protected static int[] DY = { 0, 1, 0, -1 };
    private static readonly int[] opposite = { 2, 3, 0, 1 };
    private int[][][] compatible;
    protected int FMX, FMY, T;

    protected bool init;
    protected int[] observed;
    protected bool periodic;

    protected int[][][] propagator;

    protected Random random;

    private Tuple<int, int>[] stack;
    private int stacksize;
    private double sumOfWeights, sumOfWeightLogWeights, startingEntropy;

    private int[] sumsOfOnes;
    private double[] sumsOfWeights, sumsOfWeightLogWeights, entropies;
    protected bool[][] wave;
    private double[] weightLogWeights;

    protected double[] weights;

    protected Model(int width, int height) {
        FMX = width;
        FMY = height;
    }

    private void Init() {
        wave = new bool[FMX * FMY][];
        compatible = new int[wave.Length][][];
        for (var i = 0; i < wave.Length; i++) {
            wave[i] = new bool[T];
            compatible[i] = new int[T][];
            for (var t = 0; t < T; t++) compatible[i][t] = new int[4];
        }

        weightLogWeights = new double[T];
        sumOfWeights = 0;
        sumOfWeightLogWeights = 0;

        for (var t = 0; t < T; t++) {
            weightLogWeights[t] = weights[t] * Math.Log(weights[t]);
            sumOfWeights += weights[t];
            sumOfWeightLogWeights += weightLogWeights[t];
        }

        startingEntropy = Math.Log(sumOfWeights) - sumOfWeightLogWeights / sumOfWeights;

        sumsOfOnes = new int[FMX * FMY];
        sumsOfWeights = new double[FMX * FMY];
        sumsOfWeightLogWeights = new double[FMX * FMY];
        entropies = new double[FMX * FMY];

        stack = new Tuple<int, int>[wave.Length * T];
        stacksize = 0;
    }


    private bool? Observe() {
        var min = 1E+3;
        var argmin = -1;

        for (var i = 0; i < wave.Length; i++) {
            if (OnBoundary(i % FMX, i / FMX)) continue;

            var amount = sumsOfOnes[i];
            if (amount == 0) return false;

            var entropy = entropies[i];
            if (amount > 1 && entropy <= min) {
                var noise = 1E-6 * random.NextDouble();
                if (entropy + noise < min) {
                    min = entropy + noise;
                    argmin = i;
                }
            }
        }

        if (argmin == -1) {
            observed = new int[FMX * FMY];
            for (var i = 0; i < wave.Length; i++)
            for (var t = 0; t < T; t++)
                if (wave[i][t]) {
                    observed[i] = t;
                    break;
                }

            return true;
        }

        var distribution = new double[T];
        for (var t = 0; t < T; t++) distribution[t] = wave[argmin][t] ? weights[t] : 0;
        var r = distribution.Random(random.NextDouble());

        var w = wave[argmin];
        for (var t = 0; t < T; t++)
            if (w[t] != (t == r))
                Ban(argmin, t);

        return null;
    }

    protected void Propagate() {
        while (stacksize > 0) {
            var e1 = stack[stacksize - 1];
            stacksize--;

            var i1 = e1.Item1;
            int x1 = i1 % FMX, y1 = i1 / FMX;
            var w1 = wave[i1];

            for (var d = 0; d < 4; d++) {
                int dx = DX[d], dy = DY[d];
                int x2 = x1 + dx, y2 = y1 + dy;
                if (OnBoundary(x2, y2)) continue;

                if (x2 < 0) x2 += FMX;
                else if (x2 >= FMX) x2 -= FMX;
                if (y2 < 0) y2 += FMY;
                else if (y2 >= FMY) y2 -= FMY;

                var i2 = x2 + y2 * FMX;
                var p = propagator[d][e1.Item2];
                var compat = compatible[i2];

                for (var l = 0; l < p.Length; l++) {
                    var t2 = p[l];
                    var comp = compat[t2];

                    comp[d]--;
                    if (comp[d] == 0) Ban(i2, t2);
                }
            }
        }
    }

    public bool Run(int seed, int limit) {
        if (wave == null) Init();

        if (!init) {
            init = true;
            Clear();
        }

        if (seed == 0)
            random = new Random();
        else
            random = new Random(seed);

        for (var l = 0; l < limit || limit == 0; l++) {
            var result = Observe();
            if (result != null) return (bool)result;
            Propagate();
        }

        return true;
    }

    protected void Ban(int i, int t) {
        wave[i][t] = false;

        var comp = compatible[i][t];
        for (var d = 0; d < 4; d++) comp[d] = 0;
        stack[stacksize] = new Tuple<int, int>(i, t);
        stacksize++;

        var sum = sumsOfWeights[i];
        entropies[i] += sumsOfWeightLogWeights[i] / sum - Math.Log(sum);

        sumsOfOnes[i] -= 1;
        sumsOfWeights[i] -= weights[t];
        sumsOfWeightLogWeights[i] -= weightLogWeights[t];

        sum = sumsOfWeights[i];
        entropies[i] -= sumsOfWeightLogWeights[i] / sum - Math.Log(sum);
    }

    protected virtual void Clear() {
        for (var i = 0; i < wave.Length; i++) {
            for (var t = 0; t < T; t++) {
                wave[i][t] = true;
                for (var d = 0; d < 4; d++) compatible[i][t][d] = propagator[opposite[d]][t].Length;
            }

            sumsOfOnes[i] = weights.Length;
            sumsOfWeights[i] = sumOfWeights;
            sumsOfWeightLogWeights[i] = sumOfWeightLogWeights;
            entropies[i] = startingEntropy;
        }
    }

    protected abstract bool OnBoundary(int x, int y);
}