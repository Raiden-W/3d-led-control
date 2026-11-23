using System;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace LedMatrix.Editor
{
    public static class MatrixPerformance
    {
        static MatrixVolumeRenderer matrix;
        static double start,duration;
        static int frames,unityFrames;
        static long allocated;
        static string label;
        public static void Begin(double seconds,string name)
        {
            EditorApplication.update-=Tick;
            matrix=UnityEngine.Object.FindFirstObjectByType<MatrixVolumeRenderer>();
            start=EditorApplication.timeSinceStartup;duration=seconds;label=name;
            frames=matrix.FrameCount;unityFrames=Time.frameCount;
            allocated=UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            EditorApplication.update+=Tick;
        }
        static void Tick()
        {
            double elapsed=EditorApplication.timeSinceStartup-start;
            if(elapsed<duration) return;
            EditorApplication.update-=Tick;
            string result=$"{label}: {elapsed:0.00}s, atlas fps={(matrix.FrameCount-frames)/elapsed:0.00}, Unity fps={(Time.frameCount-unityFrames)/elapsed:0.00}, target={matrix.outputFps}, CPU submission={matrix.LastRenderMs:0.000}ms, allocated memory delta={UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong()-allocated} bytes. GPU={SystemInfo.graphicsDeviceName}, API={SystemInfo.graphicsDeviceType}, scale={matrix.resolutionScale}, fill={matrix.fillInterior}, preview={matrix.previewPoints}, playing={Application.isPlaying}.";
            Directory.CreateDirectory("Verification");File.AppendAllText("Verification/performance.txt",result+Environment.NewLine);Debug.Log(result);
        }
    }
}
