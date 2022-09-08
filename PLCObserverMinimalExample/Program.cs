// See https://aka.ms/new-console-template for more information
using TwinCAT.Ads;
using TwinCAT.TypeSystem;
using System.Reactive;
using TwinCAT.Ads.Reactive;
using PLCObserverMinimalExample;

IDisposable _subscription = null;
AdsClient _adsClient = new();
List<SymbolModel> _values = new();

_adsClient.Connect("192.168.1.15.1.1", 801);
var plcState = _adsClient.ReadState().AdsState;
if (plcState == AdsState.Run)
{
    // Attach notification
    var valueNotificationVariables = new List<(string, Type)>();
    valueNotificationVariables.Add((".bPulse05s", typeof(bool)));
    valueNotificationVariables.Add((".bPulse1s", typeof(bool)));
    AttachNotification(valueNotificationVariables);

    Console.WriteLine("Press any button to close...");
    Console.ReadKey();
    _subscription.Dispose();
    _adsClient.Disconnect();
}

void AttachNotification(IEnumerable<(string key, Type type)> Symbols)
{
    var observerSymbolNotification = Observer.Create<ValueNotification>(val =>
    {
        // Identify the changed Variable
        if (Extensions.Handles.Any(x => x.Value == val.Handle))
        {
            var tag = Extensions.Handles.First(x => x.Value == val.Handle).Key;

            // Collect to dictionary
            if (!_values.Any(x => x.Key == tag))
                _values.Add(new SymbolModel { Key = tag, Value = val.Value });
            else
            {
                var symbol = _values.First(x => x.Key == tag);
                symbol.Value = val.Value;
            }
        }
    });

    if (_adsClient != null)
    {
        // Get Symbols from SymbolLoader
        List<AnySymbolSpecifier> list = new();
        List<string> userData = new();
        foreach (var (key, type) in Symbols)
        {
            list.Add(new AnySymbolSpecifier(key, new AnyTypeSpecifier(type)));
            userData.Add(key);
        }
        _subscription = _adsClient.WhenNotificationWithHandle(list, NotificationSettings.ImmediatelyOnChange)
                                              .Subscribe(observerSymbolNotification);
    }
}

class SymbolModel
{
    public string Key { get; set; }
    public object Value { get; set; }
}
