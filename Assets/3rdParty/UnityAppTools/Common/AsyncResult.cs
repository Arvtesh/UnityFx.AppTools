// Copyright (c) Alexander Bogarsukov.
// Licensed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Diagnostics;
#if NET_4_6
using System.Runtime.ExceptionServices;
#endif
using System.Threading;

namespace UnityAppTools
{
	/// <summary>
	/// Defines <see cref="IAsyncResult"/> related helpers.
	/// </summary>
	/// <seealso href="https://blogs.msdn.microsoft.com/nikos/2011/03/14/how-to-implement-the-iasyncresult-design-pattern/"/>
	[DebuggerDisplay("{DebuggerDisplay,nq}")]
	public class AsyncResult : IAsyncResult, IDisposable
	{
		#region data

		private const int _completedMask = StatusCompletedFlag | StatusFaultedFlag | StatusCanceledFlag;

		private readonly string _name;
		private readonly AsyncCallback _asyncCallback;
		private readonly object _asyncState;

		private Exception _exception;
		private EventWaitHandle _waitHandle;
		private int _status;

		#endregion

		#region interface

		protected const int StatusRunning = 0;
		protected const int StatusDisposedFlag = 1;
		protected const int StatusSynchronousFlag = 2;
		protected const int StatusCompletedFlag = 4;
		protected const int StatusFaultedFlag = 8;
		protected const int StatusCanceledFlag = 16;

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncResult"/> class.
		/// </summary>
		public AsyncResult()
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncResult"/> class.
		/// </summary>
		public AsyncResult(string name)
		{
			_name = name;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncResult"/> class.
		/// </summary>
		public AsyncResult(AsyncCallback asyncCallback, object asyncState)
		{
			_asyncCallback = asyncCallback;
			_asyncState = asyncState;
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncResult"/> class.
		/// </summary>
		public AsyncResult(string name, AsyncCallback asyncCallback, object asyncState)
		{
			_name = name;
			_asyncCallback = asyncCallback;
			_asyncState = asyncState;
		}

		/// <summary>
		/// Blocks until the operation is completed.
		/// </summary>
		/// <returns>Result of the operation.</returns>
		public void Join()
		{
			ThrowIfDisposed();

			if (!IsCompleted)
			{
				AsyncWaitHandle.WaitOne();
			}

			ThrowIfFaulted();
		}

		/// <summary>
		/// Blocks until the operation is completed. Uses <see cref="Thread.Sleep(int)"/> instead of <see cref="AsyncWaitHandle"/> for waiting.
		/// </summary>
		/// <returns>Result of the operation.</returns>
		public void JoinSleep(int millisecondsSleepTimeout)
		{
			ThrowIfDisposed();

			while (!IsCompleted)
			{
				Thread.Sleep(millisecondsSleepTimeout);
			}

			ThrowIfFaulted();
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="newStatus"></param>
		/// <param name="completedSynchronously"></param>
		/// <returns></returns>
		protected bool TrySetStatus(int newStatus, bool completedSynchronously)
		{
			if (_status == StatusRunning)
			{
				if (completedSynchronously)
				{
					newStatus |= StatusSynchronousFlag;
				}

				return Interlocked.CompareExchange(ref _status, newStatus, StatusRunning) == StatusRunning;
			}

			return false;
		}

		/// <summary>
		/// Throws an exception stored in the operation (if any).
		/// </summary>
		protected void ThrowIfFaulted()
		{
			if (_exception != null)
			{
#if NET_4_6
				ExceptionDispatchInfo.Capture(_exception).Throw();
#else
				throw _exception;
#endif
			}
		}

		/// <summary>
		/// 
		/// </summary>
		protected void ThrowCompleted()
		{
			throw new InvalidOperationException("The operation is already completed.");
		}

		/// <summary>
		/// Throws <see cref="ObjectDisposedException"/> if the operation has been disposed.
		/// </summary>
		protected void ThrowIfDisposed()
		{
			if ((_status & StatusDisposedFlag) != 0)
			{
				throw new ObjectDisposedException(GetOperationName());
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <returns></returns>
		protected int GetOperationStatus()
		{
			return _status;
		}

		/// <summary>
		/// Returns the operation name.
		/// </summary>
		/// <returns>Name of the operation.</returns>
		protected string GetOperationName()
		{
			return string.IsNullOrEmpty(_name) ? GetType().Name : _name;
		}

		#endregion

		#region static interface

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="result"></param>
		/// <returns></returns>
		public static AsyncResult<T> FromResult<T>(T result)
		{
			var op = new AsyncResult<T>();
			op.TrySetResult(result, true);
			return op;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="result"></param>
		/// <param name="asyncCallback"></param>
		/// <param name="asyncState"></param>
		/// <returns></returns>
		public static AsyncResult<T> FromResult<T>(T result, AsyncCallback asyncCallback, object asyncState)
		{
			var op = new AsyncResult<T>(asyncCallback, asyncState);
			op.TrySetResult(result, true);
			return op;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="e"></param>
		/// <param name="asyncCallback"></param>
		/// <param name="asyncState"></param>
		/// <returns></returns>
		public static AsyncResult<T> FromException<T>(Exception e)
		{
			var op = new AsyncResult<T>();
			op.TrySetException(e, true);
			return op;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="e"></param>
		/// <param name="asyncCallback"></param>
		/// <param name="asyncState"></param>
		/// <returns></returns>
		public static AsyncResult<T> FromException<T>(Exception e, AsyncCallback asyncCallback, object asyncState)
		{
			var op = new AsyncResult<T>(asyncCallback, asyncState);
			op.TrySetException(e, true);
			return op;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="asyncCallback"></param>
		/// <param name="asyncState"></param>
		/// <returns></returns>
		public static AsyncResult<T> FromCanceled<T>()
		{
			var op = new AsyncResult<T>();
			op.TrySetCanceled(true);
			return op;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <param name="asyncCallback"></param>
		/// <param name="asyncState"></param>
		/// <returns></returns>
		public static AsyncResult<T> FromCanceled<T>(AsyncCallback asyncCallback, object asyncState)
		{
			var op = new AsyncResult<T>(asyncCallback, asyncState);
			op.TrySetCanceled(true);
			return op;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="waitHandle"></param>
		/// <param name="asyncResult"></param>
		/// <returns></returns>
		public static EventWaitHandle TryCreateAsyncWaitHandle(ref EventWaitHandle waitHandle, IAsyncResult asyncResult)
		{
			if (waitHandle == null)
			{
				var done = asyncResult.IsCompleted;
				var mre = new ManualResetEvent(done);

				if (Interlocked.CompareExchange(ref waitHandle, mre, null) != null)
				{
					// Another thread created this object's event; dispose the event we just created.
					mre.Close();
				}
				else if (!done && asyncResult.IsCompleted)
				{
					// We published the event as unset, but the operation has subsequently completed;
					// set the event state properly so that callers do not deadlock.
					waitHandle.Set();
				}
			}

			return waitHandle;
		}

		#endregion

		#region virtual interface

		/// <summary>
		/// Called on each <see cref="MoveNext"/> invokation to update the operation state if needed.
		/// </summary>
		/// <seealso cref="OnCompleted"/>
		protected virtual void OnUpdate()
		{
		}

		/// <summary>
		/// Called when the operation is completed.
		/// </summary>
		/// <seealso cref="OnUpdate"/>
		protected virtual void OnCompleted()
		{
			if (_waitHandle != null)
			{
				_waitHandle.Set();
			}

			if (_asyncCallback != null)
			{
				_asyncCallback.Invoke(this);
			}
		}

		/// <summary>
		/// Releases unmanaged resources used by the object.
		/// </summary>
		/// <param name="disposing">Should be <see langword="true"/> if the method is called from <see cref="Dispose()"/>; <see langword="false"/> otherwise.</param>
		/// <seealso cref="Dispose()"/>
		/// <seealso cref="ThrowIfDisposed"/>
		protected virtual void Dispose(bool disposing)
		{
			if (disposing)
			{
				_status |= StatusDisposedFlag;

				if (_waitHandle != null)
				{
					_waitHandle.Close();
					_waitHandle = null;
				}
			}
		}

		#endregion

		#region IAsyncOperationController

		/// <summary>
		/// 
		/// </summary>
		/// <param name="completedSynchronously"></param>
		/// <seealso cref="TrySetException(Exception, bool)"/>
		public void SetCompleted(bool completedSynchronously = false)
		{
			if (!TrySetCompleted(completedSynchronously))
			{
				ThrowCompleted();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="completedSynchronously"></param>
		/// <returns></returns>
		/// <seealso cref="SetCanceled(bool)"/>
		public bool TrySetCompleted(bool completedSynchronously = false)
		{
			if (TrySetStatus(StatusCompletedFlag, completedSynchronously))
			{
				OnCompleted();
				return true;
			}

			return false;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="e"></param>
		/// <param name="completedSynchronously"></param>
		/// <seealso cref="TrySetException(Exception, bool)"/>
		public void SetException(Exception e, bool completedSynchronously = false)
		{
			if (!TrySetException(e, completedSynchronously))
			{
				ThrowCompleted();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="e"></param>
		/// <param name="completedSynchronously"></param>
		/// <returns></returns>
		/// <seealso cref="SetException(Exception, bool)"/>
		public bool TrySetException(Exception e, bool completedSynchronously = false)
		{
			var status = e is OperationCanceledException ? StatusCanceledFlag : StatusFaultedFlag;

			if (TrySetStatus(status, completedSynchronously))
			{
				_exception = e;
				OnCompleted();
				return true;
			}

			return false;
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="completedSynchronously"></param>
		/// <seealso cref="TrySetCanceled(bool)"/>
		public void SetCanceled(bool completedSynchronously = false)
		{
			if (!TrySetCanceled(completedSynchronously))
			{
				ThrowCompleted();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="completedSynchronously"></param>
		/// <returns></returns>
		/// <seealso cref="SetCanceled(bool)"/>
		public bool TrySetCanceled(bool completedSynchronously = false)
		{
			if (TrySetStatus(StatusCanceledFlag, completedSynchronously))
			{
				OnCompleted();
				return true;
			}

			return false;
		}

		#endregion

		#region IAsyncOperation

		/// <inheritdoc/>
		public Exception Exception
		{
			get
			{
				return _exception;
			}
		}

		/// <inheritdoc/>
		public bool IsCompletedSuccessfully
		{
			get
			{
				return (_status & _completedMask) == StatusCompletedFlag;
			}
		}

		/// <inheritdoc/>
		public bool IsFaulted
		{
			get
			{
				return _status >= StatusFaultedFlag;
			}
		}

		/// <inheritdoc/>
		public bool IsCanceled
		{
			get
			{
				return (_status & StatusCanceledFlag) != 0;
			}
		}

		#endregion

		#region IAsyncResult

		/// <inheritdoc/>
		public WaitHandle AsyncWaitHandle
		{
			get
			{
				ThrowIfDisposed();
				return TryCreateAsyncWaitHandle(ref _waitHandle, this);
			}
		}

		/// <inheritdoc/>
		public object AsyncState
		{
			get
			{
				return _asyncState;
			}
		}

		/// <inheritdoc/>
		public bool CompletedSynchronously
		{
			get
			{
				return (_status & StatusSynchronousFlag) != 0;
			}
		}

		/// <inheritdoc/>
		public bool IsCompleted
		{
			get
			{
				return _status > StatusRunning;
			}
		}

		#endregion

		#region IEnumerator

		/// <inheritdoc/>
		public object Current
		{
			get
			{
				return null;
			}
		}

		/// <inheritdoc/>
		public bool MoveNext()
		{
			if (IsCompleted)
			{
				return false;
			}
			else
			{
				try
				{
					OnUpdate();
				}
				catch (Exception e)
				{
					TrySetException(e);
				}

				return !IsCompleted;
			}
		}

		/// <inheritdoc/>
		public void Reset()
		{
			throw new NotSupportedException();
		}

		#endregion

		#region IDisposable

		/// <inheritdoc/>
		public void Dispose()
		{
			if (_status == StatusRunning)
			{
				throw new InvalidOperationException("Cannot dispose non-completed operation.");
			}

			Dispose(true);
			GC.SuppressFinalize(this);
		}

		#endregion

		#region implementation

		private string DebuggerDisplay
		{
			get
			{
				var result = GetOperationName();
				var state = "Running";
				var status = GetOperationStatus();

				if (IsCompletedSuccessfully)
				{
					state = "Completed";
				}
				else if (IsCanceled)
				{
					state = "Canceled";
				}
				else if (IsFaulted)
				{
					if (_exception != null)
					{
						state = "Faulted (" + _exception.GetType().Name + ')';
					}
					else
					{
						state = "Faulted";
					}
				}

				result += ", State = ";
				result += state;

				if ((status & StatusDisposedFlag) != 0)
				{
					result += ", Disposed";
				}

				return result;
			}
		}

		#endregion
	}
}
