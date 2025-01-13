public static class SharedState
{
    public static bool WriteComplete { get; set; } = false;
    public static bool ReadComplete { get; set; } = false;
    // 定义一个静态对象作为锁
    public static readonly object fileLock = new object();
}



