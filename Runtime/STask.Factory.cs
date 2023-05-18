using System;
using System.Runtime.ExceptionServices;
using System.Threading;

namespace SFramework.Threading.Tasks
{
    public partial struct STask
    {
        public static readonly STask CompletedTask = new STask();

        public static STask<T> FromResult<T>(T value)
        {
            return new STask<T>(value);
        }

        #region FromException
        public static STask FromException(Exception exception)
        {
            if (exception is OperationCanceledException oce)
            {
                return FromCanceled(oce.CancellationToken);
            }
            return new STask(new ExceptionResultSource(exception), 0);
        }

        public static STask<T> FromException<T>(Exception exception)
        {
            if (exception is OperationCanceledException oce)
            {
                return FromCanceled<T>(oce.CancellationToken);
            }
            return new STask<T>(new ExceptionResultSource<T>(exception), 0);
        }
        #endregion

        #region CanceledTask
        /// <summary> 默认的 Canceled STask 对象 （CancellationToken 为 None）</summary>
        private static readonly STask CanceledSTask = new Func<STask>(() =>//用委托包装下，当方法用
        {
            return new STask(new CanceledResultSource(CancellationToken.None), 0);
        })();

        private static class CanceledSTaskCache<T>
        {
            public static readonly STask<T> Task;

            static CanceledSTaskCache()
            {
                Task = new STask<T>(new CanceledResultSource<T>(CancellationToken.None), 0);
            }
        }

        public static STask FromCanceled(CancellationToken cancellationToken = default)
        {
            if (cancellationToken == CancellationToken.None)//默认token
            {
                return CanceledSTask;
            }
            else
            {
                return new STask(new CanceledResultSource(cancellationToken), 0);
            }
        }

        public static STask<T> FromCanceled<T>(CancellationToken cancellationToken = default)
        {
            if (cancellationToken == CancellationToken.None)
            {
                return CanceledSTaskCache<T>.Task;
            }
            else
            {
                return new STask<T>(new CanceledResultSource<T>(cancellationToken), 0);
            }
        }
        #endregion

        #region ISTaskSource
        private sealed class ExceptionResultSource : ISTaskSource
        {
            private readonly ExceptionDispatchInfo exception;
            private bool calledGet;

            public ExceptionResultSource(Exception exception)
            {
                this.exception = ExceptionDispatchInfo.Capture(exception);
            }

            public void GetResult(short token)
            {
                if (!this.calledGet)
                {
                    this.calledGet = true;
                    GC.SuppressFinalize(this);//不要调用析构函数，防止在Finalize线程调用析构函数之后，GC再重复调用
                }
                this.exception.Throw();
            }

            public STaskStatus GetStatus(short token)
            {
                return STaskStatus.Faulted;
            }

            public STaskStatus UnsafeGetStatus()
            {
                return STaskStatus.Faulted;
            }

            public void OnCompleted(Action<object> continuation, object state, short token)
            {
                continuation(state);
            }

            ~ExceptionResultSource()
            {
                if (!this.calledGet)//未处理异常，抛出来
                {
                    STaskScheduler.PublishUnobservedTaskException(this.exception.SourceException);
                }
            }
        }
        
        private sealed class ExceptionResultSource<T> : ISTaskSource<T>
        {
            private readonly ExceptionDispatchInfo exception;
            private bool calledGet;

            public ExceptionResultSource(Exception exception)
            {
                this.exception = ExceptionDispatchInfo.Capture(exception);
            }

            public T GetResult(short token)
            {
                if (!this.calledGet)
                {
                    this.calledGet = true;
                    GC.SuppressFinalize(this);//不要调用析构函数，防止在Finalize线程调用析构函数之后，GC再重复调用
                }
                this.exception.Throw();
                return default;
            }

            void ISTaskSource.GetResult(short token)
            {
                if (!this.calledGet)
                {
                    this.calledGet = true;
                    GC.SuppressFinalize(this);
                }
                this.exception.Throw();
            }

            public STaskStatus GetStatus(short token)
            {
                return STaskStatus.Faulted;
            }

            public STaskStatus UnsafeGetStatus()
            {
                return STaskStatus.Faulted;
            }

            public void OnCompleted(Action<object> continuation, object state, short token)
            {
                continuation(state);
            }

            ~ExceptionResultSource()
            {
                if (!this.calledGet)//未处理异常，抛出来
                {
                    STaskScheduler.PublishUnobservedTaskException(this.exception.SourceException);
                }
            }
        }

        /// <summary>
        /// 用于创建被取消的 STask
        /// </summary>
        private sealed class CanceledResultSource : ISTaskSource
        {
            private readonly CancellationToken cancellationToken;

            public CanceledResultSource(CancellationToken cancellationToken)
            {
                this.cancellationToken = cancellationToken;
            }

            public void GetResult(short token)
            {
                throw new OperationCanceledException(this.cancellationToken);
            }

            public STaskStatus GetStatus(short token)
            {
                return STaskStatus.Canceled;
            }

            public STaskStatus UnsafeGetStatus()
            {
                return STaskStatus.Canceled;
            }

            public void OnCompleted(Action<object> continuation, object state, short token)
            {
                continuation(state);
            }
        }

        private sealed class CanceledResultSource<T> : ISTaskSource<T>
        {
            private readonly CancellationToken cancellationToken;

            public CanceledResultSource(CancellationToken cancellationToken)
            {
                this.cancellationToken = cancellationToken;
            }

            public T GetResult(short token)
            {
                throw new OperationCanceledException(this.cancellationToken);
            }

            void ISTaskSource.GetResult(short token)
            {
                throw new OperationCanceledException(this.cancellationToken);
            }

            public STaskStatus GetStatus(short token)
            {
                return STaskStatus.Canceled;
            }

            public STaskStatus UnsafeGetStatus()
            {
                return STaskStatus.Canceled;
            }

            public void OnCompleted(Action<object> continuation, object state, short token)
            {
                continuation(state);
            }
        }
        #endregion
    }
}