// Copyright (c) Alexander Bogarsukov.
// Licensed under the MIT license. See the LICENSE.md file in the project root for more information.

using System;
using System.Collections;
using System.Diagnostics;
using System.Threading;

namespace UnityAppTools
{
	/// <summary>
	/// A yieldable asynchronous operation.
	/// </summary>
	public class AsyncResult<T> : AsyncResult, IAsyncOperation<T>, IEnumerator
	{
		#region data

		private T _result;

		#endregion

		#region interface

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
			: base(name)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncResult"/> class.
		/// </summary>
		public AsyncResult(AsyncCallback asyncCallback, object asyncState)
			: base(asyncCallback, asyncState)
		{
		}

		/// <summary>
		/// Initializes a new instance of the <see cref="AsyncResult"/> class.
		/// </summary>
		public AsyncResult(string name, AsyncCallback asyncCallback, object asyncState)
			: base(name, asyncCallback, asyncState)
		{
		}

		/// <summary>
		/// Blocks until the operation is completed.
		/// </summary>
		/// <returns>Result of the operation.</returns>
		public new T Join()
		{
			base.Join();
			return _result;
		}

		/// <summary>
		/// Blocks until the operation is completed. Uses <see cref="Thread.Sleep(int)"/> instead of <see cref="AsyncWaitHandle"/> for waiting.
		/// </summary>
		/// <returns>Result of the operation.</returns>
		public new T JoinSleep(int millisecondsSleepTimeout)
		{
			base.JoinSleep(millisecondsSleepTimeout);
			return _result;
		}

		#endregion

		#region IAsyncOperationController

		/// <summary>
		/// 
		/// </summary>
		/// <param name="result"></param>
		/// <param name="completedSynchronously"></param>
		/// <seealso cref="TrySetResult(T, bool)"/>
		public void SetResult(T result, bool completedSynchronously = false)
		{
			if (!TrySetResult(result, completedSynchronously))
			{
				ThrowCompleted();
			}
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="result"></param>
		/// <param name="completedSynchronously"></param>
		/// <returns></returns>
		/// <seealso cref="SetResult(T, bool)"/>
		public bool TrySetResult(T result, bool completedSynchronously = false)
		{
			if (TrySetStatus(StatusCompletedFlag, completedSynchronously))
			{
				_result = result;
				OnCompleted();
				return true;
			}

			return false;
		}

		#endregion

		#region IAsyncOperation

		/// <inheritdoc/>
		public T Result
		{
			get
			{
				if (!IsCompletedSuccessfully)
				{
					throw new InvalidOperationException("The operation result is not available.");
				}

				return _result;
			}
		}

		#endregion

		#region implementation
		#endregion
	}
}
