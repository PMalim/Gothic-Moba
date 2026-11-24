using System.Diagnostics;

namespace BotArena.WebCenter;

public sealed class PerfMeter
{
    private double sum, max;
    private int count;

    public void Record(double milliseconds)
    {
        sum += milliseconds;
        count++;
        if (milliseconds > max) max = milliseconds;
    }

    public (double Average, double Max, int Ticks) Take()
    {
        (double, double, int) result = (count == 0 ? 0 : sum / count, max, count);
        sum = max = 0;
        count = 0;
        return result;
    }
}

public sealed class ProcessStats
{
    private TimeSpan lastCpu;
    private long lastTicks = Stopwatch.GetTimestamp();

    public ProcessStats()
    {
        using Process process = Process.GetCurrentProcess();
        lastCpu = process.TotalProcessorTime;
        StartedAt = process.StartTime;
    }

    public DateTime StartedAt { get; }

    public readonly record struct Sample(double WorkingSetMb, double PrivateMb, double HeapMb, double TotalMb, double CpuPercent, int Threads, int Gen0, int Gen1, int Gen2);

    public Sample Take()
    {
        using Process process = Process.GetCurrentProcess();
        TimeSpan cpu = process.TotalProcessorTime;
        long ticks = Stopwatch.GetTimestamp();
        double wall = Stopwatch.GetElapsedTime(lastTicks, ticks).TotalSeconds;
        double percent = wall <= 0 ? 0 : (cpu - lastCpu).TotalSeconds / wall / Environment.ProcessorCount * 100;
        lastCpu = cpu;
        lastTicks = ticks;
        const double Mb = 1024.0 * 1024.0;
        return new Sample(process.WorkingSet64 / Mb, process.PrivateMemorySize64 / Mb, GC.GetTotalMemory(false) / Mb,
            GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / Mb, Math.Round(percent, 1), process.Threads.Count,
            GC.CollectionCount(0), GC.CollectionCount(1), GC.CollectionCount(2));
    }
}
