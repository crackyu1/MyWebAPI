using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Hosting;
using System.IO;
using System.Threading;
using System.Threading.Tasks;



var builder = WebApplication.CreateBuilder(args);

// 允许同步 IO 操作
builder.Services.Configure<IISServerOptions>(options =>
{
    options.AllowSynchronousIO = true;
});

builder.WebHost.ConfigureKestrel(serverOptions =>
{
    serverOptions.AllowSynchronousIO = true;
});

var app = builder.Build();
app.UseHttpsRedirection();
//index page
app.MapGet("/", (HttpContext context) =>
{
    return "Hello World!";
});
app.MapPost("/api/WriteFile", (HttpContext context) =>
{
    var request = context.Request;
    var response = context.Response;
    using var reader = new StreamReader(request.Body);
    var body = reader.ReadToEnd();
    var path = Path.Combine(Directory.GetCurrentDirectory(), "file.txt");

    // 使用互斥锁进行文件写入
    lock (SharedState.fileLock)
    {
        File.WriteAllText(path, body);
    }

    // 写入完成标记
    SharedState.WriteComplete = true;

    // 启动后台任务处理GET请求
    Task.Run(async () =>
    {
        using var client = new HttpClient();
        try
        {
            await client.GetAsync("http://localhost:5284/api/ReadFile");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"后台GET请求失败: {ex.Message}");
        }
    });

    // 立即返回请求体中的内容
    return body;
});

app.MapGet("/api/ReadFile", static async (HttpContext context) =>
{
    // 等待文件写入完成
    while (!SharedState.WriteComplete)
    {
        await Task.Delay(100);
    }
    var request = context.Request;
    var response = context.Response;
    var path = Path.Combine(Directory.GetCurrentDirectory(), "file.txt");
    try
    {
        // 使用互斥锁进行文件读取
        string result;
        lock (SharedState.fileLock)
        {
            var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.None);
            var reader = new StreamReader(fileStream);
            result = reader.ReadLine() ?? string.Empty;
            // 读取完成标记
            SharedState.ReadComplete = true;
            // 关闭文件流
            reader.Close();
            fileStream.Close();
        }
                    
        // 模拟查询数据库，等待1000ms
        await Task.Delay(10000);
        //打印结果
        Console.WriteLine($"Read file content: {result}");
        await response.WriteAsync($"Read file content: {result}");
    }
    catch (Exception ex)
    {
        Console.WriteLine($"File read failed: {ex.Message}");
        await response.WriteAsync("File read failed");
    }
});
app.Run();



