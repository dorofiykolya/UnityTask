using System;
using System.Collections;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace UnityTask
{
    public enum TaskThreadExecute
    {
        UnityCoroutineThread,
        BackgroundThread
    }

    public enum TaskState
    {
        Running,
        AbortRequested,
        Aborted,
        Completed
    }

    public class TaskInstruction : YieldInstruction, IEnumerator
    {
        public virtual bool MoveNext() { return false; }
        public virtual void Reset() { }
        public virtual object Current { get; private set; }
    }

    public sealed class TaskAbort : TaskInstruction
    {
        public static readonly TaskAbort Instance = new TaskAbort();
        private TaskAbort() { }
    }

    public class TaskBreak : TaskInstruction
    {
        protected TaskBreak() { }
    }

    public class TaskWait : TaskInstruction
    {
        private readonly int _finishTime;

        public TaskWait(int milliseconds)
        {
            _finishTime = Environment.TickCount + milliseconds;
        }

        private int RemainigTime
        {
            get { return Math.Max(0, _finishTime - Environment.TickCount); }
        }

        public override object Current
        {
            get { return RemainigTime; }
        }

        public override bool MoveNext()
        {
            return RemainigTime > 0;
        }

        public override string ToString()
        {
            return base.ToString() + ":" + RemainigTime;
        }
    }

    public class TaskCoroutineThread : TaskBreak { }

    public class TaskBackgroundThread : TaskBreak { }

    public class Task<T> : Task
    {
        private readonly OneListener<Task<T>> _completeListener = new OneListener<Task<T>>();

        public new event Action<Task<T>> Complete
        {
            add { _completeListener.Add(value); }
            remove { _completeListener.Remove(value); }
        }

        public T Result { get; set; }

        protected override void OnComplete()
        {
            _completeListener.Invoke(this);
            _completeListener.RemoveAll();
        }
    }

    public class Task
    {
        private class SynchronizedList<T> : IList<T>
        {
            private readonly List<T> _list;
            private readonly object _root;

            internal SynchronizedList(List<T> list)
            {
                _list = list;
                _root = ((ICollection)list).SyncRoot;
            }

            public object SyncRoot { get { return _root; } }

            public int Count
            {
                get
                {
                    lock (_root)
                    {
                        return _list.Count;
                    }
                }
            }

            public bool IsReadOnly
            {
                get
                {
                    return ((ICollection<T>)_list).IsReadOnly;
                }
            }

            public void Add(T item)
            {
                lock (_root)
                {
                    _list.Add(item);
                }
            }

            public void Clear()
            {
                lock (_root)
                {
                    _list.Clear();
                }
            }

            public bool Contains(T item)
            {
                lock (_root)
                {
                    return _list.Contains(item);
                }
            }

            public void CopyTo(T[] array, int arrayIndex)
            {
                lock (_root)
                {
                    _list.CopyTo(array, arrayIndex);
                }
            }

            public bool Remove(T item)
            {
                lock (_root)
                {
                    return _list.Remove(item);
                }
            }

            IEnumerator IEnumerable.GetEnumerator()
            {
                lock (_root)
                {
                    return _list.GetEnumerator();
                }
            }

            IEnumerator<T> IEnumerable<T>.GetEnumerator()
            {
                lock (_root)
                {
                    return ((IEnumerable<T>)_list).GetEnumerator();
                }
            }

            public T this[int index]
            {
                get
                {
                    lock (_root)
                    {
                        return _list[index];
                    }
                }
                set
                {
                    lock (_root)
                    {
                        _list[index] = value;
                    }
                }
            }

            public int IndexOf(T item)
            {
                lock (_root)
                {
                    return _list.IndexOf(item);
                }
            }

            public void Insert(int index, T item)
            {
                lock (_root)
                {
                    _list.Insert(index, item);
                }
            }

            public void RemoveAt(int index)
            {
                lock (_root)
                {
                    _list.RemoveAt(index);
                }
            }
        }


        protected class OneListener<T>
        {
            private readonly List<Action<T>> _list = new List<Action<T>>();
            private bool _stoped;
            private int _count;

            public void Add(Action<T> action)
            {
                var index = _list.IndexOf(action);
                if (index == -1)
                {
                    _list.Add(action);
                    _count++;
                }
                else
                {
                    if (_count == 1) return;
                    _list[index] = null;
                    _list.Add(action);
                }
            }

            public void Remove(Action<T> action)
            {
                var index = _list.IndexOf(action);
                if (index != -1)
                {
                    _list[index] = null;
                    _count--;
                }
            }

            public void RemoveAll()
            {
                _list.Clear();
                _count = 0;
            }

            public void Invoke(T value)
            {
                _stoped = false;
                var length = _list.Count;
                if (length == 0)
                {
                    _stoped = false;
                    return;
                }
                var index = 0;
                var i = 0;
                for (; i < length; i++)
                {
                    if (_count == 0 || _stoped)
                    {
                        _stoped = false;
                        return;
                    }
                    var current = _list[i];
                    if (current != null)
                    {
                        if (index != i)
                        {
                            _list[index] = _list[i];
                            _list[i] = null;
                        }
                        current.Invoke(value);
                        index++;
                    }
                }
                if (index != i)
                {
                    length = _list.Count;
                    while (i < length)
                    {
                        _list[index++] = _list[i++];
                    }
                    _list.RemoveRange(index, length - index);
                }
                _stoped = false;
            }
        }

        private class TaskThreadActionWrapper : IEnumerator
        {
            private readonly Action<Task> _actionTask;
            private readonly Task _task;
            private readonly Action _action;

            public TaskThreadActionWrapper(Action<Task> action, Task task)
            {
                _actionTask = action;
                _task = task;
            }

            public TaskThreadActionWrapper(Action action)
            {
                _action = action;
            }

            public bool MoveNext()
            {
                if (_actionTask != null)
                {
                    _actionTask.Invoke(_task);
                }
                else if (_action != null)
                {
                    _action.Invoke();
                }
                return false;
            }

            public void Reset() { }
            public object Current
            {
                get { return null; }
            }
        }

        private class TaskThreadFuncWrapper<T> : IEnumerator
        {
            private readonly Func<Task<T>, T> _funcTask;
            private readonly Task<T> _task;
            private readonly Func<T> _func;

            public TaskThreadFuncWrapper(Func<Task<T>, T> func, Task<T> task)
            {
                _funcTask = func;
                _task = task;
            }

            public TaskThreadFuncWrapper(Func<T> func)
            {
                _func = func;
            }

            public bool MoveNext()
            {
                if (_funcTask != null)
                {
                    _task.Result = _funcTask.Invoke(_task);
                }
                else if (_func != null)
                {
                    _task.Result = _func.Invoke();
                }
                return false;
            }

            public void Reset()
            {
            }

            public object Current
            {
                get { return null; }
            }
        }

        private class TaskThreadPool
        {
            private void Add(IEnumerator enumerator, Task task)
            {
                ThreadPool.QueueUserWorkItem(state =>
                {
                    task.CurrentThreadExecute = TaskThreadExecute.BackgroundThread;
                    if (task.State == TaskState.AbortRequested)
                    {
                        task.State = TaskState.Aborted;
                        RunInCoroutineThread(TaskAbort.Instance, task);
                        return;
                    }
                    while (enumerator.MoveNext())
                    {
                        task.Tick();
                        if (task.State == TaskState.AbortRequested)
                        {
                            task.State = TaskState.Aborted;
                            RunInCoroutineThread(TaskAbort.Instance, task);
                            return;
                        }
                        if (task.State == TaskState.Running)
                        {
                            var current = enumerator.Current;
                            task.LastState = current;
                            var taskMainThread = current as TaskCoroutineThread;
                            var taskWait = current as TaskWait;
                            if (taskWait != null)
                            {
                                if (taskWait.MoveNext())
                                {
                                    Thread.Sleep((int)taskWait.Current);
                                }
                            }
                            else if (taskMainThread != null)
                            {
                                RunInCoroutineThread(enumerator, task);
                                return;
                            }
                        }
                    }
                    RunInCoroutineThread(enumerator, task);
                });
            }

            public void Run(IEnumerator enumerator, Task task)
            {
                Add(enumerator, task);
            }
        }

        private class TaskMonoBehaviour : MonoBehaviour
        {
            private class IEnumeratorWrapper
            {
                public readonly IEnumerator Enumerator;
                public readonly Task Task;
                private TaskInstruction _instruction;

                public IEnumeratorWrapper(IEnumerator enumerator, Task task)
                {
                    Enumerator = enumerator;
                    Task = task;
                }

                public object Current
                {
                    get { return Enumerator.Current; }
                }

                public bool MoveNext()
                {
                    if (_instruction != null)
                    {
                        if (_instruction.MoveNext())
                        {
                            return true;
                        }
                        _instruction = null;
                    }
                    var result = Enumerator.MoveNext();
                    _instruction = Enumerator.Current as TaskInstruction;
                    return result;
                }
            }

            private readonly List<IEnumeratorWrapper> _list = new List<IEnumeratorWrapper>(32);
            private readonly SynchronizedList<IEnumeratorWrapper> _syncList;

            public TaskMonoBehaviour()
            {
                _syncList = new SynchronizedList<IEnumeratorWrapper>(_list);
            }

            private void Awake()
            {
                StartCoroutine(CorotinueExecuter());
            }

            private IEnumerator CorotinueExecuter()
            {
                while (true)
                {
                    int count = _syncList.Count;
                    if (count != 0)
                    {
                        var index = 0;
                        var i = 0;

                        for (; i < count; ++i)
                        {
                            var current = _syncList[i];
                            if (current != null)
                            {
                                if (index != i)
                                {
                                    _syncList[index] = current;
                                    _syncList[i] = null;
                                }
                                current.Task.CurrentThreadExecute = TaskThreadExecute.UnityCoroutineThread;
                                if (current.Task.State == TaskState.AbortRequested || current.Task.State == TaskState.Aborted)
                                {
                                    _syncList[index] = null;
                                    _syncTaskList.Remove(current.Task);
                                    current.Task.State = TaskState.Aborted;
                                    current.Task.OnAbort();
                                }
                                else
                                {
                                    if (current.MoveNext())
                                    {
                                        current.Task.Tick();
                                        current.Task.LastState = current.Current;
                                        if (current.Current is TaskBackgroundThread)
                                        {
                                            _syncList[index] = null;
                                            RunInBackgroundThread(current.Enumerator, current.Task);
                                        }
                                        else
                                        {
                                            index++;
                                        }
                                    }
                                    else
                                    {
                                        _syncList[index] = null;
                                        _syncTaskList.Remove(current.Task);
                                        current.Task.State = TaskState.Completed;
                                        current.Task.OnComplete();
                                    }
                                }
                            }
                        }
                        if (index != i)
                        {
                            count = _syncList.Count;
                            while (i < count)
                            {
                                lock (_syncList.SyncRoot)
                                {
                                    _syncList[index] = _syncList[i];
                                }
                                ++index;
                                ++i;
                            }
                        }
                    }
                    yield return null;
                }
            }

            private void Add(IEnumerator enumerator, Task task)
            {
                _syncList.Add(new IEnumeratorWrapper(enumerator, task));
            }

            public void Run(IEnumerator enumerator, Task task)
            {
                Add(enumerator, task);
            }
        }
        
        private static int _taskIdCounter;
        private static TaskMonoBehaviour _taskMonoBehaviour;
        private static TaskThreadPool _taskThreadPool;
        private static readonly List<Task> _taskList = new List<Task>(32);
        private static readonly SynchronizedList<Task> _syncTaskList = new SynchronizedList<Task>(_taskList);

        private static TaskMonoBehaviour GetTaskMonoBehaviour()
        {
            return _taskMonoBehaviour ?? (_taskMonoBehaviour = new GameObject("Task").AddComponent<TaskMonoBehaviour>());
        }

        private static TaskThreadPool GetTaskThreadPool()
        {
            return _taskThreadPool ?? (_taskThreadPool = new TaskThreadPool());
        }

        private static int NewId()
        {
            int newId;
            // We need to repeat if Interlocked.Increment wraps around and returns 0.
            // Otherwise next time this task's Id is queried it will get a new value
            do
            {
                newId = Interlocked.Increment(ref _taskIdCounter);
            } while (newId == 0);
            return newId;
        }

        private static Task RunInCoroutineThread(IEnumerator enumerator, Task task, bool newTask = false)
        {
            if (task == null)
            {
                task = new Task();
                _syncTaskList.Add(task);
            }
            else if (newTask)
            {
                _syncTaskList.Add(task);
            }
            task.NextThreadExecute = TaskThreadExecute.UnityCoroutineThread;
            GetTaskMonoBehaviour().Run(enumerator, task);
            return task;
        }

        private static Task RunInBackgroundThread(IEnumerator enumerator, Task task, bool newTask = false)
        {
            if (task == null)
            {
                task = new Task();
                _syncTaskList.Add(task);
            }
            else if (newTask)
            {
                _syncTaskList.Add(task);
            }

            task.NextThreadExecute = TaskThreadExecute.BackgroundThread;
            GetTaskThreadPool().Run(enumerator, task);
            return task;
        }

        public static Task[] Tasks
        {
            get
            {
                Task[] array;
                lock (_syncTaskList.SyncRoot)
                {
                    array = new Task[_syncTaskList.Count];
                    _syncTaskList.CopyTo(array, 0);
                }
                var index = 0;
                var i = 0;
                for (; i < array.Length; i++)
                {
                    var current = array[i];
                    if (current != null)
                    {
                        if (index != i)
                        {
                            array[index] = current;
                            array[i] = null;
                        }
                        index++;
                    }
                }
                if (index != i)
                {
                    Array.Resize(ref array, index);
                }
                return array;
            }
        }

        public static Task Run(Action action, TaskThreadExecute execute = TaskThreadExecute.UnityCoroutineThread)
        {
            var task = new Task();
            if (execute == TaskThreadExecute.BackgroundThread)
            {
                return RunInBackgroundThread(new TaskThreadActionWrapper(action), task, true);
            }
            return RunInCoroutineThread(new TaskThreadActionWrapper(action), task, true);
        }

        public static Task<T> Run<T>(Func<T> function, TaskThreadExecute execute = TaskThreadExecute.UnityCoroutineThread)
        {
            var task = new Task<T>();
            if (execute == TaskThreadExecute.BackgroundThread)
            {
                return RunInBackgroundThread(new TaskThreadFuncWrapper<T>(function), task, true) as Task<T>;
            }
            return RunInCoroutineThread(new TaskThreadFuncWrapper<T>(function), task, true) as Task<T>;
        }

        public static Task Run(Action<Task> action, TaskThreadExecute execute = TaskThreadExecute.UnityCoroutineThread)
        {
            var task = new Task();
            if (execute == TaskThreadExecute.BackgroundThread)
            {
                return RunInBackgroundThread(new TaskThreadActionWrapper(action, task), task, true);
            }
            return RunInCoroutineThread(new TaskThreadActionWrapper(action, task), task, true);
        }

        public static Task<T> Run<T>(Func<Task<T>, T> function, TaskThreadExecute execute = TaskThreadExecute.UnityCoroutineThread)
        {
            var task = new Task<T>();
            if (execute == TaskThreadExecute.BackgroundThread)
            {
                return RunInBackgroundThread(new TaskThreadFuncWrapper<T>(function, task), task, true) as Task<T>;
            }
            return RunInCoroutineThread(new TaskThreadFuncWrapper<T>(function, task), task, true) as Task<T>;
        }

        public static Task Run(IEnumerator enumerator, TaskThreadExecute execute = TaskThreadExecute.UnityCoroutineThread)
        {
            if (enumerator == null) throw new ArgumentNullException();
            if (execute == TaskThreadExecute.BackgroundThread)
            {
                return RunInBackgroundThread(enumerator, new Task(), true);
            }
            return RunInCoroutineThread(enumerator, new Task(), true);
        }

        public static readonly TaskCoroutineThread CoroutineYield = new TaskCoroutineThread();
        public static readonly TaskBackgroundThread BackgroundYield = new TaskBackgroundThread();
        public static readonly TaskAbort AbortYield = TaskAbort.Instance;
        public static TaskWait WaitYield(int milliseconds) { return new TaskWait(milliseconds); }

        // INSTANCE
        private readonly OneListener<Task> _completeListener = new OneListener<Task>();
        private readonly OneListener<Task> _abortedListener = new OneListener<Task>();

        public event Action<Task> Complete
        {
            add { _completeListener.Add(value); }
            remove { _completeListener.Remove(value); }
        }

        public event Action<Task> Aborted
        {
            add { _abortedListener.Add(value); }
            remove { _abortedListener.Remove(value); }
        }

        private readonly int _startTime;

        public int Id { get; private set; }

        public bool IsCompleted { get { return State == TaskState.Completed; } }

        public bool IsAborted { get { return State == TaskState.Aborted; } }

        public TaskThreadExecute CurrentThreadExecute { get; protected set; }

        public TaskThreadExecute NextThreadExecute { get; protected set; }

        public TaskState State { get; protected set; }

        public int Ticks { get; protected set; }

        public int LifeTime { get { return Environment.TickCount - _startTime; } }

        public object LastState { get; protected set; }

        public Task()
        {
            _startTime = Environment.TickCount;
            Id = NewId();
            State = TaskState.Running;
        }

        public virtual void Abort()
        {
            if (State == TaskState.Running)
            {
                State = TaskState.AbortRequested;
            }
        }

        private void Tick()
        {
            ++Ticks;
        }

        protected virtual void OnComplete()
        {
            _completeListener.Invoke(this);
            _completeListener.RemoveAll();
        }

        protected virtual void OnAbort()
        {
            _abortedListener.Invoke(this);
            _abortedListener.RemoveAll();
        }
    }
}