using System;
using System.Threading.Tasks;
using System.Threading;

namespace CustomTaskScheduler
{
    class Program
    {
        static void Main(string[] args)
{
    var tasks = new Task[4];
    var scheduler = new SimpleScheduler();
 
    using (scheduler)//Automatically invoke dispose when you exit using.
    {
 
        Task taskS1 = new Task(() => 
        { Write("Running 1 seconds"); Thread.Sleep(1000); });
        tasks[0] = taskS1;
 
        Task taskS2 = new Task(() => 
        { Write("Running 2 seconds"); Thread.Sleep(2000); });
        tasks[1] = taskS2;
 
        Task taskS3 = new Task(() => 
        { Write("Running 3 seconds"); Thread.Sleep(3000); });
        tasks[2] = taskS3;
 
        Task taskS4 = new Task(() => 
        { Write("Running 4 seconds"); Thread.Sleep(4000); });
        tasks[3] = taskS4;
 
        foreach (var t in tasks)
        {
            t.Start(scheduler);
        }
 
 
        Write("Press any key to quit..");
        Console.ReadKey();
 
    }
}
static void Write(string msg)
{
    Console.WriteLine(DateTime.Now.ToString() + " on Thread " + Thread.CurrentThread.ManagedThreadId.ToString() + " -- " + msg);
 
}
    }
}
