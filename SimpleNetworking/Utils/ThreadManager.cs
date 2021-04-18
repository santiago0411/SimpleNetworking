using System;
using System.Collections.Generic;
using System.Threading;

namespace SimpleNetworking.Utils
{
    internal class ThreadManager
    {
        private readonly List<Action> executeOnMainThread = new List<Action>();
        private readonly List<Action> executeCopiedOnMainThread = new List<Action>();
        private bool actionToExecuteOnMainThread = false;

        private volatile bool running = true;

        /// <summary>Starts the main thread.</summary>
        public void StartMainThread(double refreshRate)
        {
            running = true;

            DateTime nextLoop = DateTime.Now;
            while (running)
            {
                while (nextLoop < DateTime.Now)
                {
                    UpdateMain();
                    nextLoop = nextLoop.AddMilliseconds(refreshRate);

                    if (nextLoop > DateTime.Now)
                        Thread.Sleep(nextLoop - DateTime.Now);
                }
            }
        }

        /// <summary>Sets an action to be executed on the main thread.</summary>
        /// <param name="action">The action to be executed on the main thread.</param>
        public void ExecuteOnMainThread(Action action)
        {
            if (action == null)
                return;

            lock (executeOnMainThread)
            {
                executeOnMainThread.Add(action);
                actionToExecuteOnMainThread = true;
            }
        }

        /// <summary>Stops running the main thread loop and clears all the actions.</summary>
        public void StopMainThread()
        {
            running = false;

            lock (executeOnMainThread)
            {
                executeOnMainThread.Clear();
                executeCopiedOnMainThread.Clear();
                actionToExecuteOnMainThread = false;
            }
        }

        /// <summary>Executes all code meant to run on the main thread. NOTE: Call this ONLY from the main thread.</summary>
        private void UpdateMain()
        {
            if (actionToExecuteOnMainThread)
            {
                executeCopiedOnMainThread.Clear();
                lock (executeOnMainThread)
                {
                    executeCopiedOnMainThread.AddRange(executeOnMainThread);
                    executeOnMainThread.Clear();
                    actionToExecuteOnMainThread = false;
                }

                for (int i = 0; i < executeCopiedOnMainThread.Count; i++)
                {
                    executeCopiedOnMainThread[i]();
                }
            }
        }

        ~ThreadManager()
        {
            running = false;
        }
    }
}
