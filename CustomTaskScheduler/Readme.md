The code sample in this project shows how a custom Task Scheduler can be built in .Net. Basically, a task scheduler has a queue containing tasks and the scheduler overrides four methods from the abstract class `TaskScheduler`. Once, Task.Start is called, the `QueueTask` method of the scheduler is called. `QueueTask` can use ThreadPool maintained by the .Net framework or schedule the tasks on `Thread` which it itself creates. In this project, the `QueueTask` creates its own `Thread`. But [here](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskscheduler?view=netframework-4.7.2) is an example of how `ThreadPool's` `UnsafeQueueUserWorkItem` or `QueueUserWorkItem` can also be used.

#### What's the difference between using Thread and ThreadPool?
Thread and ThreadPool both can be used to scheduler delegates from parallel asynchronous execution. When you use Thread, you are basically spinning up a new Thread and it will exist till the delegate given to it finishes. Instead, you can use `ThreadPool` which is a .Net managed pool of threads which are always there and thus don't have spinning up overhead. But one should explicitly use any of `Thread` or `ThreadPool` only when writing a scheduler. Once the scheduler has been written, user can use the functionality of `Tasks` from TPL library which is given a delegate to execute and a scheduler provided to the constructor of the `Task`. Then, when the `Task` is started, it automatically uses the scheduler to schedule the delegate. 

From C# 5.0, users don't have to explicitly create the delegates to be made into a Task. `async` and `await` keywords automatically create and schedule Tasks on the Scheduler (be it the default TaskScheduler or any custom scheduler). 

There are many more implementations to asynchrnously start delegates. [This blog](http://blog.stephencleary.com/2010/08/various-implementations-of-asynchronous.html) by Stephen Cleary describes the various implementations available to start asynchronous delegates in C#. Note that this blog discusses stuff in context of GUI applications but this is also true for normal console applications. There can also be multiple Task Schedulers with their own queues. As the blog says, if we use Tasks (using async/await), the Task being awaited on (called `awaitable`) captures the current context and resumes the rest of the method in that context, when it finishes. In contrast, if we use Tasks (from TPL library) to schedule Tasks, we will have to explicitly call the UI Task Scheduler (by calling `TaskScheduler.FromCurrentSynchronizationContext`) to schedule the requires Tasks. 

#### Async, Await and Awaitables
An async keyword in method signature does nothing except than enabling the existence of an await operator in the method. The real magic is done by the await the keyword. `Await` is a unary operator which just takes one argument - an `awaitable`. C# has many awaitables - `Task`, `Task<T>`, `Task.Yield`, Windows 8 runtime has an unmanaged awaitable. The control flow continues synchronously till an await keyword is encountered. When an `await` is encountered, the await first checks the status of the awaitable (usually `Task.Status`). If the awaitable is completed, then the method continues synchronously. However, if the awaitable is not yet completed, then the `await` tells the awaitable to continue with the rest of the method when it completes and returns from the function. This transfer of execution flow is shown in this [blog](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/async/). Note that the asynchronous method kind of pauses when it hits await, but the thread doesn't block (therefore, this is an asynchronous wait). 

Note that the stuff the awaitable is waiting on may be some long running job or some I/O operation. Take the code snippet below as an example:

```
public void Main()
{
  Task x = Foo();
  ///
  // do some other work
  ///
  Console.Writeline(x.Result);
}

async Task Foo()
{  
  await Bar();
   ///
  // do some work
  ///
}

async Task Bar()
{
  ///
  // long running job or I/O operation
  ///
}
```

In the case of long running job, 2 threads are needed at least - one which runs Main after control returns to it and one which is running the long running job. However, if it is an I/O operation, only one thread is needed. Bar() is actually waiting on an I/O port (Http read, Disk read etc.) and it needs no thread. Only Main is using the thread. Stephen explains [here](http://blog.stephencleary.com/2013/11/there-is-no-thread.html) why an I/O operation does not need a thread of its own.

However, imagine the case that the Main() is a GUI application. When it hits the `x.Result` statement, the GUI thread blocks. Now, when the Bar() completes its I/O operation, it needs some thread to run the remainder of the `Foo()` code. Also, as we said before await returns from Foo(), the awaitable captures the context on which to execute the rest of Foo() code and it is the GUI context. Therefore, the awaitable is also waiting for the GUI thread to become free so that it can schedule the rest of the Foo() code. Now, this results in a deadlock because there is just one GUI thread. In order to avoid this deadlock, we should call Bar() by `await Bar().ConfigureAwait(false)`. This tells the awaitable that the remainder code doesn't need context. But what if the remainder of the code needs the context? Then, in Main() you will have to do something like `await Foo()` instead of Foo().Result. This will cause the GUI thread to return to the main controller and Main() won't hold the thread. Therefore, whenever the thread becomes avaiable, the remainder of the Foo() can be scheduled. Note that `await Foo()` can be done in GUI, but what if it is a console application. If you do await in Main(), then where will the thread return to? And you are thinking right. In fact, Main() method has been allowed to be `async` only recently starting from C# 7.0. [This blog](https://blog.stephencleary.com/2012/07/dont-block-on-async-code.html) by Stephen explains the **deadlock scenario** in more detail.

 [This blog](http://blog.stephencleary.com/2012/02/async-and-await.html) by Stephen Cleary explains the async, await and awaitable in more detail.

#### How does await tell the awaitable to run the rest of the function when it completes?
