using System.Reactive.Concurrency;
using System.Reactive.Linq;
using System.Runtime.CompilerServices;
using TwinCAT.Ads.Internal;
using TwinCAT.Ads.Tracing;
using TwinCAT.Ads;
using TwinCAT.TypeSystem;
using TwinCAT.Ads.Reactive;

namespace PLCObserverMinimalExample
{
    public static class Extensions
    {
        public static Dictionary<string, uint> Handles { get; private set; } = new();

        public static IObservable<ValueNotification> WhenNotificationWithHandle(this IAdsConnection connection, IList<AnySymbolSpecifier> symbols, NotificationSettings settings)
        {
            IAdsConnection connection2 = connection;
            IList<AnySymbolSpecifier> symbols2 = symbols;
            NotificationSettings settings2 = settings;
            if (connection2 == null)
            {
                throw new ArgumentNullException("connection");
            }

            if (symbols2 == null)
            {
                throw new ArgumentNullException("symbols");
            }

            if (symbols2.Count == 0)
            {
                throw new ArgumentOutOfRangeException("symbols", "Symbol list is empty!");
            }

            IDisposableHandleBag<AnySymbolSpecifier> bag = null;
            EventLoopScheduler scheduler = new EventLoopScheduler();
            IObservable<int> whenSymbolChangeObserver = connection2.WhenSymbolVersionChanges(scheduler);
            IDisposable whenSymbolChanges = null;
            Action<EventHandler<AdsNotificationExEventArgs>> addHandler = delegate (EventHandler<AdsNotificationExEventArgs> h)
            {
                connection2.AdsNotificationEx += h;
                bag = ((IAdsHandleCacheProvider)connection2).CreateNotificationExHandleBag(symbols2, relaxSubErrors: false, settings2, null);
                bag.CreateHandles();

                // Collect Handles
                Handles.Clear();
                foreach (var item in bag.SourceResultHandles)
                    Handles.Add(item.source.InstancePath, item.result.Handle);

                whenSymbolChanges = whenSymbolChangeObserver.Subscribe((Action<int>)delegate
                {
                    bag.CreateHandles();
                    Handles.Clear();
                    foreach (var item in bag.SourceResultHandles)
                        Handles.Add(item.source.InstancePath, item.result.Handle);

                }, (Action<Exception>)delegate
                {
                    TcTraceSource traceAds = AdsModule.TraceAds;
                    DefaultInterpolatedStringHandler defaultInterpolatedStringHandler = new DefaultInterpolatedStringHandler(101, 1);
                    defaultInterpolatedStringHandler.AppendLiteral("The AdsServer '");
                    defaultInterpolatedStringHandler.AppendFormatted(connection2.Address);
                    defaultInterpolatedStringHandler.AppendLiteral("' doesn't support SymbolVersionChanged Notifications! Handle recreation is not active!");
                    traceAds.TraceInformation(defaultInterpolatedStringHandler.ToStringAndClear());
                });
            };
            Action<EventHandler<AdsNotificationExEventArgs>> removeHandler = delegate (EventHandler<AdsNotificationExEventArgs> h)
            {
                if (whenSymbolChanges != null)
                {
                    whenSymbolChanges.Dispose();
                }

                scheduler.Dispose();
                if (bag != null)
                {
                    bag.Dispose();
                    bag = null;

                    Handles.Clear();
                }

                connection2.AdsNotificationEx -= h;
            };

            return from ev in Observable.FromEventPattern<EventHandler<AdsNotificationExEventArgs>, AdsNotificationExEventArgs>(addHandler, removeHandler)
                   where bag.Contains(ev.EventArgs.Handle)
                   select new ValueNotification(ev.EventArgs, ev.EventArgs.Value);
        }
    }
}
