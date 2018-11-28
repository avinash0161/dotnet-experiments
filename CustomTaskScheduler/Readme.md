The code sample in this project shows how a custom Task Scheduler can be built in .Net. Basically, a task scheduler has a queue containing tasks and the scheduler overrides four methods from the abstract class `TaskScheduler`. Once, Task.Start is called, the `QueueTask` method of the scheduler is called. `QueueTask` can use ThreadPool maintained by the .Net framework or schedule the tasks on `Thread` which it itself creates. In this project, the `QueueTask` creates its own `Thread`. But [here](https://docs.microsoft.com/en-us/dotnet/api/system.threading.tasks.taskscheduler?view=netframework-4.7.2) is an example of how `ThreadPool's` `UnsafeQueueUserWorkItem` or `QueueUserWorkItem` can also be used.

#### What's the difference between using Thread and ThreadPool?
Thread and ThreadPool both can be used to scheduler delegates from parallel asynchronous execution. When you use Thread, you are basically spinning up a new Thread and it will exist till the delegate given to it finishes. Instead, you can use `ThreadPool` which is a .Net managed pool of threads which are always there and thus don't have spinning up overhead. But one should explicitly use any of `Thread` or `ThreadPool` only when writing a scheduler. Once the scheduler has been written, user can use the functionality of `Tasks` from TPL library which is given a delegate to execute and a scheduler provided to the constructor of the `Task`. Then, when the `Task` is started, it automatically uses the scheduler to schedule the delegate. 

From C# 5.0, users don't have to explicitly create the delegates to be made into a Task. `async` and `await` keywords automatically create and schedule Tasks on the Scheduler (be it the default TaskScheduler or any custom scheduler). 

There are many more implementations to asynchrnously start delegates. [This blog](http://blog.stephencleary.com/2010/08/various-implementations-of-asynchronous.html) by Stephen Cleary describes the various implementations available to start asynchronous delegates in C#. Note that this blog discusses stuff in context of GUI applications but this is also true for normal console applications. As the blog says, if we use Tasks (using async/await), the Task being awaited on (called `awaitable`) captures the current context and resumes the rest of the method in that context, when it finishes. In contrast, if we use Tasks (from TPL library) to schedule Tasks, we will have to explicitly call the UI Task Scheduler (by calling `TaskScheduler.FromCurrentSynchronizationContext`) to schedule the requires Tasks. Let me illustrate why is the context important and why can't we schedule the rest of the method in any thread, with an example. Consider a GUI application. The application has a GUI thread which has all the GUI elements instantiated in its stack memory space. Therefore, if one wishes to change any property of the elements, one has to transfer to the GUI thread. And therefore, if the remainder of the method is accessing some GUI element, it has to execute in the GUI thread. Another example, where context is important is the implementation of Get() and Post() APIs in an HTTP server. The thread which receives the HTTP request has all the request context and if the remainder method wants to access some information about the HTTP request, it has to execute in the thread which received the request. `Await` does this automatically by capturing the context and resuming the remainder method in the same context. There are two kinds of context - `ExecutionContext` and `SynchronizationContext`. It's not always necessary that the remainder method should start on the same thread. It can start on a different thread and the `ExecutionContext` can be copied into the thread it is executing on. The two types of contexts are dicussed in detail later here.

#### Async, Await and Awaitables
An async keyword in method signature does nothing except than enabling the existence of an await operator in the method. The real magic is done by the await the keyword. `Await` is a unary operator which just takes one argument - an `awaitable`. C# has many awaitables - `Task`, `Task<T>`, `Task.Yield`, Windows 8 runtime has an unmanaged awaitable. The control flow continues synchronously till an await keyword is encountered. When an `await` is encountered, the await first checks the status of the awaitable (usually `Task.Status`). If the awaitable is completed, then the method continues synchronously. However, if the awaitable is not yet completed, then the `await` tells the awaitable to continue with the rest of the method when it completes and returns from the function. This transfer of execution flow is shown in this [blog](https://docs.microsoft.com/en-us/dotnet/csharp/programming-guide/concepts/async/). Note that the asynchronous method kind of pauses when it hits await, but the thread doesn't block (therefore, this is an asynchronous wait). 

Note that the stuff the awaitable is waiting on may be some long running job or some I/O operation. Take the code snippet below as an example:

```C#
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

So, what we have seen is that the remainder part of function can be scheduled on the thread with GUI context (i.e GUI thread), HTTP request context or any thread from the ThreadPool in case no context in required. [This blog](http://blog.stephencleary.com/2012/02/async-and-await.html) by Stephen Cleary explains the async, await and awaitable in more detail.

#### How does await tell the awaitable to run the rest of the function when it completes?
The remainder code is kind of chained with the awaitable by using something like ContinueWith(). [This stackoverflow post](https://stackoverflow.com/questions/8767218/is-async-await-keyword-equivalent-to-a-continuewith-lambda) explains this in detail.

#### ExecutionContext and SynchronizationContext
[This blog](https://blogs.msdn.microsoft.com/pfxteam/2012/06/15/executioncontext-vs-synchronizationcontext/) is a must read. Also I asked [this stackoverflow question](https://stackoverflow.com/questions/53511412/understanding-context-in-net-task-execution) on this. Let me summarize the main points. 

In a synchronous world, thread-local information is sufficient: everything’s happening on that one thread, and thus regardless of what stack frame you’re in on that thread, what function is being executed, and so forth, all code running on that thread can see and be influenced by data specific to that thread. In many systems, such ambient information is maintained in thread-local storage (TLS), such as in a ThreadStatic field or in a ThreadLocal<T>.  In a synchronous world, if I do operation A, then operation B, and then operation C, all three of those operations happen on the same thread, and thus all three of those are subject to the ambient data stored on that thread. But in an asynchronous world, I might start A on one thread and have it complete on another, such that operation B may start or run on a different thread than A, and similarly such that C may start or run on a different thread than B.  This means that this ambient context we’ve come to rely on for controlling details of our execution is no longer viable, because TLS doesn’t “flow” across these async points.  Thread-local storage is specific to a thread, whereas these asynchronous operations aren’t tied to a specific thread.  There is, however, typically a logical flow of control, and we want this ambient data to flow with that control flow, such that the ambient data moves from one thread to another.  This is what ExecutionContext enables. ExecutionContext is captured with the static Capture method and restored during the invocation of a delegate via the static run method. 
  
```C#
ExecutionContext ec = ExecutionContext.Capture();
```
```C#
ExecutionContext.Run(ec, delegate 
{ 
    … // code here will see ec’s state as ambient 
}, null);
```
All of the methods in the .NET Framework that fork asynchronous work capture and restore ExecutionContext in a manner like this. 

SynchronizationContext represents a particular environment you want to do some work in.  As an example of such an environment, Windows Forms apps have a UI thread, which is where any work that needs to use UI controls needs to happen.  For cases where you’re running code on a ThreadPool thread and you need to marshal work back to the UI so that this work can muck with UI controls, Windows Forms provides the `Control.BeginInvoke` method.  You give a delegate to a Control’s BeginInvoke method, and that delegate will be invoked back on the thread with which that control is associated. This `Control.BegineInvoke` is in Windows Forms. WPF app have a similar API called `Dispatcher.BeginInvoke`. So, different frameworks have different APIs. How to make a component which is agnostic to the UI framework? The answer is using `SynchronizationContext`. SynchronizationContext provides a virtual Post method; this method simply takes a delegate and runs it wherever, whenever, and however the SynchronizationContext implementation deems fit.  Windows Forms provides the WindowsFormSynchronizationContext type which overrides Post to call Control.BeginInvoke.  WPF provides the DispatcherSynchronizationContext type which overrides Post to call Dispatcher.BeginInvoke. 

When you flow ExecutionContext, you’re capturing the state from one thread and then restoring that state such that it’s ambient during the supplied delegate’s execution.  That’s not what happens when you capture and use a SynchronizationContext.  The capturing part is the same, in that you’re grabbing data from the current thread, but you then use that state differently.  Rather than making that state current during the invocation of the delegate, with SynchronizationContext.Post you’re simply using that captured state to invoke the delegate.  Where and when and how that delegate runs is completely up to the implementation of the Post method.

When you await a task, by default the awaiter will capture the current SynchronizationContext, and if there was one, when the task completes it’ll Post the supplied continuation delegate back to that context, rather than running the delegate on whatever thread the task completed or rather than scheduling it to run on the ThreadPool. This behavior of capturing and running on the SynchronizationContext can be suppressed by using `ConfigureAwait()` method. Note that while ConfigureAwait provides explicit, await-related programming model support for changing behavior related to SynchronizationContext, there is no await-related programming model support for suppressing ExecutionContext flow.  This is on purpose.  ExecutionContext is not something developers writing async code should need to worry about; it’s infrastructure level support that helps to simulate synchronous semantics (i.e. TLS) in an asynchronous world. In contrast, where code runs is something developers should be cognizant of, and thus SynchronizationContext rises to level of something that does deserve explicit programming model support.  

`SynchronizationContext` is actually part of the `ExecutionContext` and it is supposed to flow with ExecutionContext. This can cause various problems as disussed in the blog by Stephen Toub. However, pretty much any asynchronous operation whose core implementation resides in mscorlib (like await) won’t flow SynchronizationContext as part of ExecutionContext, because mscorlib suppresses the flow of SynchronizationContext as part of ExecutionContext.
