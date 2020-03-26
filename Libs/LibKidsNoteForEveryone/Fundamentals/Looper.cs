using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace LibKidsNoteForEveryone.Fundamentals
{
    public abstract class Looper
    {
        private Thread LooperThread = null;
        private object ThreadLock = new object();
        protected bool QuitRequested = false;
        private bool Pause = false;
        private int ManagedThreadId = -1;
        private string LooperName;

        public Looper(string name)
        {
            LooperName = name;
        }

        ~Looper()
        {
            Quit();
            Join();
        }

        protected void SetLooperName(string name)
        {
            LooperThread.Name = name;
        }

        protected string GetLooperName()
        {
            return LooperThread.Name;
        }

        public void Start()
        {
            if (LooperThread == null)
            {
                LooperThread = new Thread(this.ThreadFunc);
                SetLooperName(LooperName);
            }

            LooperThread.Start();
        }

        public void Quit()
        {
            if (LooperThread.IsAlive)
            {
                QuitRequested = true;

                if (Pause)
                {
                    Resume();
                }
            }
        }

        public void Join()
        {
            System.Diagnostics.Trace.WriteLine(String.Format("{0} Joining thread {1}...", DateTime.Now, LooperThread.Name));
            if (LooperThread.IsAlive)
            {
                LooperThread.Join();
            }
            System.Diagnostics.Trace.WriteLine(String.Format("{0} Thread {1} finished...", DateTime.Now, LooperThread.Name));
            LooperThread = null;
        }

        public void Suspend()
        {
            Pause = true;
        }

        public void Resume()
        {
            Pause = false;
            lock (ThreadLock)
            {
                Monitor.Pulse(ThreadLock);
            }
        }

        private void ThreadFunc()
        {
            if (ManagedThreadId < 0)
            {
                ManagedThreadId = System.Threading.Thread.CurrentThread.ManagedThreadId;
            }

            while (!QuitRequested)
            {
                LooperFunc();

                if (Pause)
                {
                    lock (ThreadLock)
                    {
                        Monitor.Wait(ThreadLock);
                    }
                }
            }
        }

        public int Id()
        {
            return LooperThread.ManagedThreadId;
        }

        public string Name()
        {
            return LooperThread.Name;
        }

        public int GetNativeThreadId()
        {
            return ManagedThreadId;
        }

        protected abstract void LooperFunc();
    }
}
