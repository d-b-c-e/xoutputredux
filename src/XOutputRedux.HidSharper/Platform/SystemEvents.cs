#region License
/* Copyright 2017 James F. Bellinger <http://www.zer7.com/software/hidsharp>

   Licensed under the Apache License, Version 2.0 (the "License");
   you may not use this file except in compliance with the License.
   You may obtain a copy of the License at

      http://www.apache.org/licenses/LICENSE-2.0

   Unless required by applicable law or agreed to in writing,
   software distributed under the License is distributed on an
   "AS IS" BASIS, WITHOUT WARRANTIES OR CONDITIONS OF ANY
   KIND, either express or implied.  See the License for the
   specific language governing permissions and limitations
   under the License. */
#endregion

using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace XOutputRedux.HidSharper.Platform.SystemEvents
{
    #region System
    internal abstract class SystemEvent : IDisposable
    {
        protected SystemEvent(string name)
        {
            if (name == null) { throw new ArgumentNullException(); }
            Name = name;
        }

        public abstract void Dispose();
        public abstract void Reset();
        public abstract void Set();

        public bool Wait(int timeout)
        {
            try
            {
                return WaitHandle.WaitOne(timeout);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e);
                return false;
            }
        }

        public abstract bool CreatedNew { get; }
        public string Name { get; private set; }
        public abstract WaitHandle WaitHandle { get; }
    }

    internal abstract class SystemMutex : IDisposable
    {
        static HashSet<string> _antirecursionList = new HashSet<string>();
        Thread? _lockThread; // Mostly for debugging. Mutexes must be released by the threads that locked them.

        protected SystemMutex(string name)
        {
            if (name == null) { throw new ArgumentNullException(); }
            Name = name;
        }

        public abstract void Dispose();
        protected abstract bool WaitOne(int timeout);
        protected abstract void ReleaseMutex();

        sealed class ResourceLock : IDisposable
        {
            int _disposed;

            internal SystemMutex M = null!;

            public void Dispose()
            {
                if (Interlocked.CompareExchange(ref _disposed, 1, 0) != 0) { return; }

                try
                {
                    M.ReleaseMutexOuter();
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
            }
        }

        public bool TryLock(out IDisposable? @lock)
        {
            return TryLock(Timeout.Infinite, out @lock);
        }

        public bool TryLock(int timeout, out IDisposable? @lock)
        {
            @lock = null;

            try
            {
                if (!WaitOneOuter(timeout)) { return false; }
            }
            catch (AbandonedMutexException e)
            {
                Debug.WriteLine(e);
                return false;
            }

            @lock = new ResourceLock() { M = this };
            return true;
        }

        bool WaitOneOuter(int timeout)
        {
            if (!WaitOneInner(timeout)) { return false; }

            lock (_antirecursionList)
            {
                if (_antirecursionList.Contains(Name))
                {
                    ReleaseMutexInner(); return false;
                }

                _antirecursionList.Add(Name); return true;
            }
        }

        bool WaitOneInner(int timeout)
        {
            try
            {
                if (!WaitOne(timeout))
                {
                    return false;
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e); return false;
            }

            if (_lockThread != null) { throw new InvalidOperationException(); }
            _lockThread = Thread.CurrentThread; return true;
        }

        void ReleaseMutexOuter()
        {
            lock (_antirecursionList)
            {
                _antirecursionList.Remove(Name);
                ReleaseMutexInner();
            }
        }

        void ReleaseMutexInner()
        {
            if (_lockThread != Thread.CurrentThread) { throw new InvalidOperationException(); }
            ReleaseMutex(); _lockThread = null;
        }

        public abstract bool CreatedNew { get; }
        public string Name { get; private set; }
    }

    internal abstract class EventManager
    {
        internal abstract void Start();

        public abstract SystemEvent CreateEvent(string name);
        public abstract SystemMutex CreateMutex(string name);

        public bool MutexMayExist(string name) // Call it "MayExist" because it *might* not -- another could call this at the same time.
        {
            using (var mutex = CreateMutex(name)) { return !mutex.CreatedNew; }
        }
    }
    #endregion

    #region Default Implementation (Windows)
    internal class DefaultEventManager : EventManager
    {
        sealed class DefaultEvent : SystemEvent
        {
            bool _createdNew;
            EventWaitHandle? _event;

            public DefaultEvent(string name)
                : base(name)
            {
                _event = new EventWaitHandle(false, EventResetMode.ManualReset, GetGlobalName(name), out _createdNew);
            }

            public override void Dispose()
            {
                try
                {
                    if (_event != null)
                    {
                        _event.Close();
                        _event = null;
                    }
                }
                catch
                {

                }
            }

            public override void Reset()
            {
                try { _event?.Reset(); }
                catch { }
            }

            public override void Set()
            {
                try { _event?.Set(); }
                catch { }
            }

            public override bool CreatedNew
            {
                get { return _createdNew; }
            }

            public override WaitHandle WaitHandle
            {
                get { return _event!; }
            }
        }

        sealed class DefaultMutex : SystemMutex
        {
            bool _createdNew;
            Mutex? _mutex;

            public DefaultMutex(string name)
                : base(name)
            {
                _mutex = new Mutex(false, GetGlobalName(name), out _createdNew);
            }

            public override void Dispose()
            {
                try
                {
                    if (_mutex != null)
                    {
                        _mutex.Close();
                        _mutex = null;
                    }
                }
                catch (Exception e)
                {
                    Debug.WriteLine(e);
                }
            }

            protected override bool WaitOne(int timeout)
            {
                if (!_mutex!.WaitOne(timeout)) { return false; }
                return true;
            }

            protected override void ReleaseMutex()
            {
                if (_mutex == null) { return; }
                _mutex.ReleaseMutex();
            }

            public override bool CreatedNew
            {
                get { return _createdNew; }
            }
        }

        static string GetGlobalName(string name)
        {
            if (name == null) { throw new ArgumentNullException(); }
            if (name.Length > 240) { name = "HIDSharp Global (" + Convert.ToBase64String(SHA256.HashData(Encoding.UTF8.GetBytes(name))) + ")"; }
            return @"Global\" + name;
        }

        internal override void Start()
        {

        }

        public override SystemEvent CreateEvent(string name)
        {
            return new DefaultEvent(name);
        }

        public override SystemMutex CreateMutex(string name)
        {
            return new DefaultMutex(name);
        }
    }
    #endregion
}
