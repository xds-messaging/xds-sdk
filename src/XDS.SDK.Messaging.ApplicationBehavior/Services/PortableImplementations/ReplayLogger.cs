using System;
using System.Collections.Generic;
using XDS.Messaging.SDK.ApplicationBehavior.Services.Interfaces;
using XDS.SDK.Messaging.CrossTierTypes;

namespace XDS.Messaging.SDK.ApplicationBehavior.Services.PortableImplementations
{
	public class ReplayLogger : ILog
	{
		readonly Queue<Tuple<string, LogLevel, int>> _savedLogs =
			new Queue<Tuple<string, LogLevel, int>>();


		public Action<string, LogLevel, int> Callback;


		public void Log(string message, LogLevel category, int priority)
		{
			var date = DateTime.Now;
			var time = $"{date.Hour}:{date.Minute}:{date.Second}";
			string timestamped = $"{time} {message}";
			if (this.Callback != null)
			{
				this.Callback(timestamped, category, priority);
			}
			else
			{
				if (this._savedLogs.Count > 100)
					this._savedLogs.Dequeue();
				this._savedLogs.Enqueue(new Tuple<string, LogLevel, int>(timestamped, category, priority));
			}
		}

		public void ReplaySavedLogs(Action<string, LogLevel, int> callback)
		{
			if (callback == null)
				throw new ArgumentNullException("callback");

			while (this._savedLogs.Count > 0)
			{
				var log = this._savedLogs.Dequeue();
				this.Callback(log.Item1, log.Item2, log.Item3);
			}
		}

		public void Debug(string p)
		{
			Log(p, LogLevel.Info, 1);
			System.Diagnostics.Debug.WriteLine("Debug: {0}", p);
		}


		public void Exception(Exception e, string message = null)
		{
			var msg = message == null ? e.Message : $"{e.Message}, {message}";
			Log(msg, LogLevel.Error, 3);
			System.Diagnostics.Debug.WriteLine("Exeption: {0}", msg);
		}
	}
}