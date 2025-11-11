using System.Linq;
using BenchmarkDotNet.Attributes;
using Microsoft.VSDiagnostics;

namespace RealNotes.Benchmarks
{
    public class DummyNote
    {
        public double Left;
        public double Top;
        public double TransformX;
        public double TransformY;
    }

    [CPUUsageDiagnoser]
    public class DraggableNoteBenchmarks
    {
        private DummyNote[] notes = null !;
        [GlobalSetup]
        public void Setup()
        {
            // simulate a large number of notes
            notes = Enumerable.Range(0, 2000).Select(_ => new DummyNote()).ToArray();
        }

        [Benchmark]
        public void MoveUsingTransform()
        {
            for (int i = 0; i < notes.Length; i++)
            {
                var n = notes[i];
                // simulate small incremental pointer movement
                n.TransformX += 1.234;
                n.TransformY += 0.987;
            }
        }

        [Benchmark]
        public void MoveUsingCanvasSet()
        {
            for (int i = 0; i < notes.Length; i++)
            {
                var n = notes[i];
                // simulate applying new layout positions (costlier in real WPF)
                n.Left = n.Left + 1.234;
                n.Top = n.Top + 0.987;
            }
        }
    }
}